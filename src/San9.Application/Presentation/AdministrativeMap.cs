using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Application.Presentation;

public sealed record InformationMapMarker(int CityId, int X, int Y, int? OwnerId, int? TargetId);
public sealed record InformationMapField(string Label, string Value, CatalogAlignment Alignment);

public static class AdministrativeMap
{
    private sealed record SummaryColumn(string Id, string Label, CatalogAlignment Alignment);
    private static SummaryColumn C(string id, string label) => new(id, label, CatalogAlignment.Center);
    private static SummaryColumn N(string id, string label) => new(id, label, CatalogAlignment.Right);

    public static InformationMapMarker[] Markers(StrategySnapshot view, InformationCategory category, IEnumerable<int> candidates)
    {
        if (category is not (InformationCategory.Forces or InformationCategory.Cities)) throw new ArgumentOutOfRangeException(nameof(category));
        var available = candidates.ToHashSet();
        return view.Settlements.Where(s => s.Kind == SettlementKind.City).Select(s => {
            int? target = category == InformationCategory.Forces ? s.OwnerId : s.Id;
            return new InformationMapMarker(s.Id,s.X,s.Y,s.OwnerId,target is int id && available.Contains(id) ? id : null);
        }).ToArray();
    }
    public static InformationMapField[] Summary(StrategySnapshot view, TextCatalog texts, InformationKey key)
        => Summary(new AdministrativeCatalog(view, texts), key);

    public static InformationMapField[] Summary(AdministrativeCatalog catalog, InformationKey key)
    {
        SummaryColumn[] columns = key.Category switch {
            InformationCategory.Forces => [C("name","势力"),C("rank","官爵"),N("trust","信望"),C("tactics","兵法"),
                C("relation","友好"),C("adviser","军师"),N("cities","都市"),N("officers","在任"),
                N("population","人口"),N("draft","兵役人口"),N("soldiers","士兵"),N("wounded","伤兵"),
                N("money","资金"),N("food","兵粮"),N("money-balance","资金收支"),N("food-balance","兵粮收支")],
            InformationCategory.Cities => [C("name","都市"),N("durability","耐久"),C("trait","特性"),C("province","州"),
                N("public-order","民心"),N("commerce","收益"),N("cultivation","收获"),C("merchant","商人"),
                C("commander","大将"),N("serving","在任"),N("captives","俘虏"),N("free","在野"),
                N("population","人口"),N("draft","兵役人口"),N("soldiers","士兵"),N("wounded","伤兵")],
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        string Value(string id)
        {
            if (key.Category == InformationCategory.Cities && id is "durability" or "commerce" or "cultivation")
                return $"{catalog.Cell(key.Category, key.Id, id).Text}/{catalog.Cell(key.Category, key.Id, "max-" + id).Text}";
            if (!id.EndsWith("-balance",StringComparison.Ordinal)) return catalog.Cell(key.Category,key.Id,id).Text;
            string resource=id[..id.IndexOf('-')];
            var income=catalog.Cell(key.Category,key.Id,resource+"-income");
            var cost=catalog.Cell(key.Category,key.Id,resource+"-cost");
            return income.Number is long i && cost.Number is long c ? (i-c).ToString(System.Globalization.CultureInfo.InvariantCulture) : income.Text;
        }
        return columns.Select(c => new InformationMapField(c.Label,Value(c.Id),c.Alignment)).ToArray();
    }
}
