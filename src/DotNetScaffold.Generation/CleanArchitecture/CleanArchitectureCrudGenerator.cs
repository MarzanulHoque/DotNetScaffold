using DotNetScaffold.Abstractions;
using DotNetScaffold.Generation.Crud;
using DotNetScaffold.Generation.Dtos;
using DotNetScaffold.Metadata;
using DotNetScaffold.Templating;

namespace DotNetScaffold.Generation.CleanArchitecture;

/// <summary>
/// Implements `generate` for the Clean Architecture (Domain/Application/Infrastructure/Web) architecture:
/// SYSTEM-DESIGN.md §5.3. Per entity, renders the four DTOs (M4, unchanged, targeted at the Application
/// namespace), an <c>I{Entity}Service</c> interface (Application), a partial-class-split
/// <c>{Entity}Service</c> implementation that injects <c>AppDbContext</c> directly -- no repository layer,
/// per the SRS -- (Infrastructure), an <c>{Entity}Controller</c> (Web), and EF Core InMemory-seeded
/// <c>{Entity}ServiceTests</c> (Infrastructure.Tests) -- then registers the service in the Web project's
/// DI container. Mirrors <see cref="Layered.LayeredCrudGenerator"/>.
/// </summary>
public sealed class CleanArchitectureCrudGenerator : IArchitectureCrudGenerator
{
    private readonly ITargetAssemblyLocator _assemblyLocator;
    private readonly IDbContextModelReader _modelReader;
    private readonly IDtoGenerator _dtoGenerator;
    private readonly IEntityCrudViewModelBuilder _crudViewModelBuilder;
    private readonly ITemplateEngine _templateEngine;

    public CleanArchitectureCrudGenerator(
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
        var infrastructureProjectPath = Path.Combine(request.SolutionRoot, ToPlatformPath(config.Projects[LayerNames.Infrastructure]));
        var assemblyPath = _assemblyLocator.FindBuiltAssemblyPath(infrastructureProjectPath);
        var model = _modelReader.ReadModel(assemblyPath, config.DbContextTypeName);

        foreach (var skip in model.SkippedManyToMany.DistinctBy(s => (s.EntityClrName, s.NavigationPropertyName)))
        {
            Console.WriteLine(
                $"warning: many-to-many navigation '{skip.EntityClrName}.{skip.NavigationPropertyName}' " +
                "is not supported in v1 and was skipped (SRS 3.2.3).");
        }

        var targetEntities = ResolveTargetEntities(request, model);

        var applicationName = ProjectName(config.Projects[LayerNames.Application]);
        var infrastructureName = ProjectName(config.Projects[LayerNames.Infrastructure]);
        var webName = ProjectName(config.Projects[LayerNames.Web]);
        var infrastructureTestsName = ProjectName(config.Projects[LayerNames.InfrastructureTests]);

        var applicationDir = ProjectDirectory(config.Projects[LayerNames.Application]);
        var infrastructureDir = ProjectDirectory(config.Projects[LayerNames.Infrastructure]);
        var webDir = ProjectDirectory(config.Projects[LayerNames.Web]);
        var infrastructureTestsDir = ProjectDirectory(config.Projects[LayerNames.InfrastructureTests]);

        foreach (var entity in targetEntities)
        {
            GenerateForEntity(
                entity, model, request,
                applicationName, infrastructureName, webName, infrastructureTestsName,
                applicationDir, infrastructureDir, webDir, infrastructureTestsDir);
        }

        return Task.CompletedTask;
    }

    private void GenerateForEntity(
        EntityMetadata entity,
        DbContextModelMetadata model,
        GenerateRequest request,
        string applicationName, string infrastructureName, string webName, string infrastructureTestsName,
        string applicationDir, string infrastructureDir, string webDir, string infrastructureTestsDir)
    {
        var dtoFiles = _dtoGenerator.Generate(entity, model, applicationName);
        var crudViewModel = _crudViewModelBuilder.Build(entity, model);

        var serviceInterfaceContent = _templateEngine.Render(
            "CleanArchitecture/ServiceInterface.sbn",
            new
            {
                ApplicationNamespace = applicationName,
                crudViewModel.EntityName,
                crudViewModel.PrimaryKeyCSharpTypeName,
            });

        var controllerContent = _templateEngine.Render(
            "CleanArchitecture/Controller.sbn",
            new
            {
                WebNamespace = webName,
                ApplicationNamespace = applicationName,
                crudViewModel.EntityName,
                crudViewModel.PrimaryKeyPropertyName,
                crudViewModel.PrimaryKeyCSharpTypeName,
            });

        var testsContent = _templateEngine.Render(
            "CleanArchitecture/ServiceTests.sbn",
            new
            {
                InfrastructureTestsNamespace = infrastructureTestsName,
                ApplicationNamespace = applicationName,
                InfrastructureNamespace = infrastructureName,
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
        guardedFiles.AddRange(dtoFiles.Select(f => (Path.Combine(solutionRoot, applicationDir, f.FileName), f.Content)));
        guardedFiles.Add((Path.Combine(solutionRoot, applicationDir, $"I{entity.ClrName}Service.cs"), serviceInterfaceContent));
        guardedFiles.Add((Path.Combine(solutionRoot, webDir, $"{entity.ClrName}Controller.cs"), controllerContent));
        guardedFiles.Add((Path.Combine(solutionRoot, infrastructureTestsDir, $"{entity.ClrName}ServiceTests.cs"), testsContent));

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

        foreach (var (path, content) in guardedFiles)
        {
            WriteFile(path, content);
        }

        // Always overwritten -- this is the generated half of the service's partial-class idempotency split.
        var serviceGeneratedContent = _templateEngine.Render(
            "CleanArchitecture/ServiceGenerated.sbn",
            new
            {
                ApplicationNamespace = applicationName,
                InfrastructureNamespace = infrastructureName,
                EntityNamespace = entity.Namespace,
                crudViewModel.EntityName,
                crudViewModel.PrimaryKeyPropertyName,
                crudViewModel.PrimaryKeyCSharpTypeName,
                crudViewModel.ScalarProperties,
                crudViewModel.CreateOrUpdateProperties,
                crudViewModel.ReferenceNavigations,
                crudViewModel.ChildCollections,
            });
        WriteFile(Path.Combine(solutionRoot, infrastructureDir, $"{entity.ClrName}Service.Generated.cs"), serviceGeneratedContent);

        // Written once, never overwritten (even with --force) -- the hand-edit half of the split.
        var servicePartialPath = Path.Combine(solutionRoot, infrastructureDir, $"{entity.ClrName}Service.cs");
        if (!File.Exists(servicePartialPath))
        {
            var servicePartialContent = _templateEngine.Render(
                "CleanArchitecture/ServicePartial.sbn", new { Namespace = infrastructureName, EntityName = entity.ClrName });
            WriteFile(servicePartialPath, servicePartialContent);
        }

        ProgramCsRegistrar.EnsureServiceRegistered(Path.Combine(solutionRoot, webDir, "Program.cs"), applicationName, infrastructureName, entity.ClrName);
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

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
