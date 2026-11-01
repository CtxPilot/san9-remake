using San9.Application.Presentation;
using San9.Content.Authoring;

namespace San9.Client.Godot;

/// <summary>The opening roster has its own summary followed by the nine shared tactics pages.</summary>
public static class OpeningOfficerCatalog
{
    public static IReadOnlyList<CatalogPage> Pages { get; } = new[]
    {
        new CatalogPage("君主", OfficerCatalog.Pages[0].Columns.Take(6).ToArray())
    }.Concat(OfficerCatalog.Pages.Skip(4)).ToArray();

    public static CatalogCell Cell(CreatedOfficer person, string field)
    {
        if (field.StartsWith("mastery.", StringComparison.Ordinal))
        {
            bool mastered = person.MasteredCategories.Contains(int.Parse(field.AsSpan(8)));
            return new(mastered ? "☆" : "", mastered ? 1 : 0);
        }
        if (field.StartsWith("proficiency.", StringComparison.Ordinal)) return person.Proficiencies[int.Parse(field.AsSpan(12))];
        if (field.StartsWith("tactic.", StringComparison.Ordinal))
        {
            bool learned = person.LearnedTactics.Contains(int.Parse(field.AsSpan(7)));
            return new(learned ? "○" : "", learned ? 1 : 0);
        }
        return field switch
        {
            "name" => person.FullName, "leadership" => person.Abilities.Leadership, "war" => person.Abilities.War,
            "intelligence" => person.Abilities.Intelligence, "politics" => person.Abilities.Politics,
            "learned" => person.LearnedTactics.Count, _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
    }
}
