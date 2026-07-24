using System.CommandLine;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding;

namespace DotNetScaffold.Cli.Commands;

public static class NewCommandFactory
{
    public static Command Create(ISolutionScaffolder scaffolder)
    {
        var typeOption = new Option<string>("--type")
        {
            Description = "Architecture style: 'layered' or 'cleanarchitecture'.",
            Required = true,
        };

        var nameOption = new Option<string>("--name")
        {
            Description = "Solution/root namespace name, e.g. 'MyApp'.",
            Required = true,
        };

        var outputOption = new Option<string>("--output")
        {
            Description = "Directory to scaffold into. Defaults to the current directory.",
        };

        var command = new Command("new", "Scaffold a new solution matching one of the supported architectures.");
        command.Options.Add(typeOption);
        command.Options.Add(nameOption);
        command.Options.Add(outputOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var typeValue = parseResult.GetValue(typeOption)!;
            var name = parseResult.GetValue(nameOption)!;
            var output = parseResult.GetValue(outputOption) ?? Directory.GetCurrentDirectory();

            if (!ArchitectureTypeParser.TryParse(typeValue, out var architectureType))
            {
                Console.Error.WriteLine(
                    $"error: unknown --type '{typeValue}'. Expected 'layered' or 'cleanarchitecture'.");
                return 1;
            }

            try
            {
                await scaffolder.ScaffoldAsync(
                    new ScaffoldRequest(architectureType, name, output),
                    cancellationToken);
                Console.WriteLine($"Scaffolded '{name}' ({typeValue}) into {output}.");
                return 0;
            }
            catch (NotImplementedException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }
}
