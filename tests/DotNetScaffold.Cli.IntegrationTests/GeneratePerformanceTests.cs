using System.Diagnostics;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Cli.IntegrationTests;

/// <summary>
/// M8: measures the SRS performance NFR (SYSTEM-DESIGN.md §9: "15-20 entity DbContext generates in
/// less than 30 seconds") against a synthetic 18-entity model. Unverified until now -- M5-M7's own
/// end-to-end tests all use SampleBlog's 5-entity model, sized for relationship coverage, not throughput.
/// </summary>
public class GeneratePerformanceTests : IDisposable
{
    private const int EntityCount = 18;

    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private string _solutionRoot = string.Empty;

    public GeneratePerformanceTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-perf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
    }

    [Fact]
    public async Task GenerateAll_ForAnEighteenEntityDbContext_CompletesInUnderThirtySeconds()
    {
        const string solutionName = "PerfTestApp";
        var scaffolder = new LayeredSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
        await scaffolder.ScaffoldAsync(new ScaffoldRequest(ArchitectureType.Layered, solutionName, _outputDirectory));
        _solutionRoot = Path.Combine(_outputDirectory, solutionName);

        var dalNamespace = $"{solutionName}.DAL";
        var dalDir = Path.Combine(_solutionRoot, "src", dalNamespace);
        var entityNames = SyntheticEntityModelSource.EntityNames(EntityCount);
        foreach (var entityName in entityNames)
        {
            File.WriteAllText(Path.Combine(dalDir, $"{entityName}.cs"), SyntheticEntityModelSource.Entity(dalNamespace, entityName));
        }

        File.WriteAllText(
            Path.Combine(dalDir, "AppDbContext.cs"),
            SyntheticEntityModelSource.AppDbContext(dalNamespace, dalNamespace, entityNames));

        await _dotnetCli.RunAsync(_solutionRoot, ["build", $"src/{solutionName}.DAL/{solutionName}.DAL.csproj"]);

        var stopwatch = Stopwatch.StartNew();
        await _dotnetCli.RunAsync(_solutionRoot, [FindCliDllPath(), "generate", "--all"]);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(30),
            $"the SRS performance NFR requires a {EntityCount}-entity DbContext to fully `generate --all` in under 30 seconds");

        // Also prove codegen at this scale actually compiles, not just that it was fast.
        var buildAction = () => _dotnetCli.RunAsync(_solutionRoot, ["build"]);
        await buildAction.Should().NotThrowAsync($"every generated artifact across all {EntityCount} entities must be valid, compiling C#");
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
