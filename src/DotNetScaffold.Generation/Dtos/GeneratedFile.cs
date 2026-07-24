namespace DotNetScaffold.Generation.Dtos;

/// <summary>A generated file's name and content -- deliberately not a disk path. Where a file lands
/// (BLL vs. Application, exact folder) is an architecture-specific decision left to the M5/M6 callers
/// that orchestrate the full per-entity generation; this project only produces content.</summary>
public sealed record GeneratedFile(string FileName, string Content);
