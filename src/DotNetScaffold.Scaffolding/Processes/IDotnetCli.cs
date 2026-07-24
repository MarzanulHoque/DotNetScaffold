namespace DotNetScaffold.Scaffolding.Processes;

/// <summary>Runs `dotnet` CLI subcommands (new/sln/add) so project/solution file plumbing is always
/// produced by the trusted SDK tooling rather than hand-rolled csproj/sln XML.</summary>
public interface IDotnetCli
{
    Task RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
