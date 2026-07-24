using System.Text.Json.Serialization;

namespace DotNetScaffold.Abstractions;

/// <summary>Well-known keys used in <see cref="ToolConfig.Projects"/>, shared across both architecture templates.</summary>
public static class LayerNames
{
    public const string Dal = "dal";
    public const string Bll = "bll";
    public const string Api = "api";
    public const string TestsDal = "testsDal";
    public const string TestsBll = "testsBll";

    public const string Domain = "domain";
    public const string Application = "application";
    public const string Infrastructure = "infrastructure";
    public const string Web = "web";
    public const string ApplicationTests = "applicationTests";
    public const string InfrastructureTests = "infrastructureTests";

    public const string ArchitectureTests = "architectureTests";
}

/// <summary>
/// Persisted at solution root as <c>.yourtool.json</c>. Read by <c>generate</c> so the architecture
/// type and layer project locations don't need to be re-specified per command.
/// </summary>
public sealed class ToolConfig
{
    public const string FileName = ".yourtool.json";

    [JsonPropertyName("architecture")]
    public required string Architecture { get; init; }

    [JsonPropertyName("solutionName")]
    public required string SolutionName { get; init; }

    [JsonPropertyName("dbContextProject")]
    public required string DbContextProject { get; init; }

    [JsonPropertyName("dbContextTypeName")]
    public required string DbContextTypeName { get; init; }

    /// <summary>Maps a <see cref="LayerNames"/> key to a project file path relative to the solution root.</summary>
    [JsonPropertyName("projects")]
    public required Dictionary<string, string> Projects { get; init; }

    [JsonIgnore]
    public ArchitectureType ArchitectureType =>
        ArchitectureTypeParser.TryParse(Architecture, out var type)
            ? type
            : throw new InvalidOperationException($"Unknown architecture '{Architecture}' in {FileName}.");
}
