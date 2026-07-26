namespace DotNetScaffold.Generation.CleanArchitecture;

/// <summary>
/// A generated `{Entity}Service` must be registered against its `I{Entity}Service` interface in the Web
/// project's DI container before a controller can resolve it. `new` (M2) already generated `Program.cs`
/// with `AppDbContext` registered; `generate` (M6) needs to add one
/// `AddScoped&lt;I{Entity}Service, {Entity}Service&gt;()` line per entity into that *existing*,
/// hand-owned-looking file, idempotently (re-running `generate` must not duplicate the line). Mirrors
/// <see cref="DotNetScaffold.Generation.Layered.ProgramCsRegistrar"/>.
/// </summary>
public static class ProgramCsRegistrar
{
    private const string DbContextRegistrationAnchor =
        "options.UseSqlServer(builder.Configuration.GetConnectionString(\"DefaultConnection\")));";

    public static void EnsureServiceRegistered(string programCsPath, string applicationNamespace, string infrastructureNamespace, string entityName)
    {
        if (!File.Exists(programCsPath))
        {
            throw new InvalidOperationException($"'{programCsPath}' was not found -- expected Program.cs from a Clean Architecture scaffold.");
        }

        var text = File.ReadAllText(programCsPath);
        var registrationLine = $"builder.Services.AddScoped<I{entityName}Service, {entityName}Service>();";

        if (text.Contains(registrationLine, StringComparison.Ordinal))
        {
            return;
        }

        text = EnsureUsing(text, applicationNamespace);
        text = EnsureUsing(text, infrastructureNamespace);

        var anchorIndex = text.IndexOf(DbContextRegistrationAnchor, StringComparison.Ordinal);
        if (anchorIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not find the expected AddDbContext registration in '{programCsPath}' to anchor the new registration.");
        }

        var insertAt = anchorIndex + DbContextRegistrationAnchor.Length;
        text = text.Insert(insertAt, Environment.NewLine + registrationLine);

        File.WriteAllText(programCsPath, text);
    }

    private static string EnsureUsing(string text, string ns)
    {
        var usingLine = $"using {ns};";
        return text.Contains(usingLine, StringComparison.Ordinal) ? text : usingLine + Environment.NewLine + text;
    }
}
