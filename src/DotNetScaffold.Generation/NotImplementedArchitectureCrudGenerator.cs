using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation;

/// <summary>Placeholder for the architecture(s) M5+ hasn't implemented yet -- currently Clean
/// Architecture, planned for M6.</summary>
public sealed class NotImplementedArchitectureCrudGenerator : IArchitectureCrudGenerator
{
    public Task GenerateAsync(GenerateRequest request, ToolConfig config, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            $"CRUD generation for the '{config.Architecture}' architecture is not implemented yet (planned for a later milestone).");
}
