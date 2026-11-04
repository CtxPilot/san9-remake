using San9.Content.Authoring;

namespace San9.Content.Tests;

public sealed class OfficerFlagCatalogTests
{
    [Fact]
    public void SurnameDefaultsMustUseSingleCharactersAndAvailableGlyphs()
    {
        new OfficerFlagCatalog(1, new() { ["𠀀"] = 6000 }, [6000]).Validate();
        Assert.Throws<InvalidDataException>(() => new OfficerFlagCatalog(1, new() { ["司马"] = 66 }, [66]).Validate());
        Assert.Throws<InvalidDataException>(() => new OfficerFlagCatalog(1, new() { ["\uD800"] = 66 }, [66]).Validate());
        Assert.Throws<InvalidDataException>(() => new OfficerFlagCatalog(1, new() { ["司"] = 66 }, [77]).Validate());
        Assert.Throws<InvalidDataException>(() => new OfficerFlagCatalog(1, [], [66, 66]).Validate());
    }

    [Fact]
    public void AvailableGlyphsRequireBothInksAndAllBuildingAndUnitClips()
    {
        var catalog = new OfficerFlagCatalog(1, new() { ["司"] = 6000 }, [6000]);
        var clips = Enumerable.Range(0, 2).SelectMany(ink => Enumerable.Range(0, 8)
            .Select(direction => $"unit-flag-{direction}-glyph-{ink}-6000").Append($"flag-glyph-{ink}-6000")).ToHashSet();
        catalog.ValidateReferences(clips);
        clips.Remove("unit-flag-7-glyph-1-6000");
        Assert.Throws<InvalidDataException>(() => catalog.ValidateReferences(clips));
    }
}
