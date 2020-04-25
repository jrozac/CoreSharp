using System.Linq;
using System.Reflection;
using CoreSharp.DataAccess.Attributes;
using FluentNHibernate.Conventions;
using FluentNHibernate.Conventions.Instances;
using NHibernate.Cfg;

#nullable disable

namespace CoreSharp.NHibernate.PostgreSQL.Conventions
{
    public class UniqueAttributeConvention : AttributePropertyConvention<UniqueAttribute>, IReferenceConvention
    {
        private readonly INamingStrategy _namingStrategy;
        
        public UniqueAttributeConvention(global::NHibernate.Cfg.Configuration configuration)
        {
            _namingStrategy = configuration.NamingStrategy;
        }
        
        protected override void Apply(UniqueAttribute attribute, IPropertyInstance instance)
        {
            if (attribute.IsKeySet)
            {
                var keys = attribute.KeyName.Split(',').Select(x => $"ux_{_namingStrategy.TableName(x.Trim())}");

                instance.UniqueKey(string.Join(",", keys));
            }
            else
            {
                instance.Unique();
            }
        }

        public void Apply(IManyToOneInstance instance)
        {
            var attribute = instance.Property.MemberInfo.GetCustomAttribute<UniqueAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (attribute.IsKeySet)
            {
                var keys = attribute.KeyName.Split(',').Select(x => $"ux_{_namingStrategy.TableName(x.Trim())}");

                instance.UniqueKey(string.Join(",", keys));
            }
            else
            {
                instance.Unique();
            }
        }
    }
}
