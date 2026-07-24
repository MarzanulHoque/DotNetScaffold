using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Scaffolding;

/// <summary>Routes a scaffold request to the implementation for its <see cref="ArchitectureType"/>.</summary>
public sealed class DispatchingSolutionScaffolder : ISolutionScaffolder
{
    private readonly ISolutionScaffolder _layered;
    private readonly ISolutionScaffolder _cleanArchitecture;

    public DispatchingSolutionScaffolder(ISolutionScaffolder layered, ISolutionScaffolder cleanArchitecture)
    {
        _layered = layered;
        _cleanArchitecture = cleanArchitecture;
    }

    public Task ScaffoldAsync(ScaffoldRequest request, CancellationToken cancellationToken = default) =>
        request.Type switch
        {
            ArchitectureType.Layered => _layered.ScaffoldAsync(request, cancellationToken),
            ArchitectureType.CleanArchitecture => _cleanArchitecture.ScaffoldAsync(request, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Type, "Unknown architecture type."),
        };
}
