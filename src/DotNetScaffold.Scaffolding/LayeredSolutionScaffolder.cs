using System.Text.Json;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;

namespace DotNetScaffold.Scaffolding;

/// <summary>
/// Scaffolds the layered (DAL/BLL/API, repository-pattern) template: SRS 3.1.1. Project/solution
/// plumbing (csproj/sln creation, references) is delegated to the `dotnet` CLI itself via
/// <see cref="IDotnetCli"/>; only the interesting source files (AppDbContext, repository/unit-of-work,
/// Program.cs, architecture tests) are rendered from Scriban templates.
/// </summary>
public sealed class LayeredSolutionScaffolder : ISolutionScaffolder
{
    private const string SrcDir = "src";
    private const string TestsDir = "tests";

    private readonly IDotnetCli _dotnetCli;
    private readonly ITemplateEngine _templateEngine;

    public LayeredSolutionScaffolder(IDotnetCli dotnetCli, ITemplateEngine templateEngine)
    {
        _dotnetCli = dotnetCli;
        _templateEngine = templateEngine;
    }

    public async Task ScaffoldAsync(ScaffoldRequest request, CancellationToken cancellationToken = default)
    {
        var solutionName = request.SolutionName;
        var solutionRoot = Path.Combine(request.OutputDirectory, solutionName);

        if (Directory.Exists(solutionRoot) && Directory.EnumerateFileSystemEntries(solutionRoot).Any())
        {
            throw new InvalidOperationException(
                $"'{solutionRoot}' already exists and is not empty. Choose a different --name/--output or remove it first.");
        }

        Directory.CreateDirectory(solutionRoot);

        var dalName = $"{solutionName}.DAL";
        var bllName = $"{solutionName}.BLL";
        var apiName = $"{solutionName}.API";
        var testsDalName = $"{solutionName}.Tests.DAL";
        var testsBllName = $"{solutionName}.Tests.BLL";
        var archTestsName = $"{solutionName}.ArchitectureTests";

        var dalDir = Path.Combine(SrcDir, dalName);
        var bllDir = Path.Combine(SrcDir, bllName);
        var apiDir = Path.Combine(SrcDir, apiName);
        var testsDalDir = Path.Combine(TestsDir, testsDalName);
        var testsBllDir = Path.Combine(TestsDir, testsBllName);
        var archTestsDir = Path.Combine(TestsDir, archTestsName);

        await _dotnetCli.RunAsync(solutionRoot, ["new", "sln", "-n", solutionName, "-f", "sln"], cancellationToken);

        await CreateProjectAsync(solutionRoot, "classlib", dalName, dalDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "classlib", bllName, bllDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "web", apiName, apiDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "xunit", testsDalName, testsDalDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "xunit", testsBllName, testsBllDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "xunit", archTestsName, archTestsDir, cancellationToken);

        DeleteIfExists(Path.Combine(solutionRoot, dalDir, "Class1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, bllDir, "Class1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, testsDalDir, "UnitTest1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, testsBllDir, "UnitTest1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, archTestsDir, "UnitTest1.cs"));

        await _dotnetCli.RunAsync(
            solutionRoot,
            [
                "sln", "add",
                CsprojPath(dalDir, dalName), CsprojPath(bllDir, bllName), CsprojPath(apiDir, apiName),
                CsprojPath(testsDalDir, testsDalName), CsprojPath(testsBllDir, testsBllName),
                CsprojPath(archTestsDir, archTestsName),
            ],
            cancellationToken);

        await AddReferenceAsync(solutionRoot, bllDir, bllName, dalDir, dalName, cancellationToken);
        await AddReferenceAsync(solutionRoot, apiDir, apiName, bllDir, bllName, cancellationToken);
        await AddReferenceAsync(solutionRoot, testsDalDir, testsDalName, dalDir, dalName, cancellationToken);
        await AddReferenceAsync(solutionRoot, testsBllDir, testsBllName, bllDir, bllName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, dalDir, dalName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, bllDir, bllName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, apiDir, apiName, cancellationToken);

        await AddPackageAsync(solutionRoot, dalDir, dalName, "Microsoft.EntityFrameworkCore", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, dalDir, dalName, "Microsoft.EntityFrameworkCore.SqlServer", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, dalDir, dalName, "Microsoft.EntityFrameworkCore.Design", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, testsDalDir, testsDalName, "FluentAssertions", PackageVersions.FluentAssertions, cancellationToken);
        await AddPackageAsync(solutionRoot, testsBllDir, testsBllName, "FluentAssertions", PackageVersions.FluentAssertions, cancellationToken);
        await AddPackageAsync(solutionRoot, testsBllDir, testsBllName, "Moq", PackageVersions.Moq, cancellationToken);
        await AddPackageAsync(solutionRoot, archTestsDir, archTestsName, "NetArchTest.Rules", PackageVersions.NetArchTestRules, cancellationToken);
        await AddPackageAsync(solutionRoot, archTestsDir, archTestsName, "FluentAssertions", PackageVersions.FluentAssertions, cancellationToken);

        var nameModel = new { Name = solutionName };

        WriteFile(Path.Combine(solutionRoot, dalDir, "AppDbContext.cs"), _templateEngine.Render("Layered/AppDbContext.sbn", nameModel));
        WriteFile(Path.Combine(solutionRoot, dalDir, "IRepository.cs"), _templateEngine.Render("Layered/IRepository.sbn", nameModel));
        WriteFile(Path.Combine(solutionRoot, dalDir, "Repository.cs"), _templateEngine.Render("Layered/Repository.sbn", nameModel));
        WriteFile(Path.Combine(solutionRoot, dalDir, "IUnitOfWork.cs"), _templateEngine.Render("Layered/IUnitOfWork.sbn", nameModel));
        WriteFile(Path.Combine(solutionRoot, dalDir, "UnitOfWork.cs"), _templateEngine.Render("Layered/UnitOfWork.sbn", nameModel));
        WriteFile(
            Path.Combine(solutionRoot, dalDir, "DalAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = dalName, ClassName = "DalAssemblyReference" }));

        WriteFile(Path.Combine(solutionRoot, bllDir, "NotFoundException.cs"), _templateEngine.Render("Layered/NotFoundException.sbn", nameModel));
        WriteFile(
            Path.Combine(solutionRoot, bllDir, "BllAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = bllName, ClassName = "BllAssemblyReference" }));

        WriteFile(
            Path.Combine(solutionRoot, apiDir, "ApiAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = apiName, ClassName = "ApiAssemblyReference" }));
        WriteFile(Path.Combine(solutionRoot, apiDir, "Program.cs"), _templateEngine.Render("Layered/Program.sbn", nameModel));
        WriteFile(Path.Combine(solutionRoot, apiDir, "appsettings.json"), _templateEngine.Render("Layered/appsettings.sbn", nameModel));

        WriteFile(Path.Combine(solutionRoot, archTestsDir, "LayeredArchitectureTests.cs"), _templateEngine.Render("Layered/ArchitectureTests.sbn", nameModel));

        var config = new ToolConfig
        {
            Architecture = ArchitectureType.Layered.ToConfigString(),
            SolutionName = solutionName,
            DbContextProject = ToForwardSlashes(CsprojPath(dalDir, dalName)),
            DbContextTypeName = $"{dalName}.AppDbContext",
            Projects = new Dictionary<string, string>
            {
                [LayerNames.Dal] = ToForwardSlashes(CsprojPath(dalDir, dalName)),
                [LayerNames.Bll] = ToForwardSlashes(CsprojPath(bllDir, bllName)),
                [LayerNames.Api] = ToForwardSlashes(CsprojPath(apiDir, apiName)),
                [LayerNames.TestsDal] = ToForwardSlashes(CsprojPath(testsDalDir, testsDalName)),
                [LayerNames.TestsBll] = ToForwardSlashes(CsprojPath(testsBllDir, testsBllName)),
                [LayerNames.ArchitectureTests] = ToForwardSlashes(CsprojPath(archTestsDir, archTestsName)),
            },
        };

        File.WriteAllText(
            Path.Combine(solutionRoot, ToolConfig.FileName),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        await _dotnetCli.RunAsync(solutionRoot, ["restore"], cancellationToken);
    }

    private Task CreateProjectAsync(string solutionRoot, string template, string projectName, string relativeDir, CancellationToken cancellationToken) =>
        _dotnetCli.RunAsync(solutionRoot, ["new", template, "-n", projectName, "-o", relativeDir, "-f", "net8.0", "--no-restore"], cancellationToken);

    private Task AddReferenceAsync(string solutionRoot, string fromDir, string fromName, string toDir, string toName, CancellationToken cancellationToken) =>
        _dotnetCli.RunAsync(solutionRoot, ["add", CsprojPath(fromDir, fromName), "reference", CsprojPath(toDir, toName)], cancellationToken);

    private Task AddPackageAsync(string solutionRoot, string projectDir, string projectName, string packageId, string version, CancellationToken cancellationToken) =>
        _dotnetCli.RunAsync(solutionRoot, ["add", CsprojPath(projectDir, projectName), "package", packageId, "--version", version, "--no-restore"], cancellationToken);

    private static string CsprojPath(string dir, string projectName) => Path.Combine(dir, $"{projectName}.csproj");

    private static string ToForwardSlashes(string path) => path.Replace('\\', '/');

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
