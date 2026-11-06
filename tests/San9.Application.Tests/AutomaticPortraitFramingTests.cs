using San9.Application.Authoring;
using San9.Content.Images;

namespace San9.Application.Tests;

public sealed class AutomaticPortraitFramingTests
{
    [Theory]
    [InlineData(240)]
    [InlineData(512)]
    [InlineData(1024)]
    public void NormalizedExperimentalFramingIsIndependentOfMotherImageResolution(int size)
    {
        var face = new PortraitFaceCandidate(.93, 72d / 240, 60d / 240, 72d / 240, 90d / 240,
            108d / 240, 90d / 240, 110d / 240, 110d / 240, 109d / 240, 122d / 240);
        var result = AutomaticPortraitFraming.Suggest(face);
        Assert.False(result.IsLimited);
        Assert.Equal(56d / 240, result.FaceAnchor.X, 12);
        Assert.Equal(43d / 240, result.FaceAnchor.Y, 12);
        var portrait = new PortraitDefinition(0, new("portrait", "portrait.png", size, size, 3),
            PortraitGeometry.BustFromFace(result.FaceAnchor), result.FaceAnchor);
        portrait.Validate();
        Assert.True(result.FaceAnchor.X <= face.Left - 6d / 240);
        Assert.True(result.FaceAnchor.Y + .5 >= face.Top + face.Height + 4d / 240);
    }

    [Fact]
    public void ReportedRightDownDriftUsesPoseAndCompressedVerticalSpacing()
    {
        var face = new PortraitFaceCandidate(.93, 98d / 240, 69d / 240, 59d / 240, 76d / 240,
            134d / 240, 96d / 240, 138d / 240, 112d / 240, 133.5 / 240, 123.5 / 240);
        var result = AutomaticPortraitFraming.Suggest(face);
        Assert.False(result.IsLimited);
        Assert.Equal(new PortraitAnchor(76d / 240, 39d / 240), result.FaceAnchor);
    }

    [Fact]
    public void FaceLargerThanFixedCropProducesAnExplicitLimitedSuggestion()
    {
        var result = AutomaticPortraitFraming.Suggest(new(.9, .1, .05, .8, .8, .5, .4, .5, .5, .5, .6));
        Assert.True(result.IsLimited);
        result.FaceAnchor.Validate(PortraitView.Face);
        PortraitGeometry.BustFromFace(result.FaceAnchor).Validate(PortraitView.Bust);
    }

    [Fact]
    public void LowScoresAndInvalidObservationsCannotProduceSuggestions()
    {
        var valid = new PortraitFaceCandidate(.9, .2, .2, .3, .4, .35, .3, .36, .4, .35, .45);
        foreach (var face in new[] { valid with { Score = .79 }, valid with { EyeX = double.NaN },
            valid with { NoseY = double.NaN }, valid with { MouthX = 2 }, valid with { Width = 0 }, valid with { EyeY = 2 }, valid with { Score = double.PositiveInfinity } })
        {
            Assert.False(AutomaticPortraitFraming.IsUsable(face));
            Assert.Throws<ArgumentException>(() => AutomaticPortraitFraming.Suggest(face));
        }
    }
}
