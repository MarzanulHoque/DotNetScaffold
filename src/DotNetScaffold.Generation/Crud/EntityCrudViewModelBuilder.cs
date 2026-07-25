using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation.Crud;

public sealed class EntityCrudViewModelBuilder : IEntityCrudViewModelBuilder
{
    public EntityCrudViewModel Build(EntityMetadata entity, DbContextModelMetadata model)
    {
        var scalarProperties = entity.Properties.Select(ToProperty).ToList();
        var createOrUpdateProperties = entity.Properties
            .Where(p => !p.IsPrimaryKey)
            .Select(ToProperty)
            .ToList();

        var referenceNavigations = entity.Navigations
            .Where(n => !n.IsCollection)
            .Select(n => ReferenceNavigationFlattener.Flatten(n, model))
            .Where(f => f is not null)
            .Select(ToReferenceNavigation!)
            .ToList();

        var childCollections = entity.Navigations
            .Where(n => n.IsCollection)
            .Select(n => BuildChildCollection(n, model))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        return new EntityCrudViewModel(
            entity.ClrName,
            entity.Namespace,
            entity.PrimaryKey.Name,
            entity.PrimaryKey.ClrTypeName,
            SampleValueGenerator.LiteralFor(entity.PrimaryKey.ClrTypeName),
            scalarProperties,
            createOrUpdateProperties,
            referenceNavigations,
            childCollections);
    }

    private static CrudChildCollectionViewModel? BuildChildCollection(NavigationMetadata navigation, DbContextModelMetadata model)
    {
        var childEntity = model.Entities.FirstOrDefault(e => e.ClrName == navigation.RelatedEntityClrName);
        if (childEntity is null)
        {
            return null;
        }

        var childScalarProperties = childEntity.Properties.Select(ToProperty).ToList();
        var childReferenceNavigations = childEntity.Navigations
            .Where(n => !n.IsCollection)
            .Select(n => ReferenceNavigationFlattener.Flatten(n, model))
            .Where(f => f is not null)
            .Select(ToReferenceNavigation!)
            .ToList();

        return new CrudChildCollectionViewModel(
            navigation.PropertyName,
            navigation.RelatedEntityClrName,
            $"{navigation.RelatedEntityClrName}ListDto",
            childScalarProperties,
            childReferenceNavigations);
    }

    private static CrudReferenceNavigationViewModel ToReferenceNavigation(ReferenceNavigationFlattener.Flattened flattened) =>
        new(flattened.NavigationPropertyName, flattened.RelatedEntityClrName, flattened.FlattenedFieldName,
            flattened.FlattenedFieldCSharpTypeName, flattened.IsNullable, flattened.RelatedDisplayPropertyName,
            SampleValueGenerator.LiteralFor(flattened.FlattenedFieldCSharpTypeName));

    private static CrudPropertyViewModel ToProperty(PropertyMetadata property)
    {
        var typeName = property.IsNullable ? $"{property.ClrTypeName}?" : property.ClrTypeName;
        return new CrudPropertyViewModel(property.Name, typeName, SampleValueGenerator.LiteralFor(property.ClrTypeName));
    }
}
