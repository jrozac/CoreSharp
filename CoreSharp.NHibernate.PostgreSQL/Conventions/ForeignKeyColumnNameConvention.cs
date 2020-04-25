using System;
using FluentNHibernate;
using FluentNHibernate.Conventions;
using NHibernate.Cfg;

namespace CoreSharp.NHibernate.PostgreSQL.Conventions
{
    public class ForeignKeyColumnNameConvention : ForeignKeyConvention
    {
        private readonly INamingStrategy _namingStrategy;
        
        public ForeignKeyColumnNameConvention(global::NHibernate.Cfg.Configuration configuration)
        {
            _namingStrategy = configuration.NamingStrategy;
        }
        
        protected override string GetKeyName(Member property, Type type)
        {
            if (property == null)
            {
                return _namingStrategy.TableName(type.Name + "Id");
            }

            return _namingStrategy.TableName(property.Name + "Id");
        }
    }
}
