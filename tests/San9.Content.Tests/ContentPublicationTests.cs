using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class ContentPublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "san9-publication-" + Guid.NewGuid().ToString("N"));
    public ContentPublicationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task AbandonedBatchLeavesSharedInputsAndWorldsUnchanged()
    {
        string shared = Path.Combine(_root, "definitions.json"), world = Path.Combine(_root, "scenario.json");
        await File.WriteAllTextAsync(shared, "old definitions");
        await File.WriteAllTextAsync(world, "old world");
        using (var publication = new ContentPublication())
        {
            await publication.StageAsync(shared, new { Value = "new definitions" }, PackageFiles.JsonOptions);
            await publication.StageAsync(world, new { Value = "new world" }, PackageFiles.JsonOptions);
            publication.Retire(Path.Combine(_root, "retained.txt"));
            await File.WriteAllTextAsync(Path.Combine(_root, "retained.txt"), "keep");
            Assert.Equal("old definitions", await File.ReadAllTextAsync(shared));
        }
        Assert.Equal("old definitions", await File.ReadAllTextAsync(shared));
        Assert.Equal("old world", await File.ReadAllTextAsync(world));
        Assert.True(File.Exists(Path.Combine(_root, "retained.txt")));
        Assert.Empty(Directory.GetFiles(_root, "*.pending"));
    }

    [Fact]
    public async Task ReplacementFailureRollsBackEarlierFilesAndDoesNotRetireOldContent()
    {
        string shared = Path.Combine(_root, "definitions.json"), blocked = Path.Combine(_root, "blocked");
        await File.WriteAllTextAsync(shared, "old definitions");
        Directory.CreateDirectory(blocked);
        using var publication = new ContentPublication();
        await publication.StageAsync(shared, new { Value = "new definitions" }, PackageFiles.JsonOptions);
        await publication.StageAsync(blocked, new { Value = "new world" }, PackageFiles.JsonOptions);
        publication.Retire(shared);
        var error = Record.Exception(() => publication.Commit());
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.Equal("old definitions", await File.ReadAllTextAsync(shared));
    }

    [Fact]
    public async Task DuplicateTargetAbortsBatchWithoutReplacingTheEarlierStagedValue()
    {
        string file = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(file, "old");
        using var publication = new ContentPublication();
        await publication.StageAsync(file, new { Value = "first" }, PackageFiles.JsonOptions);
        await Assert.ThrowsAsync<InvalidDataException>(() => publication.StageAsync(file, new { Value = "second" }, PackageFiles.JsonOptions));
        Assert.Throws<InvalidOperationException>(() => publication.Commit());
        Assert.Equal("old", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task CommitPublishesAllFilesAndCancellationKeepsLiveFiles()
    {
        string file = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(file, "old");
        using var publication = new ContentPublication();
        await publication.StageAsync(file, new { Value = "new" }, PackageFiles.JsonOptions);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => publication.Commit(cancellation.Token));
        Assert.Equal("old", await File.ReadAllTextAsync(file));
        publication.Commit();
        Assert.Contains("new", await File.ReadAllTextAsync(file));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
