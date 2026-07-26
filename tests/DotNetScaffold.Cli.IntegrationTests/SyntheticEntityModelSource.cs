namespace DotNetScaffold.Cli.IntegrationTests;

/// <summary>
/// Synthetic, relationship-free entities used only to measure the SRS performance NFR (SYSTEM-DESIGN.md
/// §9: "15-20 entity DbContext generates in less than 30 seconds"). Deliberately flat -- no navigations --
/// so the measurement isolates per-entity codegen throughput rather than relationship-flattening cost,
/// which is already covered by the SampleBlog-based end-to-end tests elsewhere.
/// </summary>
internal static class SyntheticEntityModelSource
{
    internal static IReadOnlyList<string> EntityNames(int count) =>
        Enumerable.Range(1, count).Select(i => $"Entity{i:D2}").ToList();

    internal static string Entity(string @namespace, string entityName) => $$"""
        namespace {{@namespace}};

        public class {{entityName}}
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
        }
        """;

    internal static string AppDbContext(string @namespace, string entityNamespace, IReadOnlyList<string> entityNames)
    {
        var usingLine = @namespace == entityNamespace ? string.Empty : $"using {entityNamespace};{Environment.NewLine}";
        var dbSets = string.Join(
            Environment.NewLine,
            entityNames.Select(name => $"    public DbSet<{name}> {name}s => Set<{name}>();"));

        return $$"""
            using Microsoft.EntityFrameworkCore;
            {{usingLine}}
            namespace {{@namespace}};

            public class AppDbContext : DbContext
            {
                public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
                {
                }

            {{dbSets}}
            }
            """;
    }
}
