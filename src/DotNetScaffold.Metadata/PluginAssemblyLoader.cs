using System.Reflection;
using System.Runtime.Loader;

namespace DotNetScaffold.Metadata;

/// <summary>A target assembly loaded into its own collectible <see cref="AssemblyLoadContext"/>. Disposing
/// unloads the context (best-effort -- actual reclamation depends on the GC, per the standard
/// collectible-ALC caveat).</summary>
public sealed class LoadedPluginAssembly : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;

    internal LoadedPluginAssembly(Assembly assembly, AssemblyLoadContext loadContext)
    {
        Assembly = assembly;
        _loadContext = loadContext;
    }

    public Assembly Assembly { get; }

    public void Dispose() => _loadContext.Unload();
}

public interface IPluginAssemblyLoader
{
    LoadedPluginAssembly Load(string assemblyPath);
}

/// <summary>
/// Loads a target project's built assembly the way .NET's own "plugin" pattern recommends: a dedicated
/// collectible <see cref="AssemblyLoadContext"/> plus an <see cref="AssemblyDependencyResolver"/> built
/// from the target's own path, so the target's own unique dependencies resolve correctly from its build
/// output folder. Shared dependencies this tool itself already references at the same version (notably
/// EF Core, since <c>DotNetScaffold.Metadata</c> references the same 8.0.29 the scaffolded templates
/// pin) are resolved by the runtime's normal default-context probing before this loader's `Resolving`
/// handler is even asked -- confirmed by a probe (not assumed): constructing the target `DbContext` and
/// casting the result to this project's own compile-time `DbContext` type succeeds, proving both sides
/// share the same loaded EF Core assembly rather than two incompatible copies.
/// </summary>
public sealed class PluginAssemblyLoader : IPluginAssemblyLoader
{
    public LoadedPluginAssembly Load(string assemblyPath)
    {
        var resolver = new AssemblyDependencyResolver(assemblyPath);
        var loadContext = new AssemblyLoadContext(
            $"DotNetScaffold-{Path.GetFileNameWithoutExtension(assemblyPath)}", isCollectible: true);

        loadContext.Resolving += (context, assemblyName) =>
        {
            var resolvedPath = resolver.ResolveAssemblyToPath(assemblyName);
            return resolvedPath is null ? null : context.LoadFromAssemblyPath(resolvedPath);
        };

        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        return new LoadedPluginAssembly(assembly, loadContext);
    }
}
