using System;
using System.Linq;
using System.Text.RegularExpressions;
using FluentNHibernate.Conventions;
using FluentNHibernate.Conventions.Inspections;
using FluentNHibernate.Conventions.Instances;
using NHibernate.Cfg;

namespace CoreSharp.NHibernate.PostgreSQL.Conventions
{
    public class ForeignKeyNameConvention : IReferenceConvention, IHasOneConvention, IHasManyToManyConvention
    {
        private static Regex SplitRegex = new Regex("__", RegexOptions.Compiled);
        private readonly INamingStrategy _namingStrategy;
        
        public ForeignKeyNameConvention(global::NHibernate.Cfg.Configuration configuration)
        {
            _namingStrategy = configuration.NamingStrategy;
        }
        
        public void Apply(IManyToOneInstance instance)
        {
            var fkName = GetFkName($"fk__{GetTableName(instance.EntityType.Name)}__{GetTableName(instance.Class.Name)}__{GetTableName(instance.Name)}");

            instance.ForeignKey(fkName);
        }

        public void Apply(IOneToOneInstance instance)
        {
            var oneToOne = instance as IOneToOneInspector;
            var fkName = GetFkName($"fk__{GetTableName(instance.EntityType.Name)}__{GetTableName(oneToOne.Class.Name)}__{GetTableName(instance.Name)}");

            instance.ForeignKey(fkName);
        }

        public void Apply(IManyToManyCollectionInstance instance)
        {
            var fkName =
                GetFkName($"fk__{GetTableName(instance.EntityType.Name)}__{GetTableName(instance.OtherSide.EntityType.Name)}__{GetTableName(((ICollectionInspector) instance).Name)}");

            instance.Relationship.ForeignKey(fkName);
        }

        private string GetTableName(string name)
        {
            return _namingStrategy.TableName(name);
        }

        private static string GetFkName(string name)
        {
            var split = SplitRegex.Split(name);
            var shorten = name;
            var length = 18;

            while (shorten.Length > 63)
            {
                shorten = string.Join("", split.Select(x => x.Length > length ? x.Substring(0, length) : x));
                length--;

                if (length < 10)
                {
                    throw new ApplicationException($"FK name too long: {name}");
                }
            }

            return shorten;
        }
    }
}
