using FluentNHibernate.Conventions;
using FluentNHibernate.Conventions.Inspections;
using FluentNHibernate.Conventions.Instances;

namespace CoreSharp.NHibernate.SQLServer.Conventions
{
    public class ForeignKeyNameConvention : IReferenceConvention, IHasOneConvention, IHasManyToManyConvention
    {
        public void Apply(IManyToOneInstance instance)
        {
            var fkName = $"FK_{instance.EntityType.Name}To{instance.Class.Name}_{instance.Name}";

            instance.ForeignKey(fkName);
        }

        public void Apply(IOneToOneInstance instance)
        {
            var oneToOne = instance as IOneToOneInspector;
            var fkName = $"FK_{instance.EntityType.Name}To{oneToOne.Class.Name}_{instance.Name}";

            instance.ForeignKey(fkName);
        }

        public void Apply(IManyToManyCollectionInstance instance)
        {
            var fkName =
                $"FK_{instance.EntityType.Name}{instance.OtherSide.EntityType.Name}_{((ICollectionInspector) instance).Name}";

            instance.Relationship.ForeignKey(fkName);
        }
    }
}
