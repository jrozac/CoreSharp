using System.Reflection;
using CoreSharp.DataAccess.Attributes;
using FluentNHibernate.Conventions;
using FluentNHibernate.Conventions.Instances;
using NHibernate.Cfg;

#nullable disable

namespace CoreSharp.NHibernate.PostgreSQL.Conventions
{
    public class IndexedAttributeConvention : AttributePropertyConvention<IndexAttribute>, IReferenceConvention
    {
        private readonly INamingStrategy _namingStrategy;
        
        public IndexedAttributeConvention(global::NHibernate.Cfg.Configuration configuration)
        {
            _namingStrategy = configuration.NamingStrategy;
        }
    
        protected override void Apply(IndexAttribute attribute, IPropertyInstance instance)
        {
            instance.Index(attribute.IsKeySet
                ? GetIndexName(instance.EntityType.Name, attribute.KeyName)
                : GetIndexName(instance.EntityType.Name, instance.Name));
        }

        public void Apply(IManyToOneInstance instance)
        {
            var attribute = instance.Property.MemberInfo.GetCustomAttribute<IndexAttribute>();

            if (attribute == null)
            {
                return;
            }

            instance.Index(attribute.IsKeySet
                ? GetIndexName(instance.EntityType.Name, attribute.KeyName)
                : GetIndexName(instance.EntityType.Name, instance.Name));
        }

        private string GetIndexName(string tableName, string name)
        {
            return $"ix__{_namingStrategy.TableName(name)}";
        }
    }
}
