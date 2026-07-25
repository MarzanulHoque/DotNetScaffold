namespace DotNetScaffold.Generation.Layered;

/// <summary>
/// A generated `{Entity}Service` must be registered in the API project's DI container before it can be
/// resolved by its controller -- ASP.NET Core's container never auto-resolves an unregistered concrete
/// type. `new` (M1) already generated `Program.cs` with `IUnitOfWork` registered; `generate` (M5) needs to
/// add one `AddScoped&lt;{Entity}Service&gt;()` line per entity into that *existing*, hand-owned-looking
/// file, idempotently (re-running `generate` must not duplicate the line).
/// </summary>
public static class ProgramCsRegistrar
{
    private const string UnitOfWorkRegistrationLine = "builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();";

    public static void EnsureServiceRegistered(string programCsPath, string bllNamespace, string entityName)
    {
        if (!File.Exists(programCsPath))
        {
            throw new InvalidOperationException($"'{programCsPath}' was not found -- expected Program.cs from a layered scaffold.");
        }

        var text = File.ReadAllText(programCsPath);
        var registrationLine = $"builder.Services.AddScoped<{entityName}Service>();";

        if (text.Contains(registrationLine, StringComparison.Ordinal))
        {
            return;
        }

        var usingLine = $"using {bllNamespace};";
        if (!text.Contains(usingLine, StringComparison.Ordinal))
        {
            text = usingLine + Environment.NewLine + text;
        }

        var unitOfWorkIndex = text.IndexOf(UnitOfWorkRegistrationLine, StringComparison.Ordinal);
        if (unitOfWorkIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not find the expected IUnitOfWork registration line in '{programCsPath}' to anchor the new registration.");
        }

        var insertAt = unitOfWorkIndex + UnitOfWorkRegistrationLine.Length;
        text = text.Insert(insertAt, Environment.NewLine + registrationLine);

        File.WriteAllText(programCsPath, text);
    }
}
