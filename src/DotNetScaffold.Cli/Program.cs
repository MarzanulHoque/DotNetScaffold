using System.CommandLine;
using DotNetScaffold.Cli.Commands;
using DotNetScaffold.Generation;
using DotNetScaffold.Generation.Crud;
using DotNetScaffold.Generation.Dtos;
using DotNetScaffold.Generation.Layered;
using DotNetScaffold.Metadata;
using DotNetScaffold.Scaffolding;
using DotNetScaffold.Scaffolding.Processes;
using DotNetScaffold.Templating;

ISolutionScaffolder scaffolder = new DispatchingSolutionScaffolder(
    layered: new LayeredSolutionScaffolder(new DotnetCliRunner(), new ScribanTemplateEngine()),
    cleanArchitecture: new CleanArchitectureSolutionScaffolder(new DotnetCliRunner(), new ScribanTemplateEngine()));

var templateEngine = new ScribanTemplateEngine();
var modelReader = new EfModelReader(new PluginAssemblyLoader());
ICrudGenerator generator = new DispatchingCrudGenerator(
    layered: new LayeredCrudGenerator(
        new TargetAssemblyLocator(),
        modelReader,
        new DtoGenerator(templateEngine, new EntityDtoViewModelBuilder()),
        new EntityCrudViewModelBuilder(),
        templateEngine),
    cleanArchitecture: new NotImplementedArchitectureCrudGenerator());

var rootCommand = new RootCommand("DotNetScaffold - scaffold backend solutions and generate CRUD from an EF Core DbContext.");
rootCommand.Subcommands.Add(NewCommandFactory.Create(scaffolder));
rootCommand.Subcommands.Add(GenerateCommandFactory.Create(generator));

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
