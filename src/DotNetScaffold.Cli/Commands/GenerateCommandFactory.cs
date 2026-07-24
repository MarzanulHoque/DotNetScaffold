using System.CommandLine;
using DotNetScaffold.Generation;

namespace DotNetScaffold.Cli.Commands;

public static class GenerateCommandFactory
{
    public static Command Create(ICrudGenerator generator)
    {
        var entityOption = new Option<string>("--entity")
        {
            Description = "Name of a single entity (as declared on the DbContext) to generate CRUD for.",
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Generate CRUD for every entity in the DbContext.",
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing generated files instead of failing when they already exist.",
        };

        var command = new Command("generate", "Generate CRUD artifacts (DTOs, service/repository, controller, tests) from the DbContext model.");
        command.Options.Add(entityOption);
        command.Options.Add(allOption);
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var entity = parseResult.GetValue(entityOption);
            var all = parseResult.GetValue(allOption);
            var force = parseResult.GetValue(forceOption);

            if (all && !string.IsNullOrWhiteSpace(entity))
            {
                Console.Error.WriteLine("error: specify either --entity <Name> or --all, not both.");
                return 1;
            }

            if (!all && string.IsNullOrWhiteSpace(entity))
            {
                Console.Error.WriteLine("error: specify either --entity <Name> or --all.");
                return 1;
            }

            try
            {
                await generator.GenerateAsync(
                    new GenerateRequest(entity, all, force, Directory.GetCurrentDirectory()),
                    cancellationToken);
                Console.WriteLine(all ? "Generated CRUD for all entities." : $"Generated CRUD for '{entity}'.");
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
