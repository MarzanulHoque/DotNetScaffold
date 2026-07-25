using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation;

/// <summary>Implements `generate` for one specific <see cref="ArchitectureType"/>, given the already-parsed
/// <see cref="ToolConfig"/> (see <see cref="DispatchingCrudGenerator"/>, which reads it once and dispatches).</summary>
public interface IArchitectureCrudGenerator
{
    Task GenerateAsync(GenerateRequest request, ToolConfig config, CancellationToken cancellationToken = default);
}
