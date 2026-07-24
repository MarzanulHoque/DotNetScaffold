# DotNetScaffold

A .NET global CLI tool (distributed as a `dotnet tool` via NuGet) that scaffolds new backend solutions
and generates CRUD code (DTOs, services/repositories, controllers, and tests) from an existing EF Core
`DbContext`. It supports two architectural styles:

- **Layered monolith** (`--type layered`) — repository-pattern DAL/BLL/API projects.
- **Clean Architecture** (`--type cleanarchitecture`) — Domain/Application/Infrastructure/Web projects.

Every scaffolded solution also gets an `ArchitectureTests` project (using
[NetArchTest.Rules](https://github.com/BenMorris/NetArchTest)) that fails `dotnet test` if a later code
change violates the chosen architecture's dependency-direction rules (e.g. DAL referencing BLL, or
Application referencing Infrastructure).

> **Status:** v1 under active development. See milestones below for what's implemented so far.

## Requirements

- .NET 8 SDK (this repo targets `net8.0`; a newer SDK side-by-side is fine)
- A target project using ASP.NET Core 8 + EF Core 8+ (for `generate`, the target project must build
  successfully — metadata is read from the built assembly, not parsed from source)

## Usage

```bash
# Scaffold a new solution
dnscaffold new --type layered --name MyApp [--output ./path]
dnscaffold new --type cleanarchitecture --name MyApp [--output ./path]

# Generate CRUD for one entity, or all entities, from the DbContext
dnscaffold generate --entity Author
dnscaffold generate --all
dnscaffold generate --entity Author --force   # overwrite existing generated files
```

`new` writes a `.yourtool.json` config file at the solution root recording the architecture type and
layer project paths, so `generate` doesn't need `--type` repeated.

## Repository layout

```
src/
  DotNetScaffold.Abstractions/   Shared models: ArchitectureType, ToolConfig, EntityMetadata, etc.
  DotNetScaffold.Metadata/       Loads the target project's built assembly and reads EF Core's IModel.
  DotNetScaffold.Templating/     Scriban-based template engine wrapper.
  DotNetScaffold.Scaffolding/    `new` — solution/project scaffolding per architecture.
  DotNetScaffold.Generation/     `generate` — per-entity CRUD generation, idempotency, transactional writes.
  DotNetScaffold.Cli/            System.CommandLine command tree and composition root.
templates/                      Externalized .sbn (Scriban) code templates.
tests/                          Unit + end-to-end integration test projects, one per src/ project plus
                                 a Cli.IntegrationTests project that scaffolds + generates + builds real output.
samples/                        Sample EF Core DbContext fixtures used by tests and manual trial runs.
```

## Design decisions (v1)

- **Idempotency**: generated classes that carry custom logic (currently: services) are split into a
  `{Entity}Service.Generated.cs` partial (always rewritten under `--force`) and a `{Entity}Service.cs`
  partial (created once, never touched again on regeneration).
- **Error handling**: services throw typed exceptions (e.g. `NotFoundException`); no `Result<T>` wrapper.
- **Repository granularity** (layered template): one generic `IRepository<T>`/`Repository<T>`, plus a
  thin per-entity `I{Entity}Repository : IRepository<Entity>` marker interface.
- **Test seeding**: generated tests include a minimal auto-seed (one valid entity instance, plus one
  related child/parent for relationships) so they pass out of the box.
- **Many-to-many relationships**: detected and skipped with a logged warning, never an error (out of
  scope for v1, per the SRS).

## Build & test

```bash
dotnet build
dotnet test
```

## Milestones

- [x] M0 — Repo + CLI skeleton (`new`/`generate` commands, `--help`, validation)
- [ ] M1 — `new --type layered` full scaffolding + ArchitectureTests
- [ ] M2 — `new --type cleanarchitecture` full scaffolding + ArchitectureTests
- [ ] M3 — EF Core metadata reader
- [ ] M4 — Scriban templating engine + DTO generation
- [ ] M5 — Layered `generate` output
- [ ] M6 — Clean-architecture `generate` output
- [ ] M7 — `--force` / transactional writer / many-to-many skip+warn
- [ ] M8 — End-to-end integration tests
- [ ] M9 — Pack as a `dotnet tool`
