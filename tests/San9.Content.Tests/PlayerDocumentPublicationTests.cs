using System.Text.Json;
using San9.Content.Packages;

namespace San9.Content.Tests;

public sealed class PlayerDocumentPublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "san9-player-publication-" + Guid.NewGuid().ToString("N"));

    private sealed class PausedDocument(TaskCompletionSource entered, ManualResetEventSlim release)
    {
        public int Value { get { entered.TrySetResult(); release.Wait(); return 1; } }
    }

    [Fact]
    public async Task OverlappingTransactionsCannotExchangeDraftsAndStaleWritersCannotOverwriteTheWinner()
    {
        string firstPath = Path.Combine(_root, "a.json"), sharedPath = Path.Combine(_root, "b.json"), otherPath = Path.Combine(_root, "c.json");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var first = Task.Run(() => ScenarioDataFiles.WritePlayerDocumentsAsync([
            new(firstPath, new PausedDocument(entered, release), null), new(sharedPath, new { Value = 1 }, null)]));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<IOException>(() => ScenarioDataFiles.WritePlayerDocumentsAsync([
                new(sharedPath, new { Value = 2 }, null), new(otherPath, new { Value = 2 }, null)]));
            Assert.False(File.Exists(otherPath));
        }
        finally { release.Set(); await first; }
        var published = await first;
        Assert.Equal(1, ReadValue(firstPath));
        Assert.Equal(1, ReadValue(sharedPath));
        await Assert.ThrowsAsync<InvalidDataException>(() => ScenarioDataFiles.WritePlayerDocumentsAsync([
            new(sharedPath, new { Value = 2 }, null), new(otherPath, new { Value = 2 }, null)]));
        Assert.False(File.Exists(otherPath));
        await ScenarioDataFiles.WritePlayerDocumentsAsync([new(sharedPath, new { Value = 3 }, published[sharedPath])]);
        Assert.Equal(3, ReadValue(sharedPath));
        Assert.Empty(Directory.GetFiles(_root, "*.pending"));
    }

    [Fact]
    public async Task NoncooperatingFileChangesDuringPreparationArePreservedAndReleaseTheLocks()
    {
        string target = Path.Combine(_root, "a.json");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var writing = Task.Run(() => ScenarioDataFiles.WritePlayerDocumentsAsync([new(target, new PausedDocument(entered, release), null)]));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await File.WriteAllTextAsync(target, "{\"value\":9}");
        }
        finally { release.Set(); }
        await Assert.ThrowsAsync<InvalidDataException>(() => writing);
        Assert.Equal(9, ReadValue(target));
        await ScenarioDataFiles.WritePlayerDocumentsAsync([new(target, new { Value = 10 }, await File.ReadAllBytesAsync(target))]);
        Assert.Equal(10, ReadValue(target));
        Assert.Empty(Directory.GetFiles(_root, "*.pending"));
    }

    private static int ReadValue(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("value").GetInt32();
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
