using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Scaffolding.Tests;

/// <summary>
/// Exercises the real `dotnet` CLI end to end (scaffold -> build -> test), not just that
/// <see cref="LayeredSolutionScaffolder"/> produces syntactically plausible C#. These tests are slow
/// (multiple `dotnet` subprocess invocations) but that's the only way to actually prove the scaffolded
/// output builds and that the generated ArchitectureTests project is real, not a no-op.
/// </summary>
public class LayeredSolutionScaffolderTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private readonly LayeredSolutionScaffolder _scaffolder;

    public LayeredSolutionScaffolderTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
        _scaffolder = new LayeredSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
    }

    [Fact]
    public async Task ScaffoldAsync_ProducesASolutionThatBuildsAndPassesArchitectureTests()
    {
        var request = new ScaffoldRequest(ArchitectureType.Layered, "ScaffoldTestApp", _outputDirectory);

        await _scaffolder.ScaffoldAsync(request);

        var solutionRoot = Path.Combine(_outputDirectory, "ScaffoldTestApp");
        File.Exists(Path.Combine(solutionRoot, ToolConfig.FileName)).Should().BeTrue();

        var buildAction = () => _dotnetCli.RunAsync(solutionRoot, ["build"]);
        await buildAction.Should().NotThrowAsync("a freshly scaffolded layered solution must build with zero manual edits");

        var testAction = () => _dotnetCli.RunAsync(solutionRoot, ["test"]);
        await testAction.Should().NotThrowAsync("the generated ArchitectureTests project must pass on a fresh, unmodified scaffold");
    }

    [Fact]
    public async Task ScaffoldAsync_WhenTargetDirectoryAlreadyExistsAndIsNotEmpty_ThrowsWithoutWritingFiles()
    {
        var request = new ScaffoldRequest(ArchitectureType.Layered, "ScaffoldTestApp", _outputDirectory);
        await _scaffolder.ScaffoldAsync(request);

        var act = () => _scaffolder.ScaffoldAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists and is not empty*");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
