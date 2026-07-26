using FluentAssertions;

namespace DotNetScaffold.Generation.Tests;

/// <summary>
/// Proves the M7 reliability guarantee (SYSTEM-DESIGN.md §8 "Reliability") by actually breaking a write
/// partway through an entity's file set, not just asserting the happy path -- per
/// [[feedback-verify-dont-just-build]]: a rollback mechanism that never gets exercised by a real failure
/// proves nothing about whether it actually rolls back.
/// </summary>
public class TransactionalFileWriterTests : IDisposable
{
    private readonly string _directory;

    public TransactionalFileWriterTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "dotnetscaffold-txwriter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void WriteAll_WhenEveryWriteSucceeds_WritesAllFilesWithTheirContent()
    {
        var fileA = Path.Combine(_directory, "A.cs");
        var fileB = Path.Combine(_directory, "Nested", "B.cs");

        TransactionalFileWriter.WriteAll([(fileA, "content-a"), (fileB, "content-b")]);

        File.ReadAllText(fileA).Should().Be("content-a");
        File.ReadAllText(fileB).Should().Be("content-b");
    }

    [Fact]
    public void WriteAll_WhenALaterFileFailsToMove_RollsBackEarlierFilesAndLeavesLaterOnesUntouched()
    {
        var fileA = Path.Combine(_directory, "A.cs");
        var fileB = Path.Combine(_directory, "B.cs");
        var fileC = Path.Combine(_directory, "C.cs");

        File.WriteAllText(fileA, "original-a");
        Directory.CreateDirectory(fileB); // sabotage: a directory sits where B.cs's final path should be, so its move fails

        var act = () => TransactionalFileWriter.WriteAll(
        [
            (fileA, "new-a"),
            (fileB, "new-b"),
            (fileC, "new-c"),
        ]);

        act.Should().Throw<Exception>();

        File.ReadAllText(fileA).Should().Be("original-a", "the earlier, already-moved file must be rolled back to its pre-call content");
        Directory.Exists(fileB).Should().BeTrue("the sabotaged path must be left exactly as it was, not partially replaced");
        File.Exists(fileC).Should().BeFalse("a file after the failure point must never be created");

        Directory.GetFiles(_directory, "*.generate-tmp-*", SearchOption.AllDirectories)
            .Should().BeEmpty("temp files must always be cleaned up, whether the call succeeds or fails");
    }

    [Fact]
    public void WriteAll_WhenAFileDidNotExistBeforeAndALaterWriteFails_DeletesItOnRollback()
    {
        var fileA = Path.Combine(_directory, "New.cs");
        var fileB = Path.Combine(_directory, "Bad.cs");

        Directory.CreateDirectory(fileB); // sabotage

        var act = () => TransactionalFileWriter.WriteAll([(fileA, "new-a"), (fileB, "new-b")]);

        act.Should().Throw<Exception>();
        File.Exists(fileA).Should().BeFalse("a file with no prior content must be deleted on rollback, not left behind");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
