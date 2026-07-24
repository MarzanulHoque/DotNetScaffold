using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation.Dtos;

public interface IDtoGenerator
{
    /// <summary>Renders all four DTOs (detail, list, create, update) for one entity. Content only --
    /// see <see cref="GeneratedFile"/>.</summary>
    IReadOnlyList<GeneratedFile> Generate(EntityMetadata entity, DbContextModelMetadata model, string targetNamespace);
}
