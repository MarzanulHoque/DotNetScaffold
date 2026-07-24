namespace DotNetScaffold.Generation;

/// <summary>
/// Placeholder wired up in M0 so the CLI command surface is stable; replaced with a real
/// implementation across M3 (metadata reading) through M7 (force/transactional writer).
/// </summary>
public sealed class NotImplementedCrudGenerator : ICrudGenerator
{
    public Task GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("CRUD generation is not implemented yet (planned for a later milestone).");
}
