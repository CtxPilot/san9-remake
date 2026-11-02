using System.Text;

namespace San9.Content.Authoring;

/// <summary>Shared authoring defaults and available glyph references, independent of scenario state.</summary>
public sealed record OfficerFlagCatalog(int SchemaVersion, Dictionary<string, int> Surnames, int[] Glyphs)
{
    public const int CurrentSchemaVersion = 1;

    public int Resolve(string surname) => surname.Length > 0 && Rune.TryGetRuneAt(surname, 0, out var initial) &&
        Surnames.TryGetValue(initial.ToString(), out int glyph) ? glyph : -1;

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || Surnames is null || Glyphs is null ||
            Glyphs.Any(g => g < 0) || Glyphs.Distinct().Count() != Glyphs.Length ||
            Surnames.Any(p => string.IsNullOrEmpty(p.Key) || !Rune.TryGetRuneAt(p.Key, 0, out var initial) ||
                initial.Utf16SequenceLength != p.Key.Length ||
                !Glyphs.Contains(p.Value)))
            throw new InvalidDataException("Invalid officer flag catalog.");
    }

    public void ValidateReferences(IReadOnlySet<string> clips)
    {
        Validate();
        foreach (int glyph in Glyphs)
            foreach (int ink in Enumerable.Range(0, 2))
            {
                if (!clips.Contains($"flag-glyph-{ink}-{glyph}"))
                    throw new InvalidDataException($"Missing building flag glyph: {glyph}.");
                foreach (int direction in Enumerable.Range(0, 8))
                    if (!clips.Contains($"unit-flag-{direction}-glyph-{ink}-{glyph}"))
                        throw new InvalidDataException($"Missing directional flag glyph: {glyph}.");
            }
    }
}
