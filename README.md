# DotNetScaffold

DotNetScaffold is a command-line tool that scaffolds a new ASP.NET Core backend solution for you, and
then generates the repetitive CRUD code — DTOs, services, repositories, controllers, and tests — for
each entity in your database model. Instead of hand-writing the same DTO/service/controller/test
pattern for every entity, you describe your data once (as an EF Core `DbContext`) and let the tool
produce the rest.

It supports two ways of structuring your backend:

- **Layered monolith** — a classic DAL / BLL / API split using the repository pattern.
- **Clean Architecture** — Domain / Application / Infrastructure / Web, with dependencies pointing
  inward toward Domain.

Whichever you pick, your solution also comes with a set of **architecture tests** that run alongside
your normal test suite. If you (or a teammate, or an AI assistant) later add code that breaks the
layering rules — say, the data layer starts calling into the business layer — those tests fail, so the
mistake is caught by `dotnet test` instead of slipping into a code review.

> **Status**: feature-complete for v1 (see `PROGRESS-TRACKER.md`) and packaged as a real `dotnet tool`,
> but **not yet published to NuGet.org** — install it locally from a built package as shown below.

## Requirements

- .NET 8 SDK
- Your own project should target ASP.NET Core 8 and EF Core 8+
- Before running `generate`, your project needs to build successfully — the tool reads your entity model
  from the compiled output, not by parsing source code

## Getting started

Once published to NuGet.org, you'll install it like any other global tool:

```bash
dotnet tool install -g DotNetScaffold.Tool
```

Until then, build the package from source and install it from the local output folder:

```bash
dotnet pack src/DotNetScaffold.Cli/DotNetScaffold.Cli.csproj -c Release -o ./nupkg-out
dotnet tool install -g DotNetScaffold.Tool --add-source ./nupkg-out
```

Either way, the installed command is `dnscaffold`. To pick up a change you made to the source, bump
`<Version>` in `DotNetScaffold.Cli.csproj`, re-run `dotnet pack`, then `dotnet tool update -g
DotNetScaffold.Tool --add-source ./nupkg-out`.

You can also skip packaging entirely and run straight from source:

```bash
dotnet run --project src/DotNetScaffold.Cli -- <command> [options]
```

## Scaffolding a new solution

```bash
dnscaffold new --type layered --name MyApp
# or
dnscaffold new --type cleanarchitecture --name MyApp
```

This creates a `MyApp/` folder containing a ready-to-build solution: the project set for whichever
architecture you chose, matching test projects, an `ArchitectureTests` project, and a small
`.yourtool.json` file at the solution root. That file just remembers which architecture you picked and
where things live, so you don't have to repeat `--type` on every later command — you won't normally need
to open or edit it yourself.

From here, you add your own entity classes and register them on the generated `DbContext` as you
normally would, then build the project.

## Generating CRUD for your entities

Once your `DbContext` builds successfully with your entities in place:

```bash
dnscaffold generate --entity Author      # generate CRUD for a single entity
dnscaffold generate --all                # generate CRUD for every entity in your DbContext
dnscaffold generate --entity Author --force   # regenerate, overwriting previously generated files
```

For each entity this produces: a detail DTO and a list DTO, create/update DTOs, a
repository/service layer (matching whichever architecture you scaffolded), a controller, and a test
project pre-populated with sample data so the tests pass out of the box.

If you've added your own logic to a generated service, it's safe to re-run `generate --force` later —
your hand-written code lives in its own file and is never touched by regeneration.

One-to-many, one-to-one, and self-referencing relationships are all handled. Many-to-many relationships
aren't supported yet — the tool will skip generating that relationship and print a warning rather than
failing.

## Why the architecture tests matter

Every scaffolded solution includes a test project that checks your project's actual compiled dependency
graph against the rules of the architecture you chose (e.g. your data-access project must never
reference your business-logic project). It runs as part of `dotnet test`, so it's part of your normal
build feedback loop, not an extra step you have to remember to run.

## Current limitations (v1)

- Many-to-many relationships are skipped with a warning, not generated
- Your project must build before `generate` can read its entity model
- Composite (multi-column) primary keys aren't supported yet
- CQRS/MediatR-style generation, database migrations, and frontend code generation are not in scope

## License

MIT — see [LICENSE](LICENSE).
