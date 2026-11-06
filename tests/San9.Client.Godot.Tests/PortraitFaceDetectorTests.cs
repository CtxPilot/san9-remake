using San9.Content.Images;

namespace San9.Client.Godot.Tests;

public sealed class PortraitFaceDetectorTests
{
    [Fact]
    public async Task ReusedManagedDetectorAcceptsBlankRgbWithoutGodotOrNativeInference()
    {
        using var detector = new PortraitFaceDetector();
        var pixels = new PngImage(64, 64, 3, new byte[64 * 64 * 3]);
        Assert.Empty(await detector.DetectAsync(() => pixels, default));
        Assert.Empty(await detector.DetectAsync(() => pixels, default));
    }

    [Fact]
    public async Task CancellationDuringImagePreparationDiscardsTheRequestBeforeInference()
    {
        using var detector = new PortraitFaceDetector();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => detector.DetectAsync(() =>
        {
            cancellation.Cancel();
            return new(64, 64, 3, new byte[64 * 64 * 3]);
        }, cancellation.Token));
    }
}
