using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Tests;

/// <summary>Explicit columns for fixtures whose scenarios only care about battle rows.</summary>
internal static class FormationTestData
{
    internal static ImmutableArray<FormationPosition> Layout(IEnumerable<BattleRank>? ranks)
    {
        if (ranks is null) return default;
        Dictionary<BattleRank, int> columns = [];
        return ranks.Select(rank =>
        {
            int column = columns.GetValueOrDefault(rank);
            columns[rank] = column + 1;
            return new FormationPosition(column, rank);
        }).ToImmutableArray();
    }

    internal static ImmutableDictionary<int, FormationPosition> Members(IEnumerable<KeyValuePair<int, BattleRank>> ranks) =>
        ranks.ToImmutableDictionary(p => p.Key, p => new FormationPosition(0, p.Value));
}
