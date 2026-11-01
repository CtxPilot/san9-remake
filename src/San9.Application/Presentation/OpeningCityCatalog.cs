using San9.Application.Authoring;

namespace San9.Application.Presentation;

/// <summary>Read-only city choices before initialization, using the shared city queries.</summary>
public sealed class OpeningCityCatalog
{
    private readonly AdministrativeCatalog _catalog;
    public OpeningCityCatalog(NewGameDraft draft)
    {
        _catalog = new(draft.Preview(), draft.Composition.Definitions.AddText(draft.Text));
    }
    public static IReadOnlyList<CatalogPage> Pages { get; } = new[] { new CatalogPage("所在",
        [new("name", "都市", 6, CatalogAlignment.Left), new("province", "州", 4, CatalogAlignment.Center),
            new("population", "人口", 7, CatalogAlignment.Right), new("draft", "兵役人口", 10, CatalogAlignment.Right),
            new("durability", "耐久", 6, CatalogAlignment.Right), new("public-order", "民心", 6, CatalogAlignment.Right),
            new("trait", "特性", 6, CatalogAlignment.Center)]) }.Concat(AdministrativeCatalog.Pages(InformationCategory.Cities)).ToArray();
    public CatalogCell Cell(int id, string field) => _catalog.Cell(InformationCategory.Cities, id, field);
    public CatalogRow[] Read(IEnumerable<int> ids, int page) => ids.Select(id => new CatalogRow(id,
        Pages[page].Columns.Select(c => Cell(id, c.Id)).ToArray())).ToArray();
}
