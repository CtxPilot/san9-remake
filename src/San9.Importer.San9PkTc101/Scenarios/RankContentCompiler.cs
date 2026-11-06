using San9.Content.Scenarios;

namespace San9.Importer.San9PkTc101.Scenarios;

public static class RankContentCompiler
{
    // The lowest rank is a real definition, not an absent rank reference.
    public static RankDefinition[] Compile(IEnumerable<San9ScenarioRankRecord> records, Func<int, string> name, Func<int, string> description) =>
        records.Select(r => new RankDefinition(r.Id, name(r.Id), r.CommandCapacity,
            r.PrestigeRequirement, r.PromotionLoyaltyBonus, San9ScenarioRankArchive.RecordCount - 1 - r.Id)
            { IsUnranked = r.Id == 8, PreferBalancedOfficeCandidates = r.Id == 0,
              Description = description(r.Id) }).ToArray();
}
