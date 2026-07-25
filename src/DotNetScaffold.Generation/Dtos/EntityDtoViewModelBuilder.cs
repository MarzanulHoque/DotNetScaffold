using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation.Dtos;

/// <summary>
/// Builds the view model the four DTO templates render from. Implements the relationship-flattening
/// rules from SRS 3.3.4: every reference navigation (one-to-one or the "many" side's reference to its
/// one-to-many parent) becomes a flattened scalar field instead of a nested object, and every collection
/// navigation becomes a list of the *child's list DTO* -- never the child's full detail DTO -- which is
/// what keeps a self-referencing entity's detail DTO (e.g. Category -> ChildCategories) from recursing
/// infinitely: {Entity}ListDto never itself contains a child collection, so nesting it one level deep is
/// always safe, without needing special-case handling for self-references at all.
/// </summary>
public sealed class EntityDtoViewModelBuilder : IEntityDtoViewModelBuilder
{
    public EntityDtoViewModel Build(EntityMetadata entity, DbContextModelMetadata model, string targetNamespace)
    {
        var scalarProperties = entity.Properties
            .Select(ToPropertyViewModel)
            .ToList();

        var flattenedReferences = entity.Navigations
            .Where(navigation => !navigation.IsCollection)
            .Select(navigation => BuildFlattenedReference(navigation, model))
            .Where(flattened => flattened is not null)
            .Select(flattened => flattened!)
            .ToList();

        var childCollections = entity.Navigations
            .Where(navigation => navigation.IsCollection)
            .Select(navigation => new DtoCollectionViewModel(navigation.PropertyName, $"{navigation.RelatedEntityClrName}ListDto"))
            .ToList();

        // Create/Update never expose the PK (server-assigned) or navigation-derived fields (a client
        // sets relationships via the raw FK scalar, e.g. AuthorId, which is already a regular property).
        var createUpdateProperties = entity.Properties
            .Where(property => !property.IsPrimaryKey)
            .Select(ToPropertyViewModel)
            .ToList();

        return new EntityDtoViewModel(
            targetNamespace,
            entity.ClrName,
            scalarProperties,
            flattenedReferences,
            childCollections,
            createUpdateProperties,
            createUpdateProperties);
    }

    private static DtoPropertyViewModel? BuildFlattenedReference(NavigationMetadata navigation, DbContextModelMetadata model)
    {
        var flattened = ReferenceNavigationFlattener.Flatten(navigation, model);
        return flattened is null ? null : new DtoPropertyViewModel(flattened.FlattenedFieldName, flattened.FlattenedFieldCSharpTypeName);
    }

    private static DtoPropertyViewModel ToPropertyViewModel(PropertyMetadata property) =>
        new(property.Name, property.IsNullable ? $"{property.ClrTypeName}?" : property.ClrTypeName);
}
