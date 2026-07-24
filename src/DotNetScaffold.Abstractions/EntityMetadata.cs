namespace DotNetScaffold.Abstractions;

public enum RelationshipKind
{
    OneToOne,
    OneToMany,
}

/// <summary>A scalar (non-navigation) property of an entity, as read from EF Core's <c>IModel</c>.</summary>
public sealed record PropertyMetadata(
    string Name,
    string ClrTypeName,
    bool IsNullable,
    int? MaxLength,
    bool IsPrimaryKey,
    bool IsForeignKey);

/// <summary>
/// A navigation property on an entity. <paramref name="IsCollection"/> distinguishes the "many" side
/// (e.g. <c>Author.Posts</c>) from the reference side (e.g. <c>Post.Author</c>). Many-to-many
/// navigations are never represented here — they are recorded as <see cref="ManyToManySkip"/> instead.
/// </summary>
public sealed record NavigationMetadata(
    string PropertyName,
    bool IsCollection,
    string RelatedEntityClrName,
    RelationshipKind Kind,
    bool IsSelfReferencing,
    bool IsRequired,
    string? ForeignKeyPropertyName);

public sealed record EntityMetadata(
    string ClrName,
    string ClrFullName,
    string Namespace,
    IReadOnlyList<PropertyMetadata> Properties,
    IReadOnlyList<NavigationMetadata> Navigations)
{
    public PropertyMetadata PrimaryKey => Properties.First(p => p.IsPrimaryKey);
}

/// <summary>A many-to-many navigation detected on the model but skipped, per SRS 3.2.3 (out of scope for v1).</summary>
public sealed record ManyToManySkip(string EntityClrName, string NavigationPropertyName, string RelatedEntityClrName);

/// <summary>The full result of reading a target project's <c>DbContext</c> model.</summary>
public sealed record DbContextModelMetadata(
    string DbContextTypeName,
    IReadOnlyList<EntityMetadata> Entities,
    IReadOnlyList<ManyToManySkip> SkippedManyToMany);
