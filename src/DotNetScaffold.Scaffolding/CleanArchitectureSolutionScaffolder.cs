using System.Text.Json;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;

namespace DotNetScaffold.Scaffolding;

/// <summary>
/// Scaffolds the Clean Architecture template (Domain/Application/Infrastructure/Web): SRS 3.1.2.
/// Mirrors <see cref="LayeredSolutionScaffolder"/>'s approach — `dotnet` CLI for project/solution
/// plumbing, Scriban templates for the interesting source files — but without a repository layer:
/// Infrastructure injects <c>AppDbContext</c> directly, per the SRS.
/// </summary>
public sealed class CleanArchitectureSolutionScaffolder : ISolutionScaffolder
{
    private const string SrcDir = "src";
    private const string TestsDir = "tests";

    private readonly IDotnetCli _dotnetCli;
    private readonly ITemplateEngine _templateEngine;

    public CleanArchitectureSolutionScaffolder(IDotnetCli dotnetCli, ITemplateEngine templateEngine)
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

        var domainName = $"{solutionName}.Domain";
        var applicationName = $"{solutionName}.Application";
        var infrastructureName = $"{solutionName}.Infrastructure";
        var webName = $"{solutionName}.Web";
        var applicationTestsName = $"{solutionName}.Application.Tests";
        var infrastructureTestsName = $"{solutionName}.Infrastructure.Tests";
        var archTestsName = $"{solutionName}.ArchitectureTests";

        var domainDir = Path.Combine(SrcDir, domainName);
        var applicationDir = Path.Combine(SrcDir, applicationName);
        var infrastructureDir = Path.Combine(SrcDir, infrastructureName);
        var webDir = Path.Combine(SrcDir, webName);
        var applicationTestsDir = Path.Combine(TestsDir, applicationTestsName);
        var infrastructureTestsDir = Path.Combine(TestsDir, infrastructureTestsName);
        var archTestsDir = Path.Combine(TestsDir, archTestsName);

        await _dotnetCli.RunAsync(solutionRoot, ["new", "sln", "-n", solutionName, "-f", "sln"], cancellationToken);

        await CreateProjectAsync(solutionRoot, "classlib", domainName, domainDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "classlib", applicationName, applicationDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "classlib", infrastructureName, infrastructureDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "web", webName, webDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "xunit", applicationTestsName, applicationTestsDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "xunit", infrastructureTestsName, infrastructureTestsDir, cancellationToken);
        await CreateProjectAsync(solutionRoot, "xunit", archTestsName, archTestsDir, cancellationToken);

        DeleteIfExists(Path.Combine(solutionRoot, domainDir, "Class1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, applicationDir, "Class1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, infrastructureDir, "Class1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, applicationTestsDir, "UnitTest1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, infrastructureTestsDir, "UnitTest1.cs"));
        DeleteIfExists(Path.Combine(solutionRoot, archTestsDir, "UnitTest1.cs"));

        await _dotnetCli.RunAsync(
            solutionRoot,
            [
                "sln", "add",
                CsprojPath(domainDir, domainName), CsprojPath(applicationDir, applicationName),
                CsprojPath(infrastructureDir, infrastructureName), CsprojPath(webDir, webName),
                CsprojPath(applicationTestsDir, applicationTestsName), CsprojPath(infrastructureTestsDir, infrastructureTestsName),
                CsprojPath(archTestsDir, archTestsName),
            ],
            cancellationToken);

        await AddReferenceAsync(solutionRoot, applicationDir, applicationName, domainDir, domainName, cancellationToken);
        await AddReferenceAsync(solutionRoot, infrastructureDir, infrastructureName, domainDir, domainName, cancellationToken);
        await AddReferenceAsync(solutionRoot, infrastructureDir, infrastructureName, applicationDir, applicationName, cancellationToken);
        await AddReferenceAsync(solutionRoot, webDir, webName, applicationDir, applicationName, cancellationToken);
        await AddReferenceAsync(solutionRoot, webDir, webName, infrastructureDir, infrastructureName, cancellationToken);
        await AddReferenceAsync(solutionRoot, applicationTestsDir, applicationTestsName, applicationDir, applicationName, cancellationToken);
        await AddReferenceAsync(solutionRoot, infrastructureTestsDir, infrastructureTestsName, infrastructureDir, infrastructureName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, domainDir, domainName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, applicationDir, applicationName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, infrastructureDir, infrastructureName, cancellationToken);
        await AddReferenceAsync(solutionRoot, archTestsDir, archTestsName, webDir, webName, cancellationToken);

        await AddPackageAsync(solutionRoot, infrastructureDir, infrastructureName, "Microsoft.EntityFrameworkCore", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, infrastructureDir, infrastructureName, "Microsoft.EntityFrameworkCore.SqlServer", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, infrastructureDir, infrastructureName, "Microsoft.EntityFrameworkCore.Design", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, applicationTestsDir, applicationTestsName, "FluentAssertions", PackageVersions.FluentAssertions, cancellationToken);
        await AddPackageAsync(solutionRoot, infrastructureTestsDir, infrastructureTestsName, "FluentAssertions", PackageVersions.FluentAssertions, cancellationToken);
        await AddPackageAsync(solutionRoot, infrastructureTestsDir, infrastructureTestsName, "Microsoft.EntityFrameworkCore.InMemory", PackageVersions.EntityFrameworkCore, cancellationToken);
        await AddPackageAsync(solutionRoot, archTestsDir, archTestsName, "NetArchTest.Rules", PackageVersions.NetArchTestRules, cancellationToken);
        await AddPackageAsync(solutionRoot, archTestsDir, archTestsName, "FluentAssertions", PackageVersions.FluentAssertions, cancellationToken);

        var nameModel = new { Name = solutionName };

        WriteFile(
            Path.Combine(solutionRoot, domainDir, "NotFoundException.cs"),
            _templateEngine.Render("Common/NotFoundException.sbn", new { Namespace = domainName }));
        WriteFile(
            Path.Combine(solutionRoot, domainDir, "DomainAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = domainName, ClassName = "DomainAssemblyReference" }));

        WriteFile(
            Path.Combine(solutionRoot, applicationDir, "ApplicationAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = applicationName, ClassName = "ApplicationAssemblyReference" }));

        WriteFile(
            Path.Combine(solutionRoot, infrastructureDir, "AppDbContext.cs"),
            _templateEngine.Render("Common/AppDbContext.sbn", new { Namespace = infrastructureName }));
        WriteFile(
            Path.Combine(solutionRoot, infrastructureDir, "InfrastructureAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = infrastructureName, ClassName = "InfrastructureAssemblyReference" }));

        WriteFile(
            Path.Combine(solutionRoot, webDir, "WebAssemblyReference.cs"),
            _templateEngine.Render("Common/AssemblyReference.sbn", new { Namespace = webName, ClassName = "WebAssemblyReference" }));
        WriteFile(Path.Combine(solutionRoot, webDir, "Program.cs"), _templateEngine.Render("CleanArchitecture/Program.sbn", nameModel));
        WriteFile(Path.Combine(solutionRoot, webDir, "appsettings.json"), _templateEngine.Render("Common/appsettings.sbn", nameModel));

        WriteFile(
            Path.Combine(solutionRoot, archTestsDir, "CleanArchitectureTests.cs"),
            _templateEngine.Render("CleanArchitecture/ArchitectureTests.sbn", nameModel));

        var config = new ToolConfig
        {
            Architecture = ArchitectureType.CleanArchitecture.ToConfigString(),
            SolutionName = solutionName,
            DbContextProject = ToForwardSlashes(CsprojPath(infrastructureDir, infrastructureName)),
            DbContextTypeName = $"{infrastructureName}.AppDbContext",
            Projects = new Dictionary<string, string>
            {
                [LayerNames.Domain] = ToForwardSlashes(CsprojPath(domainDir, domainName)),
                [LayerNames.Application] = ToForwardSlashes(CsprojPath(applicationDir, applicationName)),
                [LayerNames.Infrastructure] = ToForwardSlashes(CsprojPath(infrastructureDir, infrastructureName)),
                [LayerNames.Web] = ToForwardSlashes(CsprojPath(webDir, webName)),
                [LayerNames.ApplicationTests] = ToForwardSlashes(CsprojPath(applicationTestsDir, applicationTestsName)),
                [LayerNames.InfrastructureTests] = ToForwardSlashes(CsprojPath(infrastructureTestsDir, infrastructureTestsName)),
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
