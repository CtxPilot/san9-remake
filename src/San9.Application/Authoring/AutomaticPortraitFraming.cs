using San9.Content.Images;

namespace San9.Application.Authoring;

/// <summary>Normalized detector observations; no dependency on a particular inference library.</summary>
public sealed record PortraitFaceCandidate(double Score, double Left, double Top, double Width, double Height,
    double EyeX, double EyeY, double NoseX, double NoseY, double MouthX, double MouthY);
public sealed record PortraitFramingSuggestion(PortraitAnchor FaceAnchor, bool IsLimited);

public static class AutomaticPortraitFraming
{
    public const double MinimumScore = .8;

    public static bool IsUsable(PortraitFaceCandidate face) =>
        new[] { face.Score, face.Left, face.Top, face.Width, face.Height, face.EyeX, face.EyeY, face.NoseX, face.NoseY, face.MouthX, face.MouthY }.All(double.IsFinite) &&
        face.Score >= MinimumScore && face.Score <= 1 && face.Width > 0 && face.Height > 0 &&
        face.Left < 1 && face.Top < 1 && face.Left + face.Width > 0 && face.Top + face.Height > 0 &&
        face.EyeX is >= 0 and <= 1 && face.EyeY is >= 0 and <= 1 &&
        face.NoseX is >= 0 and <= 1 && face.NoseY is >= 0 and <= 1 &&
        face.MouthX is >= 0 and <= 1 && face.MouthY is >= 0 and <= 1;

    public static PortraitFramingSuggestion Suggest(PortraitFaceCandidate face)
    {
        if (!IsUsable(face)) throw new ArgumentException("Unusable face observation.", nameof(face));
        // Composition is evaluated in the experiment's 240-unit analysis space, not source-image pixels.
        // Horizontal coefficients and baseline eye offset recovered with preview.4 on 418 even IDs.
        // Calibration method and the archived round5 vertical conclusion: specs/behavior/BEH-RENDER-0004-shared-portrait-views.md.
        double ex = face.EyeX * 240, ey = face.EyeY * 240;
        double noseX = (face.NoseX - face.EyeX) * 240, noseY = (face.NoseY - face.EyeY) * 240;
        double mouthX = (face.MouthX - face.EyeX) * 240, mouthY = (face.MouthY - face.EyeY) * 240;
        double left = face.Left * 240, top = face.Top * 240;
        double right = (face.Left + face.Width) * 240, bottom = (face.Top + face.Height) * 240;

        double ax = Math.Round(Math.Clamp(ex - .32687782474704924 - 2.2626836645913575 * noseX
            + 1.1150760182899369 * mouthX - 80, 0, 80));
        double minX = Math.Max(32, right + 6 - 96), maxX = Math.Min(112, left - 6);
        bool limitedX = minX > maxX;
        double faceX = limitedX ? ax + 32 : Math.Round(Math.Clamp(ax + 32, minX, maxX));

        double compressedOffset = 90.50020106678383 + .9210757121211361 * noseY - 1.7537340016555039 * mouthY;
        double calibratedY = Math.Round(Math.Clamp(ey - 51.5 - 30, 0, 60));
        double previousY = Math.Min(calibratedY,
            Math.Max(Math.Round(Math.Clamp(ey - compressedOffset - 30, 0, 60)), Math.Max(0, bottom + 4 - 150)));
        double weight = Math.Clamp((mouthY - 30) / 4, 0, 1);
        weight = weight * weight * (3 - 2 * weight);
        double targetOffset = (1 - weight) * compressedOffset + weight * 42;
        double minY = Math.Max(0, bottom + 4 - 150), maxY = Math.Min(60, top - 6 - 30);
        bool limitedY = minY > maxY;
        double ay = limitedY ? previousY : Math.Round(Math.Clamp(ey - targetOffset - 30, minY, maxY));
        return new(new(faceX / 240, (ay + 30) / 240), limitedX || limitedY);
    }
}
