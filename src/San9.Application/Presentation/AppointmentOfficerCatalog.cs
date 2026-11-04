using San9.Content.Text;

namespace San9.Application.Presentation;

/// <summary>Appointment pages bind shared officer facts by field identity.</summary>
public static class AppointmentOfficerCatalog
{
    private static string[] Fields(string kind) => kind switch
    {
        "rank" => ["name", "office", "status", "loyalty", "leadership", "war", "intelligence", "politics", "location"],
        "reward" => ["name", "loyalty", "office", "capacity", "leadership", "war", "intelligence", "politics", "learned", "location"],
        "exile" => ["name", "status", "force", "loyalty", "leadership", "war", "intelligence", "politics", "treasures", "location"],
        "bestow" => ["name", "loyalty", "leadership", "war", "intelligence", "politics", "learned", "capacity", "treasures", "location"],
        "strategist" => ["name", "intelligence", "loyalty", "leadership", "war", "politics", "learned", "treasures", "location"],
        "execute" => ["name", "loyalty", "force", "leadership", "war", "intelligence", "politics", "learned", "treasures", "location"],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static (CatalogColumn Definition, int Page, int Column)[] Sources(string kind)
    {
        var source = OfficerCatalog.CorePages.SelectMany((page, p) =>
            page.Columns.Select((column, c) => (Definition: column, Page: p, Column: c)))
            .DistinctBy(c => c.Definition.Id).ToDictionary(c => c.Definition.Id);
        return Fields(kind).Select(id => source[id]).ToArray();
    }

    public static IReadOnlyList<CatalogPage> Pages(AppointmentDraft draft, TextCatalog texts) =>
        [new(CommandText.Resolve(texts, "title." + draft.Kind), Sources(draft.Kind).Select(c => c.Definition).ToArray()),
         .. OfficerCatalog.Pages(draft.View, texts)];

    public static CatalogRow[] Read(AppointmentDraft draft, TextCatalog texts, int page, OfficeAppointmentPlan? edited = null)
    {
        var columns = page == 0 ? Sources(draft.Kind) : OfficerCatalog.Pages(draft.View, texts)[page - 1].Columns
            .Select((column, c) => (Definition: column, Page: page - 1, Column: c)).ToArray();
        var tables = columns.Select(c => c.Page).Distinct().ToDictionary(p => p,
            p => OfficerCatalog.Read(draft.View, texts, p, null, null).Rows.ToDictionary(r => r.Key.Id));
        var plan = draft.Kind == "rank" ? edited ?? draft.OfficePlan : null;
        IEnumerable<int> people = plan is null ? draft.Candidates : plan.OrderedPeople;
        return people.Select(id => new CatalogRow(id, columns.Select(c =>
        {
            var cell = tables[c.Page][id].Cells[c.Column];
            return plan is null ? cell : plan.Cell(id, c.Definition.Id, cell, texts);
        }).ToArray())).ToArray();
    }
}
