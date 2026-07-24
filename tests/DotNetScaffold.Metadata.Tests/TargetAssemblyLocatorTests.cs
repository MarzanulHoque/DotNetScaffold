using FluentAssertions;

namespace DotNetScaffold.Metadata.Tests;

public class TargetAssemblyLocatorTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly TargetAssemblyLocator _locator = new();

    public TargetAssemblyLocatorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-locator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void FindBuiltAssemblyPath_WhenProjectFileMissing_Throws()
    {
        var projectPath = Path.Combine(_tempDirectory, "MyApp.DAL.csproj");

        var act = () => _locator.FindBuiltAssemblyPath(projectPath);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Project file not found*");
    }

    [Fact]
    public void FindBuiltAssemblyPath_WhenNeverBuilt_ThrowsWithBuildInstruction()
    {
        var projectPath = WriteFakeProjectFile("MyApp.DAL");

        var act = () => _locator.FindBuiltAssemblyPath(projectPath);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Run 'dotnet build'*");
    }

    [Fact]
    public void FindBuiltAssemblyPath_WhenBuiltOnce_ReturnsThatOutput()
    {
        var projectPath = WriteFakeProjectFile("MyApp.DAL");
        var dllPath = WriteFakeBuildOutput("MyApp.DAL", "Debug", "net8.0");

        var result = _locator.FindBuiltAssemblyPath(projectPath);

        result.Should().Be(dllPath);
    }

    [Fact]
    public void FindBuiltAssemblyPath_WhenBuiltMultipleTimes_ReturnsTheMostRecentlyWrittenOutput()
    {
        var projectPath = WriteFakeProjectFile("MyApp.DAL");
        WriteFakeBuildOutput("MyApp.DAL", "Debug", "net8.0");
        Thread.Sleep(50); // ensure a distinct, later last-write-time
        var newerDllPath = WriteFakeBuildOutput("MyApp.DAL", "Release", "net8.0");

        var result = _locator.FindBuiltAssemblyPath(projectPath);

        result.Should().Be(newerDllPath);
    }

    private string WriteFakeProjectFile(string projectName)
    {
        var path = Path.Combine(_tempDirectory, $"{projectName}.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return path;
    }

    private string WriteFakeBuildOutput(string projectName, string configuration, string targetFramework)
    {
        var outputDirectory = Path.Combine(_tempDirectory, "bin", configuration, targetFramework);
        Directory.CreateDirectory(outputDirectory);
        var dllPath = Path.Combine(outputDirectory, $"{projectName}.dll");
        File.WriteAllBytes(dllPath, [0x4D, 0x5A]); // arbitrary bytes -- the locator never reads content
        return dllPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
