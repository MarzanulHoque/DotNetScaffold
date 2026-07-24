using DotNetScaffold.Abstractions;
using DotNetScaffold.Templating;

namespace DotNetScaffold.Generation.Dtos;

public sealed class DtoGenerator : IDtoGenerator
{
    private readonly ITemplateEngine _templateEngine;
    private readonly IEntityDtoViewModelBuilder _viewModelBuilder;

    public DtoGenerator(ITemplateEngine templateEngine, IEntityDtoViewModelBuilder viewModelBuilder)
    {
        _templateEngine = templateEngine;
        _viewModelBuilder = viewModelBuilder;
    }

    public IReadOnlyList<GeneratedFile> Generate(EntityMetadata entity, DbContextModelMetadata model, string targetNamespace)
    {
        var viewModel = _viewModelBuilder.Build(entity, model, targetNamespace);

        return
        [
            new GeneratedFile($"{entity.ClrName}Dto.cs", _templateEngine.Render("Dtos/EntityDto.sbn", viewModel)),
            new GeneratedFile($"{entity.ClrName}ListDto.cs", _templateEngine.Render("Dtos/EntityListDto.sbn", viewModel)),
            new GeneratedFile($"Create{entity.ClrName}Dto.cs", _templateEngine.Render("Dtos/CreateEntityDto.sbn", viewModel)),
            new GeneratedFile($"Update{entity.ClrName}Dto.cs", _templateEngine.Render("Dtos/UpdateEntityDto.sbn", viewModel)),
        ];
    }
}
