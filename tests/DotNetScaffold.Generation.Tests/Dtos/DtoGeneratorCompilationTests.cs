using DotNetScaffold.Generation.Dtos;
using DotNetScaffold.Metadata;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;
using FluentAssertions;

namespace DotNetScaffold.Generation.Tests.Dtos;

/// <summary>
/// The correctness proof that actually matters: generated DTO *strings* being reasonable-looking is not
/// the same as them being valid C#. This generates all four DTOs for every entity in the real
/// samples/SampleBlog model and compiles the result for real, the same discipline used for the
/// scaffolders in M1/M2 (a passing string-content assertion proves nothing about whether `dotnet build`
/// would actually accept the output).
/// </summary>
public class DtoGeneratorCompilationTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IDotnetCli _dotnetCli = new DotnetCliRunner();

    public DtoGeneratorCompilationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-dto-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task GeneratedDtos_ForEveryEntityInSampleBlog_AreValidCSharpThatBuilds()
    {
        var modelReader = new EfModelReader(new PluginAssemblyLoader());
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "SampleBlog.dll");
        var model = modelReader.ReadModel(assemblyPath, "SampleBlog.SampleBlogContext");

        var dtoGenerator = new DtoGenerator(new ScribanTemplateEngine(), new EntityDtoViewModelBuilder());

        await _dotnetCli.RunAsync(_tempDirectory, ["new", "classlib", "-n", "GeneratedDtos", "-o", ".", "-f", "net8.0", "--no-restore"]);
        File.Delete(Path.Combine(_tempDirectory, "Class1.cs"));

        foreach (var entity in model.Entities)
        {
            foreach (var file in dtoGenerator.Generate(entity, model, "GeneratedDtos"))
            {
                File.WriteAllText(Path.Combine(_tempDirectory, file.FileName), file.Content);
            }
        }

        var buildAction = () => _dotnetCli.RunAsync(_tempDirectory, ["build"]);
        await buildAction.Should().NotThrowAsync(
            "every generated DTO across all of SampleBlog's relationship kinds must be valid, compiling C#");
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
