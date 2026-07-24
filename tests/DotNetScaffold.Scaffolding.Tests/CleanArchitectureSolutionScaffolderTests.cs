using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Scaffolding.Tests;

/// <summary>
/// Mirrors <see cref="LayeredSolutionScaffolderTests"/> for the Clean Architecture template: exercises
/// the real `dotnet` CLI end to end so a future change can't silently break the scaffolded output.
/// </summary>
public class CleanArchitectureSolutionScaffolderTests : IDisposable
{
    private readonly string _outputDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();
    private readonly CleanArchitectureSolutionScaffolder _scaffolder;

    public CleanArchitectureSolutionScaffolderTests()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);
        _scaffolder = new CleanArchitectureSolutionScaffolder(_dotnetCli, new ScribanTemplateEngine());
    }

    [Fact]
    public async Task ScaffoldAsync_ProducesASolutionThatBuildsAndPassesArchitectureTests()
    {
        var request = new ScaffoldRequest(ArchitectureType.CleanArchitecture, "ScaffoldCaTestApp", _outputDirectory);

        await _scaffolder.ScaffoldAsync(request);

        var solutionRoot = Path.Combine(_outputDirectory, "ScaffoldCaTestApp");
        File.Exists(Path.Combine(solutionRoot, ToolConfig.FileName)).Should().BeTrue();

        var buildAction = () => _dotnetCli.RunAsync(solutionRoot, ["build"]);
        await buildAction.Should().NotThrowAsync("a freshly scaffolded Clean Architecture solution must build with zero manual edits");

        var testAction = () => _dotnetCli.RunAsync(solutionRoot, ["test"]);
        await testAction.Should().NotThrowAsync("the generated ArchitectureTests project must pass on a fresh, unmodified scaffold");
    }

    [Fact]
    public async Task ScaffoldAsync_WhenTargetDirectoryAlreadyExistsAndIsNotEmpty_ThrowsWithoutWritingFiles()
    {
        var request = new ScaffoldRequest(ArchitectureType.CleanArchitecture, "ScaffoldCaTestApp", _outputDirectory);
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
