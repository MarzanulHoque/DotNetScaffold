namespace DotNetScaffold.Generation.Dtos;

/// <summary>One property to render on a DTO: its name and fully-formatted C# type (including a trailing
/// <c>?</c> when nullable).</summary>
public sealed record DtoPropertyViewModel(string Name, string CSharpTypeName);

/// <summary>One child collection to render on a detail DTO only, e.g. <c>ICollection&lt;PostListDto&gt; Posts</c>.</summary>
public sealed record DtoCollectionViewModel(string PropertyName, string ChildListDtoTypeName);

/// <summary>
/// Everything the four DTO templates (detail, list, create, update) need for one entity. Built by
/// <see cref="IEntityDtoViewModelBuilder"/> from <see cref="Abstractions.EntityMetadata"/>, independent
/// of which architecture is generating it -- <see cref="Namespace"/> is supplied by the caller (M5/M6),
/// since it's the *generated DTO's* namespace (e.g. <c>MyApp.BLL</c> or <c>MyApp.Application</c>), which
/// has nothing to do with the entity class's own namespace (e.g. <c>MyApp.DAL</c>).
/// </summary>
public sealed record EntityDtoViewModel(
    string Namespace,
    string EntityName,
    IReadOnlyList<DtoPropertyViewModel> ScalarProperties,
    IReadOnlyList<DtoPropertyViewModel> FlattenedReferenceProperties,
    IReadOnlyList<DtoCollectionViewModel> ChildCollections,
    IReadOnlyList<DtoPropertyViewModel> CreateProperties,
    IReadOnlyList<DtoPropertyViewModel> UpdateProperties);
