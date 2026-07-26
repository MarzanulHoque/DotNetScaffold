using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Cli.IntegrationTests;

/// <summary>
/// M8: M1's own architecture-fitness-function proof only ever exercised a freshly-scaffolded, empty
/// solution (no generated code exists yet at that point). This proves the same `DAL_Should_Not_Depend_On_
/// BLL_Or_API` fitness function still actually catches a real violation once real generated CRUD code
/// exists -- and that it goes back to green once the violation is reverted -- as a permanent, automated
/// regression test rather than the one-off manual check SYSTEM-DESIGN.md §7 describes for M1.
/// </summary>
public class LayeredPostGenerateArchitectureViolationTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private string _solutionRoot = string.Empty;
    private string _solutionName = string.Empty;

    public LayeredPostGenerateArchitectureViolationTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-post-gen-violation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    [Fact]
    public async Task ArchitectureTests_AfterGenerate_StillCatchesADalToBllViolation_ThenPassesAgainAfterReverting()
    {
        _solutionName = "PostGenViolationApp";
        var scaffolder = new LayeredSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
        await scaffolder.ScaffoldAsync(new ScaffoldRequest(ArchitectureType.Layered, _solutionName, _outputDirectory));
        _solutionRoot = Path.Combine(_outputDirectory, _solutionName);

        var dalDir = Path.Combine(_solutionRoot, "src", $"{_solutionName}.DAL");
        File.WriteAllText(Path.Combine(dalDir, "Author.cs"), $$"""
            namespace {{_solutionName}}.DAL;

            public class Author
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }
            """);
        File.WriteAllText(Path.Combine(dalDir, "AppDbContext.cs"), $$"""
            using Microsoft.EntityFrameworkCore;

            namespace {{_solutionName}}.DAL;

            public class AppDbContext : DbContext
            {
                public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
                {
                }

                public DbSet<Author> Authors => Set<Author>();
            }
            """);

        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{_solutionName}.DAL/{_solutionName}.DAL.csproj"]);
        await _dotnetCli.RunAsync(_solutionRoot, [FindCliDllPath(), "generate", "--all"]);

        // Build BLL (transitively builds DAL too) so BLL.dll exists on disk for DAL to take a raw binary
        // reference to below -- a ProjectReference the other way would fail MSBuild's own cycle check
        // before NetArchTest ever ran (SYSTEM-DESIGN.md §7), so the injected violation must be a raw
        // <Reference HintPath="..."> instead.
        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{_solutionName}.BLL/{_solutionName}.BLL.csproj"]);
        var bllDllPath = Path.Combine(_solutionRoot, "src", $"{_solutionName}.BLL", "bin", "Debug", "net8.0", $"{_solutionName}.BLL.dll");
        File.Exists(bllDllPath).Should().BeTrue("BLL must already be built before DAL can take a raw binary reference to it");

        var archTestsProject = $"tests/{_solutionName}.ArchitectureTests/{_solutionName}.ArchitectureTests.csproj";
        var dalCsprojPath = Path.Combine(dalDir, $"{_solutionName}.DAL.csproj");
        var originalCsproj = File.ReadAllText(dalCsprojPath);
        var violationCsPath = Path.Combine(dalDir, "ArchitectureViolation.cs");

        try
        {
            File.WriteAllText(
                dalCsprojPath,
                originalCsproj.Replace(
                    "</Project>",
                    $"""
                      <ItemGroup>
                        <Reference Include="{_solutionName}.BLL">
                          <HintPath>{bllDllPath}</HintPath>
                          <Private>false</Private>
                        </Reference>
                      </ItemGroup>
                    </Project>
                    """));
            File.WriteAllText(violationCsPath, $$"""
                namespace {{_solutionName}}.DAL;

                public class ArchitectureViolation
                {
                    public {{_solutionName}}.BLL.AuthorService? Service { get; set; }
                }
                """);

            await _dotnetCli.RunAsync(_solutionRoot, ["build", archTestsProject]);

            var testAction = () => _dotnetCli.RunAsync(_solutionRoot, ["test", archTestsProject]);
            await testAction.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*DAL_Should_Not_Depend_On_BLL_Or_API*");
        }
        finally
        {
            File.WriteAllText(dalCsprojPath, originalCsproj);
            if (File.Exists(violationCsPath))
            {
                File.Delete(violationCsPath);
            }
        }

        var revertedTestAction = () => _dotnetCli.RunAsync(_solutionRoot, ["test", archTestsProject]);
        await revertedTestAction.Should().NotThrowAsync("reverting the violation must make the fitness function pass again");
    }

    /// <summary>See <c>LayeredCrudGeneratorTests.FindCliDllPath</c> for why `generate` must run through
    /// the real, already-built CLI executable as a subprocess rather than in-process.</summary>
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
