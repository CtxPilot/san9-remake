using FaceDetection;
using San9.Application.Authoring;
using San9.Content.Images;

namespace San9.Client.Godot;

/// <summary>Serial inference worker owned by the authoring page, with a reusable managed workspace.</summary>
internal sealed class PortraitFaceDetector : IDisposable
{
    private readonly object _sync = new();
    private FaceDetector? _detector;
    private int _closed;

    public Task<PortraitFaceCandidate[]> DetectAsync(Func<PngImage> load, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            token.ThrowIfCancellationRequested();
            var pixels = load();
            token.ThrowIfCancellationRequested();
            if (pixels.Channels != 3) throw new InvalidDataException("Portrait detection requires RGB24 pixels.");
            _detector ??= new();
            var faces = _detector.Detect(pixels.Pixels, pixels.Width, pixels.Height, checked(pixels.Width * 3), PixelFormat.Rgb24);
            token.ThrowIfCancellationRequested();
            return faces.Select(face => new PortraitFaceCandidate(face.Score,
                (double)face.Bounds.X / pixels.Width, (double)face.Bounds.Y / pixels.Height,
                (double)face.Bounds.Width / pixels.Width, (double)face.Bounds.Height / pixels.Height,
                ((double)face.Landmarks.RightEye.X + face.Landmarks.LeftEye.X) / 2 / pixels.Width,
                ((double)face.Landmarks.RightEye.Y + face.Landmarks.LeftEye.Y) / 2 / pixels.Height,
                (double)face.Landmarks.Nose.X / pixels.Width, (double)face.Landmarks.Nose.Y / pixels.Height,
                ((double)face.Landmarks.RightMouthCorner.X + face.Landmarks.LeftMouthCorner.X) / 2 / pixels.Width,
                ((double)face.Landmarks.RightMouthCorner.Y + face.Landmarks.LeftMouthCorner.Y) / 2 / pixels.Height))
                .Where(AutomaticPortraitFraming.IsUsable).ToArray();
        }
    }, token);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        // Do not block the UI while an uncancellable inference call is finishing.
        _ = Task.Run(() => { lock (_sync) { _detector?.Dispose(); _detector = null; } });
    }
}
