using San9.Content.Text;

namespace San9.Application.Presentation;

public static class EndingOfficeCatalog
{
    private static Dictionary<string, (CatalogColumn Definition, int Page, int Column)> SourceColumns(StrategySnapshot view, TextCatalog texts) => OfficerCatalog.Pages(view, texts)
        .SelectMany((page, p) => page.Columns.Select((column, c) => (Definition: column, Page: p, Column: c)))
        .DistinctBy(c => c.Definition.Id).ToDictionary(c => c.Definition.Id);
    private static readonly Dictionary<string, string[]> Columns = new()
    {
        ["chancellor"] = ["leadership", "politics", "proficiency.4", "proficiency.6", "learned", "war", "intelligence", "office", "service"],
        ["marshal"] = ["leadership", "proficiency.4", "proficiency.6", "learned", "war", "intelligence", "politics", "office", "service"],
        ["protector"] = ["leadership", "war", "proficiency.4", "proficiency.6", "learned", "intelligence", "politics", "office", "service"],
        ["successor"] = ["politics", "leadership", "war", "intelligence", "learned", "office", "loyalty", "service"]
    };
    public static IReadOnlyList<CatalogPage> Pages(StrategySnapshot view, TextCatalog texts, string office)
    {
        var source = SourceColumns(view, texts);
        return [new("能力", new[] { "name" }.Concat(Columns[office]).Select(id => source[id].Definition).ToArray())];
    }
    public static CatalogRow[] Read(EndingDraft draft, TextCatalog texts)
    {
        var source = SourceColumns(draft.View, texts);
        var columns = new[] { "name" }.Concat(Columns[draft.CurrentOffice]).Select(id => source[id]).ToArray();
        var tables = columns.Select(c => c.Page).Distinct().ToDictionary(page => page,
            page => OfficerCatalog.Read(draft.View, texts, page, null, null).Rows.ToDictionary(r => r.Key.Id));
        var candidates = draft.Candidates.ToHashSet();
        string sort = draft.CurrentOffice == "successor" ? "politics" : "leadership";
        return draft.Snapshot.Officers.Where(o => candidates.Contains(o.Id))
            .OrderByDescending(o => sort == "politics" ? o.Abilities.Politics : o.Abilities.Leadership).ThenBy(o => o.Id)
            .Select(o => new CatalogRow(o.Id, columns.Select(c => tables[c.Page][o.Id].Cells[c.Column]).ToArray())).ToArray();
    }
}
