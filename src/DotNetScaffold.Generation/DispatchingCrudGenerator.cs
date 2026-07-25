using System.Text.Json;
using DotNetScaffold.Abstractions;

namespace DotNetScaffold.Generation;

/// <summary>
/// Entry point for `generate`: reads <c>.yourtool.json</c> from the solution root once, then routes to the
/// <see cref="IArchitectureCrudGenerator"/> implementation for the recorded <see cref="ArchitectureType"/>
/// -- mirrors <see cref="DotNetScaffold.Scaffolding.DispatchingSolutionScaffolder"/>'s pattern for `new`.
/// </summary>
public sealed class DispatchingCrudGenerator : ICrudGenerator
{
    private readonly IArchitectureCrudGenerator _layered;
    private readonly IArchitectureCrudGenerator _cleanArchitecture;

    public DispatchingCrudGenerator(IArchitectureCrudGenerator layered, IArchitectureCrudGenerator cleanArchitecture)
    {
        _layered = layered;
        _cleanArchitecture = cleanArchitecture;
    }

    public async Task GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(request.SolutionRoot, ToolConfig.FileName);
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException(
                $"'{ToolConfig.FileName}' was not found in '{request.SolutionRoot}'. " +
                "Run 'generate' from the root of a solution scaffolded by 'new'.");
        }

        var config = JsonSerializer.Deserialize<ToolConfig>(File.ReadAllText(configPath))
            ?? throw new InvalidOperationException($"'{ToolConfig.FileName}' is empty or invalid.");

        var generator = config.ArchitectureType switch
        {
            ArchitectureType.Layered => _layered,
            ArchitectureType.CleanArchitecture => _cleanArchitecture,
            _ => throw new ArgumentOutOfRangeException(nameof(config), config.ArchitectureType, "Unknown architecture type."),
        };

        await generator.GenerateAsync(request, config, cancellationToken);
    }
}
