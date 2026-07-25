using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation;

/// <summary>
/// The single implementation of SRS 3.3.4's relationship-flattening rule, shared by
/// <see cref="Dtos.EntityDtoViewModelBuilder"/> (M4) and <see cref="Crud.EntityCrudViewModelBuilder"/> (M5+):
/// a reference navigation flattens to a scalar field on the related entity's "display" property (preferring
/// Name, then Title, then the first non-key string property), or -- when no such property exists -- falls
/// back to the related entity's own primary key so the DTO still indicates the related row exists.
/// </summary>
internal static class ReferenceNavigationFlattener
{
    internal sealed record Flattened(
        string NavigationPropertyName,
        string RelatedEntityClrName,
        string FlattenedFieldName,
        string FlattenedFieldCSharpTypeName,
        bool IsNullable,
        string RelatedDisplayPropertyName);

    /// <summary>Returns null when the navigation has nothing worth flattening (the related entity is
    /// unknown to the model, or this entity already holds the FK scalar itself with no better display
    /// property to add).</summary>
    internal static Flattened? Flatten(NavigationMetadata navigation, DbContextModelMetadata model)
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
                // This entity already holds the FK scalar itself (e.g. Post.AuthorId) -- that's already a
                // regular scalar property, so there's nothing useful left to flatten.
                return null;
            }

            // Principal side of a one-to-one with no string display property on the dependent (e.g.
            // Post -> PostDetail) -- fall back to the related row's own primary key so callers can at
            // least detect/fetch it, rather than giving zero indication it exists.
            displayProperty = relatedEntity.Properties.FirstOrDefault(p => p.IsPrimaryKey);
            isFallbackToPrimaryKey = true;
            if (displayProperty is null)
            {
                return null;
            }
        }

        // A flattened field is nullable whenever the relationship itself is optional, even if the
        // underlying display property is not -- the navigation might legitimately be absent. The
        // principal side of a one-to-one is always treated as optional here regardless of the FK's own
        // IsRequired, since IsRequired only guarantees the dependent's FK is non-null *if that dependent
        // row exists* -- it says nothing about whether a dependent row exists at all for this principal.
        var isNullable = displayProperty.IsNullable || !navigation.IsRequired || isFallbackToPrimaryKey;
        var typeName = isNullable ? $"{displayProperty.ClrTypeName}?" : displayProperty.ClrTypeName;

        return new Flattened(
            navigation.PropertyName,
            navigation.RelatedEntityClrName,
            $"{navigation.PropertyName}{displayProperty.Name}",
            typeName,
            isNullable,
            displayProperty.Name);
    }

    /// <summary>Picks which scalar property of a related entity best represents it in a flattened field:
    /// "Name", then "Title", then the first non-key string property, else none.</summary>
    private static PropertyMetadata? SelectDisplayProperty(EntityMetadata entity) =>
        entity.Properties.FirstOrDefault(p => p.Name == "Name")
        ?? entity.Properties.FirstOrDefault(p => p.Name == "Title")
        ?? entity.Properties.FirstOrDefault(p => p.ClrTypeName == "string" && !p.IsPrimaryKey && !p.IsForeignKey);
}
