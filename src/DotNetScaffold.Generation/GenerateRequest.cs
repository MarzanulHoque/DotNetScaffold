namespace DotNetScaffold.Generation;

/// <summary>
/// <paramref name="EntityName"/> is null when <paramref name="All"/> is true (<c>generate --all</c>);
/// otherwise exactly one of the two selects the entity/entities to generate for.
/// </summary>
public sealed record GenerateRequest(string? EntityName, bool All, bool Force, string SolutionRoot);

public interface ICrudGenerator
{
    Task GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default);
}
