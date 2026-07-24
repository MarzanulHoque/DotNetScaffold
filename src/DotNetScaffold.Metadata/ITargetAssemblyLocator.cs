namespace DotNetScaffold.Metadata;

/// <summary>
/// Resolves a target project's built output assembly. `generate` reads metadata from a compiled
/// assembly, not by parsing source (SRS 3.2.1) -- this is what turns a `.csproj` path into the `.dll`
/// path that actually gets loaded.
/// </summary>
public interface ITargetAssemblyLocator
{
    /// <summary>Throws <see cref="InvalidOperationException"/> with a clear message if the project
    /// hasn't been built yet (no matching output assembly found).</summary>
    string FindBuiltAssemblyPath(string projectPath);
}
