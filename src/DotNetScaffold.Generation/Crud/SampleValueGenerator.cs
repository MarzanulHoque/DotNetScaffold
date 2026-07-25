namespace DotNetScaffold.Generation.Crud;

/// <summary>
/// Produces a plausible C# literal for a given CLR type name, used to build the minimal auto-seed data
/// in generated tests (one valid instance per entity/related entity) -- generated tests must compile and
/// pass out of the box, not require hand-filling before they run.
/// </summary>
internal static class SampleValueGenerator
{
    internal static string LiteralFor(string csharpTypeName) => csharpTypeName.TrimEnd('?') switch
    {
        "string" => "\"Test\"",
        "int" or "long" or "short" or "byte" => "1",
        "bool" => "true",
        "decimal" => "1m",
        "double" => "1d",
        "float" => "1f",
        "Guid" => "Guid.NewGuid()",
        "DateTime" => "DateTime.UtcNow",
        "DateTimeOffset" => "DateTimeOffset.UtcNow",
        "DateOnly" => "DateOnly.FromDateTime(DateTime.UtcNow)",
        "TimeOnly" => "TimeOnly.FromDateTime(DateTime.UtcNow)",
        "TimeSpan" => "TimeSpan.Zero",
        _ => "default",
    };
}
