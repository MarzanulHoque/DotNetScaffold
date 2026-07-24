using System.CommandLine;
using System.Text.RegularExpressions;
using DotNetScaffold.Abstractions;
using DotNetScaffold.Scaffolding;

namespace DotNetScaffold.Cli.Commands;

public static partial class NewCommandFactory
{
    // A dotted identifier, e.g. "MyApp" or "MyCompany.MyApp" — becomes the generated root namespace, so
    // it must be valid C#. This also rules out path separators and "..", which is what --name/--output
    // get combined into a filesystem path with; without this check a --name like "../../etc" could steer
    // Path.Combine(output, name) outside the intended output directory.
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex SolutionNamePattern();

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

            if (!SolutionNamePattern().IsMatch(name))
            {
                Console.Error.WriteLine(
                    $"error: invalid --name '{name}'. It becomes the generated root namespace, so it must " +
                    "look like a C# identifier (letters, digits, underscores, dot-separated), e.g. 'MyApp' " +
                    "or 'MyCompany.MyApp' — no path separators.");
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
            catch (Exception ex) when (ex is NotImplementedException or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
        });

        return command;
    }
}
