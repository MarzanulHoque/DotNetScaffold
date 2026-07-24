namespace DotNetScaffold.Scaffolding;

/// <summary>
/// Placeholder wired up in M0 so the CLI command surface is stable; replaced with a real
/// implementation per architecture in M1 (layered) and M2 (clean architecture).
/// </summary>
public sealed class NotImplementedSolutionScaffolder : ISolutionScaffolder
{
    public Task ScaffoldAsync(ScaffoldRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(
            $"Scaffolding for '{request.Type}' is not implemented yet (planned for a later milestone).");
}
