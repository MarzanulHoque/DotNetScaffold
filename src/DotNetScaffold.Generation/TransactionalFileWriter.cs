namespace DotNetScaffold.Generation;

/// <summary>
/// Writes one entity's whole generated file set as a single transaction: SYSTEM-DESIGN.md §5.2 step 4 /
/// §8 "Reliability" ("must not corrupt or partially overwrite files on failure ... transactional per
/// entity"). Each file's content is first written to a per-file temp path next to its final path (so a
/// process crash mid-write never leaves a half-written file at the final path), then every temp file is
/// moved into place one at a time. If any temp-write or move fails, every final path already touched by
/// *this* call is rolled back to its exact pre-call state -- restored from a snapshot if it existed
/// before, deleted if it didn't -- before the exception is rethrown. Files untouched by this call
/// (including other entities' already-completed files in the same `--all` run, since each entity gets its
/// own call) are never touched.
/// </summary>
public static class TransactionalFileWriter
{
    public static void WriteAll(IReadOnlyList<(string Path, string Content)> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        var originalContents = files.ToDictionary(f => f.Path, f => File.Exists(f.Path) ? File.ReadAllText(f.Path) : null);
        var tempFiles = new List<(string TempPath, string FinalPath)>();

        try
        {
            foreach (var (path, content) in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tempPath = $"{path}.generate-tmp-{Guid.NewGuid():N}";
                File.WriteAllText(tempPath, content);
                tempFiles.Add((tempPath, path));
            }

            var movedFinalPaths = new List<string>();
            try
            {
                foreach (var (tempPath, finalPath) in tempFiles)
                {
                    File.Move(tempPath, finalPath, overwrite: true);
                    movedFinalPaths.Add(finalPath);
                }
            }
            catch
            {
                foreach (var finalPath in movedFinalPaths)
                {
                    RestoreOriginal(finalPath, originalContents[finalPath]);
                }

                throw;
            }
        }
        finally
        {
            foreach (var (tempPath, _) in tempFiles)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }

    private static void RestoreOriginal(string path, string? originalContent)
    {
        if (originalContent is null)
        {
            File.Delete(path);
        }
        else
        {
            File.WriteAllText(path, originalContent);
        }
    }
}
