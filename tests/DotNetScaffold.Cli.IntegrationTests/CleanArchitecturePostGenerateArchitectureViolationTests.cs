using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Cli.IntegrationTests;

/// <summary>
/// M8: the Clean Architecture counterpart to <see cref="LayeredPostGenerateArchitectureViolationTests"/>.
/// M2's own architecture-fitness-function proof only ever exercised Domain's "zero dependencies" rule on
/// a fresh, empty scaffold. This exercises the rule M6 actually made load-bearing --
/// `Application_Should_Not_Depend_On_Infrastructure_Or_Web` -- against a solution that has already been
/// through a real `generate --all` (so Application already holds real generated DTOs/service
/// interfaces), then reverts and confirms the fitness function passes again.
/// </summary>
public class CleanArchitecturePostGenerateArchitectureViolationTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private string _solutionRoot = string.Empty;
    private string _solutionName = string.Empty;

    public CleanArchitecturePostGenerateArchitectureViolationTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-post-gen-violation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    [Fact]
    public async Task ArchitectureTests_AfterGenerate_StillCatchesAnApplicationToInfrastructureViolation_ThenPassesAgainAfterReverting()
    {
        _solutionName = "PostGenCaViolationApp";
        var scaffolder = new CleanArchitectureSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
        await scaffolder.ScaffoldAsync(new ScaffoldRequest(ArchitectureType.CleanArchitecture, _solutionName, _outputDirectory));
        _solutionRoot = Path.Combine(_outputDirectory, _solutionName);

        var domainDir = Path.Combine(_solutionRoot, "src", $"{_solutionName}.Domain");
        File.WriteAllText(Path.Combine(domainDir, "Author.cs"), $$"""
            namespace {{_solutionName}}.Domain;

            public class Author
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }
            """);

        var infrastructureDir = Path.Combine(_solutionRoot, "src", $"{_solutionName}.Infrastructure");
        File.WriteAllText(Path.Combine(infrastructureDir, "AppDbContext.cs"), $$"""
            using Microsoft.EntityFrameworkCore;
            using {{_solutionName}}.Domain;

            namespace {{_solutionName}}.Infrastructure;

            public class AppDbContext : DbContext
            {
                public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
                {
                }

                public DbSet<Author> Authors => Set<Author>();
            }
            """);

        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{_solutionName}.Infrastructure/{_solutionName}.Infrastructure.csproj"]);
        await _dotnetCli.RunAsync(_solutionRoot, [FindCliDllPath(), "generate", "--all"]);

        // Build Infrastructure again (transitively builds Domain + Application too) now that generated
        // CRUD code exists, so Infrastructure.dll is available on disk for Application to take a raw
        // binary reference to below -- a ProjectReference the other way would fail MSBuild's own cycle
        // check before NetArchTest ever ran.
        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{_solutionName}.Infrastructure/{_solutionName}.Infrastructure.csproj"]);
        var infrastructureDllPath = Path.Combine(infrastructureDir, "bin", "Debug", "net8.0", $"{_solutionName}.Infrastructure.dll");
        File.Exists(infrastructureDllPath).Should().BeTrue("Infrastructure must already be built before Application can take a raw binary reference to it");

        var archTestsProject = $"tests/{_solutionName}.ArchitectureTests/{_solutionName}.ArchitectureTests.csproj";
        var applicationDir = Path.Combine(_solutionRoot, "src", $"{_solutionName}.Application");
        var applicationCsprojPath = Path.Combine(applicationDir, $"{_solutionName}.Application.csproj");
        var originalCsproj = File.ReadAllText(applicationCsprojPath);
        var violationCsPath = Path.Combine(applicationDir, "ArchitectureViolation.cs");

        try
        {
            File.WriteAllText(
                applicationCsprojPath,
                originalCsproj.Replace(
                    "</Project>",
                    $"""
                      <ItemGroup>
                        <Reference Include="{_solutionName}.Infrastructure">
                          <HintPath>{infrastructureDllPath}</HintPath>
                          <Private>false</Private>
                        </Reference>
                      </ItemGroup>
                    </Project>
                    """));
            File.WriteAllText(violationCsPath, $$"""
                namespace {{_solutionName}}.Application;

                public class ArchitectureViolation
                {
                    public {{_solutionName}}.Infrastructure.AppDbContext? Context { get; set; }
                }
                """);

            await _dotnetCli.RunAsync(_solutionRoot, ["build", archTestsProject]);

            var testAction = () => _dotnetCli.RunAsync(_solutionRoot, ["test", archTestsProject]);
            await testAction.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Application_Should_Not_Depend_On_Infrastructure_Or_Web*");
        }
        finally
        {
            File.WriteAllText(applicationCsprojPath, originalCsproj);
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
