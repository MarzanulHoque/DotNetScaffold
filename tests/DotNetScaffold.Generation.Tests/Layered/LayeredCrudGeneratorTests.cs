using System.Text.Json;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Generation.Crud;
using DotNetScaffold.Generation.Dtos;
using DotNetScaffold.Generation.Layered;
using DotNetScaffold.Metadata;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Generation.Tests.Layered;

/// <summary>
/// The real, end-to-end proof for M5: scaffold a fresh layered solution (M1's real
/// <see cref="LayeredSolutionScaffolder"/>, not a stub), hand-author a SampleBlog-shaped model directly
/// into its DAL project (one-to-many, one-to-one, self-referencing, and an intentional many-to-many),
/// build it so metadata can be read from it, run `generate --all` through the *real, built CLI executable*
/// (not the in-process generator) so this proves the actual wired-up composition root works and so the
/// process holding the DAL assembly loaded exits completely before `dotnet build` touches that file --
/// running `EfModelReader`'s collectible-ALC-based read in the *same* long-lived test host process that
/// then also shells out to `dotnet build` against that exact DLL deadlocks on an OS-level file lock that
/// never clears (proven by direct experiment, not assumed) -- then `dotnet build` + `dotnet test` the
/// whole solution. A passing string assertion on generated content would prove nothing about whether the
/// DTOs/service/controller/tests actually compile and run against real EF Core -- this is the same
/// discipline used throughout M1-M4.
/// </summary>
public class LayeredCrudGeneratorTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private string _solutionRoot = string.Empty;
    private string _solutionName = string.Empty;

    public LayeredCrudGeneratorTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-crud-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    [Fact]
    public async Task GenerateAsync_ForAllEntities_ProducesASolutionThatBuildsAndPassesGeneratedTests()
    {
        await ScaffoldAndPopulateModelAsync("GenTestApp");

        await _dotnetCli.RunAsync(_solutionRoot, [FindCliDllPath(), "generate", "--all"]);

        // I{Entity}Repository/Controller/Service/tests were written for every entity.
        File.Exists(Path.Combine(_solutionRoot, "src", "GenTestApp.DAL", "IAuthorRepository.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "src", "GenTestApp.BLL", "PostService.Generated.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "src", "GenTestApp.BLL", "PostService.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "src", "GenTestApp.API", "PostController.cs")).Should().BeTrue();
        File.Exists(Path.Combine(_solutionRoot, "tests", "GenTestApp.Tests.BLL", "PostServiceTests.cs")).Should().BeTrue();

        var programCs = File.ReadAllText(Path.Combine(_solutionRoot, "src", "GenTestApp.API", "Program.cs"));
        programCs.Should().Contain("builder.Services.AddScoped<PostService>();");

        var buildAction = () => _dotnetCli.RunAsync(_solutionRoot, ["build"]);
        await buildAction.Should().NotThrowAsync("every generated artifact across all of SampleBlog's relationship kinds must be valid, compiling C#");

        var testAction = () => _dotnetCli.RunAsync(_solutionRoot, ["test"]);
        await testAction.Should().NotThrowAsync("the generated Moq-based ServiceTests must actually pass, not just compile");
    }

    [Fact]
    public async Task GenerateAsync_WhenFilesAlreadyExistAndForceIsNotPassed_ThrowsWithoutOverwritingTheHandPartial()
    {
        await ScaffoldAndPopulateModelAsync("GenTestAppGuard");
        var config = ReadToolConfig();
        var generator = CreateGenerator();

        await generator.GenerateAsync(new GenerateRequest("Author", All: false, Force: false, _solutionRoot), config);

        var servicePartialPath = Path.Combine(_solutionRoot, "src", "GenTestAppGuard.BLL", "AuthorService.cs");
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
        _solutionName = solutionName;
        var scaffolder = new LayeredSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
        await scaffolder.ScaffoldAsync(new ScaffoldRequest(ArchitectureType.Layered, solutionName, _outputDirectory));
        _solutionRoot = Path.Combine(_outputDirectory, solutionName);

        var dalDir = Path.Combine(_solutionRoot, "src", $"{solutionName}.DAL");
        File.WriteAllText(Path.Combine(dalDir, "Author.cs"), SampleModelSource.Author(solutionName));
        File.WriteAllText(Path.Combine(dalDir, "Post.cs"), SampleModelSource.Post(solutionName));
        File.WriteAllText(Path.Combine(dalDir, "PostDetail.cs"), SampleModelSource.PostDetail(solutionName));
        File.WriteAllText(Path.Combine(dalDir, "Category.cs"), SampleModelSource.Category(solutionName));
        File.WriteAllText(Path.Combine(dalDir, "Tag.cs"), SampleModelSource.Tag(solutionName));
        File.WriteAllText(Path.Combine(dalDir, "AppDbContext.cs"), SampleModelSource.AppDbContext(solutionName));

        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{solutionName}.DAL/{solutionName}.DAL.csproj"]);
    }

    private ToolConfig ReadToolConfig() =>
        JsonSerializer.Deserialize<ToolConfig>(File.ReadAllText(Path.Combine(_solutionRoot, ToolConfig.FileName)))!;

    private LayeredCrudGenerator CreateGenerator()
    {
        var templateEngine = new ScribanTemplateEngine();
        return new LayeredCrudGenerator(
            new TargetAssemblyLocator(),
            new EfModelReader(new PluginAssemblyLoader()),
            new DtoGenerator(templateEngine, new EntityDtoViewModelBuilder()),
            new EntityCrudViewModelBuilder(),
            templateEngine);
    }

    /// <summary>Locates the real, already-built CLI executable by walking up from the test's own output
    /// directory to the repo root -- used so the "does it actually build+test" test exercises the genuine
    /// composition root (see class remarks for why this must be a separate process).</summary>
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
    /// below) may still be mid-unload when the test disposes.</summary>
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
