namespace San9.Client.Godot.Tests;

public sealed class OptionalAudioTests
{
    [Fact]
    public void MissingLibraryAndUndeclaredFilesNeverReachTheEngineDecoder()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-optional-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "music.wav"), []);
            using var streams = new AudioStreams(root, new Dictionary<string, string> { ["music"] = "music.wav" });
            Assert.Null(streams.Get("music", loop: true));
            Assert.Null(streams.Get("missing-role", loop: false));
            Assert.Null(streams.GetFile("missing.wav", loop: false));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
