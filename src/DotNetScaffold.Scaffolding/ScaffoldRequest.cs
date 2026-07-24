using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Scaffolding;

public sealed record ScaffoldRequest(ArchitectureType Type, string SolutionName, string OutputDirectory);

public interface ISolutionScaffolder
{
    Task ScaffoldAsync(ScaffoldRequest request, CancellationToken cancellationToken = default);
}
