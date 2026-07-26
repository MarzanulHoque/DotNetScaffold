using DotNetScaffold.Abstractions;
using DotNetScaffold.Generation.Crud;
using DotNetScaffold.Generation.Dtos;
using DotNetScaffold.Metadata;
using DotNetScaffold.Templating;

namespace DotNetScaffold.Generation.Layered;

/// <summary>
/// Implements `generate` for the layered (DAL/BLL/API) architecture: SYSTEM-DESIGN.md §5.2. Per entity,
/// renders the four DTOs (M4, unchanged, targeted at the BLL namespace), an <c>I{Entity}Repository</c>
/// marker (DAL), a partial-class-split <c>{Entity}Service</c> (BLL), an <c>{Entity}Controller</c> (API),
/// and Moq-based <c>{Entity}ServiceTests</c> (Tests.BLL) -- then registers the service in the API
/// project's DI container.
/// </summary>
public sealed class LayeredCrudGenerator : IArchitectureCrudGenerator
{
    private readonly ITargetAssemblyLocator _assemblyLocator;
    private readonly IDbContextModelReader _modelReader;
    private readonly IDtoGenerator _dtoGenerator;
    private readonly IEntityCrudViewModelBuilder _crudViewModelBuilder;
    private readonly ITemplateEngine _templateEngine;

    public LayeredCrudGenerator(
        ITargetAssemblyLocator assemblyLocator,
        IDbContextModelReader modelReader,
        IDtoGenerator dtoGenerator,
        IEntityCrudViewModelBuilder crudViewModelBuilder,
        ITemplateEngine templateEngine)
    {
        _assemblyLocator = assemblyLocator;
        _modelReader = modelReader;
        _dtoGenerator = dtoGenerator;
        _crudViewModelBuilder = crudViewModelBuilder;
        _templateEngine = templateEngine;
    }

    public Task GenerateAsync(GenerateRequest request, ToolConfig config, CancellationToken cancellationToken = default)
    {
        var dalProjectPath = Path.Combine(request.SolutionRoot, ToPlatformPath(config.Projects[LayerNames.Dal]));
        var assemblyPath = _assemblyLocator.FindBuiltAssemblyPath(dalProjectPath);
        var model = _modelReader.ReadModel(assemblyPath, config.DbContextTypeName);

        foreach (var skip in model.SkippedManyToMany.DistinctBy(s => (s.EntityClrName, s.NavigationPropertyName)))
        {
            Console.WriteLine(
                $"warning: many-to-many navigation '{skip.EntityClrName}.{skip.NavigationPropertyName}' " +
                "is not supported in v1 and was skipped (SRS 3.2.3).");
        }

        var targetEntities = ResolveTargetEntities(request, model);

        var dalName = ProjectName(config.Projects[LayerNames.Dal]);
        var bllName = ProjectName(config.Projects[LayerNames.Bll]);
        var apiName = ProjectName(config.Projects[LayerNames.Api]);
        var testsBllName = ProjectName(config.Projects[LayerNames.TestsBll]);

        var dalDir = ProjectDirectory(config.Projects[LayerNames.Dal]);
        var bllDir = ProjectDirectory(config.Projects[LayerNames.Bll]);
        var apiDir = ProjectDirectory(config.Projects[LayerNames.Api]);
        var testsBllDir = ProjectDirectory(config.Projects[LayerNames.TestsBll]);

        foreach (var entity in targetEntities)
        {
            GenerateForEntity(entity, model, request, dalName, bllName, apiName, testsBllName, dalDir, bllDir, apiDir, testsBllDir);
        }

        return Task.CompletedTask;
    }

    private void GenerateForEntity(
        EntityMetadata entity,
        DbContextModelMetadata model,
        GenerateRequest request,
        string dalName, string bllName, string apiName, string testsBllName,
        string dalDir, string bllDir, string apiDir, string testsBllDir)
    {
        var dtoFiles = _dtoGenerator.Generate(entity, model, bllName);
        var crudViewModel = _crudViewModelBuilder.Build(entity, model);

        var repositoryInterfaceContent = _templateEngine.Render(
            "Crud/RepositoryInterface.sbn", new { Namespace = dalName, EntityName = entity.ClrName, EntityNamespace = entity.Namespace });

        var controllerContent = _templateEngine.Render(
            "Crud/Controller.sbn",
            new
            {
                ApiNamespace = apiName,
                BllNamespace = bllName,
                crudViewModel.EntityName,
                crudViewModel.PrimaryKeyPropertyName,
                crudViewModel.PrimaryKeyCSharpTypeName,
            });

        var testsContent = _templateEngine.Render(
            "Crud/ServiceTests.sbn",
            new
            {
                TestsBllNamespace = testsBllName,
                DalNamespace = dalName,
                BllNamespace = bllName,
                EntityNamespace = entity.Namespace,
                crudViewModel.EntityName,
                crudViewModel.PrimaryKeyPropertyName,
                crudViewModel.PrimaryKeySampleValueLiteral,
                crudViewModel.ScalarProperties,
                crudViewModel.CreateOrUpdateProperties,
                crudViewModel.ReferenceNavigations,
                crudViewModel.ChildCollections,
            });

        var solutionRoot = request.SolutionRoot;

        // Files guarded by the exists-check-before-write / --force rule (SYSTEM-DESIGN.md §5.2 step 4).
        var guardedFiles = new List<(string Path, string Content)>();
        guardedFiles.AddRange(dtoFiles.Select(f => (Path.Combine(solutionRoot, bllDir, f.FileName), f.Content)));
        guardedFiles.Add((Path.Combine(solutionRoot, dalDir, $"I{entity.ClrName}Repository.cs"), repositoryInterfaceContent));
        guardedFiles.Add((Path.Combine(solutionRoot, apiDir, $"{entity.ClrName}Controller.cs"), controllerContent));
        guardedFiles.Add((Path.Combine(solutionRoot, testsBllDir, $"{entity.ClrName}ServiceTests.cs"), testsContent));

        if (!request.Force)
        {
            var alreadyExists = guardedFiles.Where(f => File.Exists(f.Path)).ToList();
            if (alreadyExists.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.ClrName}': {alreadyExists.Count} file(s) already exist " +
                    $"(e.g. '{alreadyExists[0].Path}'). Pass --force to overwrite.");
            }
        }

        // Always overwritten -- this is the generated half of the service's partial-class idempotency split.
        var serviceGeneratedContent = _templateEngine.Render(
            "Crud/ServiceGenerated.sbn",
            new
            {
                BllNamespace = bllName,
                DalNamespace = dalName,
                EntityNamespace = entity.Namespace,
                crudViewModel.EntityName,
                crudViewModel.PrimaryKeyCSharpTypeName,
                crudViewModel.ScalarProperties,
                crudViewModel.CreateOrUpdateProperties,
                crudViewModel.ReferenceNavigations,
                crudViewModel.ChildCollections,
            });

        var filesToWrite = new List<(string Path, string Content)>(guardedFiles)
        {
            (Path.Combine(solutionRoot, bllDir, $"{entity.ClrName}Service.Generated.cs"), serviceGeneratedContent),
        };

        // Written once, never overwritten (even with --force) -- the hand-edit half of the split.
        var servicePartialPath = Path.Combine(solutionRoot, bllDir, $"{entity.ClrName}Service.cs");
        if (!File.Exists(servicePartialPath))
        {
            var servicePartialContent = _templateEngine.Render(
                "Crud/ServicePartial.sbn", new { Namespace = bllName, EntityName = entity.ClrName });
            filesToWrite.Add((servicePartialPath, servicePartialContent));
        }

        // Written as one transaction per entity (SYSTEM-DESIGN.md §5.2 step 4 / §8 "Reliability"): if any
        // file in this entity's set fails to write, every file already written by this call rolls back.
        TransactionalFileWriter.WriteAll(filesToWrite);

        ProgramCsRegistrar.EnsureServiceRegistered(Path.Combine(solutionRoot, apiDir, "Program.cs"), bllName, entity.ClrName);
    }

    private static IReadOnlyList<EntityMetadata> ResolveTargetEntities(GenerateRequest request, DbContextModelMetadata model)
    {
        if (request.All)
        {
            return model.Entities;
        }

        var entity = model.Entities.SingleOrDefault(e => e.ClrName == request.EntityName)
            ?? throw new InvalidOperationException($"Entity '{request.EntityName}' was not found on '{model.DbContextTypeName}'.");
        return [entity];
    }

    private static string ProjectName(string csprojRelativePath) => Path.GetFileNameWithoutExtension(csprojRelativePath);

    private static string ProjectDirectory(string csprojRelativePath) =>
        Path.GetDirectoryName(ToPlatformPath(csprojRelativePath)) ?? string.Empty;

    private static string ToPlatformPath(string forwardSlashPath) => forwardSlashPath.Replace('/', Path.DirectorySeparatorChar);
}
