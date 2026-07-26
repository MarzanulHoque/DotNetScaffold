using System.Text.Json;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Generation.CleanArchitecture;
using DotNetScaffold.Generation.Crud;
using DotNetScaffold.Generation.Dtos;
using DotNetScaffold.Metadata;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Generation.Tests.CleanArchitecture;

/// <summary>
/// The real, end-to-end proof for M6: scaffold a fresh Clean Architecture solution (M2's real
/// <see cref="CleanArchitectureSolutionScaffolder"/>, not a stub), hand-author a SampleBlog-shaped model
/// into its Domain project plus a real <c>AppDbContext</c> into Infrastructure, build Infrastructure so
/// metadata can be read from it, run `generate --all` through the *real, built CLI executable* (see
/// <see cref="Layered.LayeredCrudGeneratorTests"/> for why this must be a separate process), then
/// `dotnet build` + `dotnet test` the whole solution. A passing string assertion on generated content
/// would prove nothing about whether the service/controller/tests actually compile and run against a
/// real EF Core InMemory-seeded <c>AppDbContext</c> -- same discipline used throughout M1-M5.
/// </summary>
public class CleanArchitectureCrudGeneratorTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private string _solutionRoot = string.Empty;

    public CleanArchitectureCrudGeneratorTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-ca-crud-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    [Fact]
    public async Task GenerateAsync_ForAllEntities_ProducesASolutionThatBuildsAndPassesGeneratedTests()
    {
        await ScaffoldAndPopulateModelAsync("CaGenTestApp");

        await _dotnetCli.RunAsync(_solutionRoot, [FindCliDllPath(), "generate", "--all"]);

        // I{Entity}Service/Controller/Service impl/tests were written for every entity.
        File.Exists(Path.Combine(_solutionRoot, "src", "CaGenTestApp.Application", "IPostService.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "src", "CaGenTestApp.Infrastructure", "PostService.Generated.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "src", "CaGenTestApp.Infrastructure", "PostService.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "src", "CaGenTestApp.Web", "PostController.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "tests", "CaGenTestApp.Infrastructure.Tests", "PostServiceTests.cs")).Should().BeTrue();

        var programCs = File.ReadAllText(Path.Combine(_solutionRoot, "src", "CaGenTestApp.Web", "Program.cs"));
        programCs.Should().Contain("builder.Services.AddScoped<IPostService, PostService>();");

        var buildAction = () => _dotnetCli.RunAsync(_solutionRoot, ["build"]);
        await buildAction.Should().NotThrowAsync("every generated artifact across all of SampleBlog's relationship kinds must be valid, compiling C#");

        var testAction = () => _dotnetCli.RunAsync(_solutionRoot, ["test"]);
        await testAction.Should().NotThrowAsync("the generated EF Core InMemory-seeded ServiceTests must actually pass, not just compile");
    }

    [Fact]
    public async Task GenerateAsync_WhenFilesAlreadyExistAndForceIsNotPassed_ThrowsWithoutOverwritingTheHandPartial()
    {
        await ScaffoldAndPopulateModelAsync("CaGenTestAppGuard");
        var config = ReadToolConfig();
        var generator = CreateGenerator();

        await generator.GenerateAsync(new GenerateRequest("Author", All: false, Force: false, _solutionRoot), config);

        var servicePartialPath = Path.Combine(_solutionRoot, "src", "CaGenTestAppGuard.Infrastructure", "AuthorService.cs");
        File.WriteAllText(servicePartialPath, File.ReadAllText(servicePartialPath) + "\n// hand-written marker");

        var act = () => generator.GenerateAsync(new GenerateRequest("Author", All: false, Force: false, _solutionRoot), config);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exist*");

        File.ReadAllText(servicePartialPath).Should().Contain("// hand-written marker");

        // Now with --force: guarded files are overwritten, but the hand partial is still preserved.
        await generator.GenerateAsync(new GenerateRequest("Author", All: false, Force: true, _solutionRoot), config);
        File.ReadAllText(servicePartialPath).Should().Contain("// hand-written marker");
    }

    private async Task ScaffoldAndPopulateModelAsync(string solutionName)
    {
        var scaffolder = new CleanArchitectureSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
        await scaffolder.ScaffoldAsync(new ScaffoldRequest(ArchitectureType.CleanArchitecture, solutionName, _outputDirectory));
        _solutionRoot = Path.Combine(_outputDirectory, solutionName);

        var domainDir = Path.Combine(_solutionRoot, "src", $"{solutionName}.Domain");
        File.WriteAllText(Path.Combine(domainDir, "Author.cs"), CleanArchitectureSampleModelSource.Author(solutionName));
        File.WriteAllText(Path.Combine(domainDir, "Post.cs"), CleanArchitectureSampleModelSource.Post(solutionName));
        File.WriteAllText(Path.Combine(domainDir, "PostDetail.cs"), CleanArchitectureSampleModelSource.PostDetail(solutionName));
        File.WriteAllText(Path.Combine(domainDir, "Category.cs"), CleanArchitectureSampleModelSource.Category(solutionName));
        File.WriteAllText(Path.Combine(domainDir, "Tag.cs"), CleanArchitectureSampleModelSource.Tag(solutionName));

        var infrastructureDir = Path.Combine(_solutionRoot, "src", $"{solutionName}.Infrastructure");
        File.WriteAllText(Path.Combine(infrastructureDir, "AppDbContext.cs"), CleanArchitectureSampleModelSource.AppDbContext(solutionName));

        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj"]);
    }

    private ToolConfig ReadToolConfig() =>
        JsonSerializer.Deserialize<ToolConfig>(File.ReadAllText(Path.Combine(_solutionRoot, ToolConfig.FileName)))!;

    private CleanArchitectureCrudGenerator CreateGenerator()
    {
        var templateEngine = new ScribanTemplateEngine();
        return new CleanArchitectureCrudGenerator(
            new TargetAssemblyLocator(),
            new EfModelReader(new PluginAssemblyLoader()),
            new DtoGenerator(templateEngine, new EntityDtoViewModelBuilder()),
            new EntityCrudViewModelBuilder(),
            templateEngine);
    }

    /// <summary>Locates the real, already-built CLI executable by walking up from the test's own output
    /// directory to the repo root -- see <see cref="Layered.LayeredCrudGeneratorTests"/> for why this must
    /// be a separate process from the in-process metadata read above.</summary>
    private static string FindCliDllPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "DotNetScaffold.Cli")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repo root from the test output directory.");
        }

        var cliDllPath = Path.Combine(directory.FullName, "src", "DotNetScaffold.Cli", "bin", "Debug", "net8.0", "DotNetScaffold.Cli.dll");
        if (!File.Exists(cliDllPath))
        {
            throw new InvalidOperationException($"'{cliDllPath}' not found -- build the solution before running this test.");
        }

        return cliDllPath;
    }

    /// <summary>Retries whole-directory cleanup since a prior in-process assembly read (the guard test
    /// above) may still be mid-unload when the test disposes.</summary>
    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(200);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            DeleteDirectoryWithRetry(_outputDirectory);
        }

        GC.SuppressFinalize(this);
    }
}
