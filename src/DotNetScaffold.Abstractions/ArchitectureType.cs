namespace DotNetScaffold.Abstractions;

public enum ArchitectureType
{
    Layered,
    CleanArchitecture,
}

public static class ArchitectureTypeParser
{
    public static bool TryParse(string value, out ArchitectureType architectureType)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "layered":
                architectureType = ArchitectureType.Layered;
                return true;
            case "cleanarchitecture":
            case "clean-architecture":
                architectureType = ArchitectureType.CleanArchitecture;
                return true;
            default:
                architectureType = default;
                return false;
        }
    }

    public static string ToConfigString(this ArchitectureType architectureType) => architectureType switch
    {
        ArchitectureType.Layered => "layered",
        ArchitectureType.CleanArchitecture => "cleanarchitecture",
        _ => throw new ArgumentOutOfRangeException(nameof(architectureType), architectureType, null),
    };
}
