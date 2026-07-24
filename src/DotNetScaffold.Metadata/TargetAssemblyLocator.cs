namespace DotNetScaffold.Metadata;

public sealed class TargetAssemblyLocator : ITargetAssemblyLocator
{
    public string FindBuiltAssemblyPath(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Project file not found: '{projectPath}'.");
        }

        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
        var binDirectory = Path.Combine(projectDirectory, "bin");

        if (!Directory.Exists(binDirectory))
        {
            throw new InvalidOperationException(BuildNotBuiltMessage(projectName, binDirectory));
        }

        // A project can have multiple build outputs (Debug/Release, multiple target frameworks); take
        // whichever was written most recently -- the last build the user actually ran is the one they want read.
        var builtAssemblyPath = Directory
            .EnumerateFiles(binDirectory, $"{projectName}.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return builtAssemblyPath
            ?? throw new InvalidOperationException(BuildNotBuiltMessage(projectName, binDirectory));
    }

    private static string BuildNotBuiltMessage(string projectName, string binDirectory) =>
        $"Could not find a built '{projectName}.dll' under '{binDirectory}'. " +
        "Run 'dotnet build' on the project containing your DbContext before running 'generate'.";
}
