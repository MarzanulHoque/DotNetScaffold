using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation.Dtos;

public interface IEntityDtoViewModelBuilder
{
    /// <param name="targetNamespace">The generated DTOs' own namespace (e.g. <c>MyApp.BLL</c> or
    /// <c>MyApp.Application</c>) -- unrelated to <paramref name="entity"/>'s own namespace.</param>
    EntityDtoViewModel Build(EntityMetadata entity, DbContextModelMetadata model, string targetNamespace);
}
