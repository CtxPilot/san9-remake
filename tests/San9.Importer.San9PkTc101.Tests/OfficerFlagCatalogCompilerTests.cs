using San9.Importer.San9PkTc101.Scenarios;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class OfficerFlagCatalogCompilerTests
{
    [Fact]
    public void SharedDefaultsSkipEmptyGlyphsRetainInvisibleBasePeopleAndExcludeExpansionSlots()
    {
        var empty = new San9ScenarioPersonRecord(
            0, 6000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            [], 0, ushort.MaxValue, [], 0);
        var people = Enumerable.Repeat(empty, 701).ToArray();
        people[1] = empty with { FamilyNameResourceCode = 1, FlagCode = 66, StatusCode = 8 };
        people[2] = empty with { FamilyNameResourceCode = 2, FlagCode = 77, StatusCode = 7 };
        people[3] = empty with { FamilyNameResourceCode = 3, FlagCode = 78, StatusCode = 7 };
        people[700] = empty with { FamilyNameResourceCode = 900, FlagCode = 66 };
        var catalog = OfficerFlagCatalogCompiler.Compile(people, family => family switch
        { 1 => "司徒", 2 => "司空", 3 => "𠀀", _ => throw new InvalidDataException("Unexpected name lookup.") });
        Assert.Equal(66, catalog.Resolve("司马"));
        Assert.Equal(78, catalog.Resolve("𠀀甲"));
        Assert.Equal(-1, catalog.Resolve("𠀁"));
        Assert.Equal(-1, catalog.Resolve(""));
        Assert.Equal(-1, catalog.Resolve("陌"));
        Assert.Contains(185, catalog.Glyphs);
    }
}
