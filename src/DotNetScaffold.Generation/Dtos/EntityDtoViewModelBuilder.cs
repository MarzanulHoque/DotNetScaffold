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
        var relatedEntity = model.Entities.FirstOrDefault(e => e.ClrName == navigation.RelatedEntityClrName);
        if (relatedEntity is null)
        {
            return null;
        }

        var displayProperty = SelectDisplayProperty(relatedEntity);
        var isFallbackToPrimaryKey = false;

        if (displayProperty is null)
        {
            if (navigation.ForeignKeyPropertyName is not null)
            {
                // This entity already holds the FK scalar itself (e.g. Post.AuthorId) -- that's already
                // present in ScalarProperties, so there's nothing useful left to flatten.
                return null;
            }

            // This is the principal side of a one-to-one with no string display property on the
            // dependent (e.g. Post -> PostDetail, which only has Id/PostId/ViewCount) -- without some
            // fallback, this DTO would give zero indication the related row even exists. Fall back to
            // the related row's own primary key so callers can at least detect/fetch it.
            displayProperty = relatedEntity.Properties.FirstOrDefault(p => p.IsPrimaryKey);
            isFallbackToPrimaryKey = true;
            if (displayProperty is null)
            {
                return null;
            }
        }

        // A flattened field is nullable whenever the relationship itself is optional, even if the
        // underlying display property (e.g. Name) is not -- the navigation might legitimately be absent.
        // The principal side of a one-to-one is always treated as optional here regardless of the FK's
        // own IsRequired: IsRequired only guarantees the dependent's FK is non-null *if that dependent
        // row exists* -- it says nothing about whether a dependent row exists at all for this principal.
        var isNullable = displayProperty.IsNullable || !navigation.IsRequired || isFallbackToPrimaryKey;
        var typeName = isNullable ? $"{displayProperty.ClrTypeName}?" : displayProperty.ClrTypeName;

        return new DtoPropertyViewModel($"{navigation.PropertyName}{displayProperty.Name}", typeName);
    }

    /// <summary>Picks which scalar property of a related entity best represents it in a flattened field:
    /// "Name", then "Title", then the first non-key string property, else none (in which case
    /// <see cref="BuildFlattenedReference"/> may still fall back to the related entity's own PK).</summary>
    private static PropertyMetadata? SelectDisplayProperty(EntityMetadata entity) =>
        entity.Properties.FirstOrDefault(p => p.Name == "Name")
        ?? entity.Properties.FirstOrDefault(p => p.Name == "Title")
        ?? entity.Properties.FirstOrDefault(p => p.ClrTypeName == "string" && !p.IsPrimaryKey && !p.IsForeignKey);

    private static DtoPropertyViewModel ToPropertyViewModel(PropertyMetadata property) =>
        new(property.Name, property.IsNullable ? $"{property.ClrTypeName}?" : property.ClrTypeName);
}
