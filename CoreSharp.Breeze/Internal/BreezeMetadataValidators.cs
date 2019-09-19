﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CoreSharp.Breeze.Events;
using CoreSharp.Breeze.Metadata;
using CoreSharp.Common.Exceptions;
using CoreSharp.Common.Extensions;
using CoreSharp.Cqrs.Events;
using CoreSharp.DataAccess;
using CoreSharp.Validation;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;

namespace CoreSharp.Breeze.Internal
{
    internal class BreezeMetadataValidators : IEventHandler<BreezeMetadataBuiltEvent>
    {
        private readonly IValidatorFactory _validatorFactory;
        private static readonly HashSet<string> NullableProperties = new HashSet<string>();
        private static bool _configured;

        public BreezeMetadataValidators(IValidatorFactory validatorFactory)
        {
            _validatorFactory = validatorFactory;
        }

        public static void AddNullableProperties(IEnumerable<string> propNames)
        {
            if (_configured)
            {
                throw new CoreSharpException("Cannot add nullable properties to BreezeMetadataConfigurator as it is locked. " +
                                                       "Hint: Call AddNullableProperties method before the BreezeMetadataBuiltEvent event is triggered.");
            }
            foreach (var propName in propNames)
            {
                NullableProperties.Add(propName);
            }
        }

        public void Handle(BreezeMetadataBuiltEvent asyncEvent)
        {
            _configured = true;

            SetupClientModels(asyncEvent);
            SetupValidators(asyncEvent);
        }

        private static MetadataSchema SetupClientModels(BreezeMetadataBuiltEvent asyncEvent)
        {
            var metadata = asyncEvent.Metadata;

            //Setup client models (not persisted)
            var clientClass = typeof(IClientModel);
            var clientModelTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && clientClass.IsAssignableFrom(type))).ToList();

            foreach (var clientModelType in clientModelTypes)
            {
                var entityType = new EntityType();

                var properties = clientModelType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.Name != "Id" && p.DeclaringType == clientModelType);

                entityType.ShortName = clientModelType.Name;
                entityType.Namespace = clientModelType.Namespace;
                entityType.AutoGeneratedKeyType = Metadata.AutoGeneratedKeyType.KeyGenerator;
                entityType.DefaultResourceName = Pluralize(clientModelType.Name);
                entityType["isUnmapped"] = true;
                entityType.NavigationProperties = new NavigationProperties();
                entityType.DataProperties = new DataProperties();

                entityType.DataProperties.Add(new DataProperty()
                {
                    NameOnServer = "Id",
                    DataType = DataType.Int64,
                    IsNullable = false,
                    IsPartOfKey = true,
                    Validators = new Validators()
                    {
                        new Validator()
                        {
                            Name = "integer"
                        }
                    }
                });

                foreach (var property in properties)
                {
                    var propertyType = property.PropertyType;
                    var isNullable = !propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null;

                    entityType.DataProperties.Add(new DataProperty()
                    {
                        NameOnServer = property.Name,
                        DataType = BreezeTypeHelper.GetDataType(Nullable.GetUnderlyingType(property.PropertyType) ??
                                                                property.PropertyType),
                        IsNullable = isNullable,
                        Validators = new Validators()
                    });
                }

                metadata.StructuralTypes.Add(entityType);

                metadata.ResourceEntityTypeMap[entityType.DefaultResourceName] = $"{entityType.ShortName}:#{entityType.Namespace}";
            }

            return metadata;
        }

        private MetadataSchema SetupValidators(BreezeMetadataBuiltEvent asyncEvent)
        {
            var metadata = asyncEvent.Metadata;

            //Setup entity models (persisted)
            var entityClass = typeof(IEntity);
            var clientModelClass = typeof(IClientModel);
            var entityModelTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()
                .Where(type => type.IsClass /*&& !type.IsAbstract*/ && (entityClass.IsAssignableFrom(type) || clientModelClass.IsAssignableFrom(type)))).ToList();

            foreach (var structuralType in metadata.StructuralTypes.Where(x => x is EntityType))
            {
                var entityModelType = entityModelTypes.FirstOrDefault(e => e.Name == structuralType.ShortName);
                if (entityModelType != null)
                {
                    var validationRules = _validatorFactory.GetValidator(entityModelType) as IEnumerable<IValidationRule>;
                    if (validationRules == null)
                    {
                        continue;
                    }

                    var allPropertyRules = validationRules.OfType<PropertyRule>()
                        .Where(x => !string.IsNullOrEmpty(x.PropertyName))
                        .ToLookup(o => o.PropertyName, o => o);

                    foreach (var dataProperty in structuralType.DataProperties)
                    {
                        dataProperty.Validators = dataProperty.Validators ?? new Validators();

                        var convertedVals = ConvertToFluentValidators(dataProperty, structuralType);

                        var propertyRules = allPropertyRules[dataProperty.NameOnServer]
                            .Where(pr => pr.RuleSets == null || ValidationRuleSet.AttributeInsertUpdateDefault.Intersect(pr.RuleSets).Any());

                        foreach (var propertyRule in propertyRules)
                        {
                            var currentValidator = propertyRule.CurrentValidator;
                            var name = FluentValidators.GetName(currentValidator);
                            if (string.IsNullOrEmpty(name) || convertedVals.Contains(name))
                            {
                                continue;
                            }

                            var validator = new Validator() { Name = name };
                            validator.MergeLeft(FluentValidators.GetParamaters(currentValidator));
                            dataProperty.Validators.AddOrUpdate(validator);
                        }
                    }
                }
            }

            //Setup codelists
            //var codeListType = typeof(ICodeList);
            //var codeListTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()
            //        .Where(type => type.IsClass && !type.IsAbstract && codeListClass.IsAssignableFrom(type))).ToList();
            /*foreach (var structuralType in metadata.StructuralTypes.OfType<EntityType>())
            {
                if (structuralType.Namespace.EndsWith(".CodeLists"))
                {
                    structuralType.AutoGeneratedKeyType = Metadata.AutoGeneratedKeyType.KeyGenerator;
                }
            }*/

            var versionedEntityType = typeof(IVersionedEntity);
            var versionedEntityProperties = new[]
                {"CreatedDate", "CreatedBy", "CreatedById", "ModifiedByDate", "ModifiedBy", "ModifiedById"};
            foreach (var structuralType in metadata.StructuralTypes.OfType<EntityType>())
            {
                var type = Type.GetType($"{structuralType.Namespace}.{structuralType.ShortName}");

                if (type != null && type.IsAssignableFrom(versionedEntityType))
                {
                    foreach (var property in structuralType.DataProperties)
                    {
                        if (versionedEntityProperties.Contains(property.NameOnServer))
                        {
                            property.IsNullable = true;
                            if (property.Validators != null)
                            {
                                var requiredValidator = property.Validators.FirstOrDefault(x => x.Name == "required");
                                if (requiredValidator != null)
                                {
                                    property.Validators.Remove(requiredValidator);
                                }
                            }
                        }
                    }
                }
            }

            return metadata;
        }

        private static string Pluralize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var last = s.Length - 1;
            var c = s[last];
            switch (c)
            {
                case 'y':
                    return s.Substring(0, last) + "ies";
                default:
                    return s + 's';
            }
        }

        private static List<string> ConvertToFluentValidators(DataProperty dataProp, StructuralType structuralType)
        {
            var toReplace = new HashSet<string> { "required", "maxLength" };
            var convertedVals = new List<string>();
            var entityType = structuralType as EntityType;
            foreach (var validator in dataProp.Validators.Where(o => toReplace.Contains(o.Name)).ToList())
            {
                dataProp.Validators.Remove(validator);
                Validator newValidator = null;
                var name = validator.Name;

                if (name == "required" && NullableProperties.Contains(dataProp.NameOnServer))
                {
                    convertedVals.Add("fvNotNull");
                    convertedVals.Add("fvNotEmpty");
                    dataProp.IsNullable = true;
                    continue;
                }

                switch (name)
                {
                    case "required":
                        //Check if the property is a foreignKey if it is then default type value is not valid
                        if (entityType != null && !string.IsNullOrEmpty(dataProp.NameOnServer) &&
                            entityType.NavigationProperties
                                .Any(o => o.ForeignKeyNamesOnServer != null && o.ForeignKeyNamesOnServer
                                              .Any(fk => fk == dataProp.NameOnServer)))
                        {
                            newValidator = new Validator { Name = "fvNotEmpty" };
                            var defVal = dataProp.PropertyInfo?.PropertyType.GetDefaultValue();
                            newValidator.MergeLeft(FluentValidators.GetParamaters(new NotEmptyValidator(defVal)));
                            convertedVals.Add("fvNotEmpty");
                        }
                        else
                        {
                            newValidator = new Validator { Name = "fvNotNull" };
                            newValidator.MergeLeft(FluentValidators.GetParamaters(new NotNullValidator()));
                            convertedVals.Add("fvNotNull");
                        }
                        break;
                    case "maxLength":
                        newValidator = new Validator { Name = "fvLength" };
                        newValidator.MergeLeft(FluentValidators.GetParamaters(new LengthValidator(0, dataProp.MaxLength)));
                        convertedVals.Add("fvLength");
                        break;
                }
                dataProp.Validators.Add(newValidator);
            }
            return convertedVals;
        }
    }

    public static class ValidatorsExtensions
    {
        public static void AddOrUpdate(this Validators validators, Validator validator)
        {
            var existingValidator = validators.SingleOrDefault(x => x.Name == validator.Name);

            if (existingValidator != null)
            {
                validators.Remove(existingValidator);
            }

            validators.Add(validator);
        }
    }
}
