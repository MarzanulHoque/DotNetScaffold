using System.CommandLine;
using DotNetScaffold.Cli.Commands;
using DotNetScaffold.Generation;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;

ISolutionScaffolder scaffolder = new DispatchingSolutionScaffolder(
    layered: new LayeredSolutionScaffolder(new DotnetCliRunner(), new ScribanTemplateEngine()),
    cleanArchitecture: new NotImplementedSolutionScaffolder());
ICrudGenerator generator = new NotImplementedCrudGenerator();

var rootCommand = new RootCommand("DotNetScaffold - scaffold backend solutions and generate CRUD from an EF Core DbContext.");
rootCommand.Subcommands.Add(NewCommandFactory.Create(scaffolder));
rootCommand.Subcommands.Add(GenerateCommandFactory.Create(generator));

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
