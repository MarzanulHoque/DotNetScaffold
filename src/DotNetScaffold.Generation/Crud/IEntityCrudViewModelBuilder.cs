using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation.Crud;

public interface IEntityCrudViewModelBuilder
{
    EntityCrudViewModel Build(EntityMetadata entity, DbContextModelMetadata model);
}
