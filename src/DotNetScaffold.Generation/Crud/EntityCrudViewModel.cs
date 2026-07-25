namespace DotNetScaffold.Generation.Crud;

/// <summary>One scalar property to render/assign in generated CRUD code: its name and fully-formatted
/// C# type (including a trailing <c>?</c> when nullable).</summary>
public sealed record CrudPropertyViewModel(string Name, string CSharpTypeName, string SampleValueLiteral);

/// <summary>
/// One flattened reference navigation (SRS 3.3.4), carrying both the flattened DTO field shape
/// (<see cref="FlattenedFieldName"/>/<see cref="FlattenedFieldCSharpTypeName"/>) and the raw navigation
/// info needed to generate the mapping expression that reads it off a loaded entity, e.g.
/// <c>entity.Author?.Name ?? string.Empty</c>. <see cref="RelatedDisplayPropertySampleValueLiteral"/>
/// lets generated tests build a plausible related-entity instance for seed data.
/// </summary>
public sealed record CrudReferenceNavigationViewModel(
    string NavigationPropertyName,
    string RelatedEntityClrName,
    string FlattenedFieldName,
    string FlattenedFieldCSharpTypeName,
    bool IsNullable,
    string RelatedDisplayPropertyName,
    string RelatedDisplayPropertySampleValueLiteral);

/// <summary>
/// One collection navigation, rendered as <c>ICollection&lt;{ChildListDtoTypeName}&gt;</c> on the detail
/// DTO. Carries the *child* entity's own scalar/flattened-reference shape so the generated service can
/// inline-map each child to its list DTO without depending on another entity's generated service class
/// (which may not exist yet if the user runs <c>generate --entity</c> for a single entity) -- the same
/// "single-level, no further recursion" discipline already used for self-referencing entities in M4.
/// </summary>
public sealed record CrudChildCollectionViewModel(
    string NavigationPropertyName,
    string ChildEntityClrName,
    string ChildListDtoTypeName,
    IReadOnlyList<CrudPropertyViewModel> ChildScalarProperties,
    IReadOnlyList<CrudReferenceNavigationViewModel> ChildReferenceNavigations);

/// <summary>Everything the layered (M5) and Clean Architecture (M6) CRUD templates need for one entity,
/// architecture-agnostic -- namespaces/wiring specifics are supplied by the caller.</summary>
public sealed record EntityCrudViewModel(
    string EntityName,
    string EntityNamespace,
    string PrimaryKeyPropertyName,
    string PrimaryKeyCSharpTypeName,
    string PrimaryKeySampleValueLiteral,
    IReadOnlyList<CrudPropertyViewModel> ScalarProperties,
    IReadOnlyList<CrudPropertyViewModel> CreateOrUpdateProperties,
    IReadOnlyList<CrudReferenceNavigationViewModel> ReferenceNavigations,
    IReadOnlyList<CrudChildCollectionViewModel> ChildCollections);
