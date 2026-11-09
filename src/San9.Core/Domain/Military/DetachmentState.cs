using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San9.Core.Domain;

/// <summary>A member's equipment and soldiers in a mixed unit.</summary>
public sealed record DetachmentState(int TroopTypeId, int Level, uint Soldiers)
{
    [JsonIgnore] public bool IsValid => TroopTypeId >= 0 && Level > 0;
}

public static class DetachmentAllocation
{
    public static ImmutableDictionary<int, DetachmentState> CreateDefault(
        IEnumerable<int> officers, IReadOnlyDictionary<int, int?> tactics, uint soldiers,
        ForceState force, MilitaryRulebook military)
    {
        var members = officers.ToArray();
        if (members.Length == 0 || soldiers < members.Length) return ImmutableDictionary<int, DetachmentState>.Empty;
        var eligible = military.TroopTypes.Values.SelectMany(type => Enumerable.Range(1, type.MaximumLevel)
            .Where(level => type.LevelResearchProjects.All(project => project.Key > level || force.CompletedResearch.Contains(project.Value)))
            .Select(level => (Type: type, Level: level))).ToArray();
        if (eligible.Length == 0) return ImmutableDictionary<int, DetachmentState>.Empty;
        var result = ImmutableDictionary.CreateBuilder<int, DetachmentState>();
        uint each = soldiers / (uint)members.Length;
        uint remainder = soldiers % (uint)members.Length;
        for (int index = 0; index < members.Length; index++)
        {
            int? tactic = tactics.GetValueOrDefault(members[index]);
            var selected = eligible.OrderByDescending(candidate => tactic is int id &&
                    candidate.Type.TacticMinimumLevels.TryGetValue(id, out int minimum) && minimum <= candidate.Level)
                .ThenBy(candidate => candidate.Level).ThenBy(candidate => candidate.Type.Id).First();
            result.Add(members[index], new(selected.Type.Id, selected.Level, each + ((uint)index < remainder ? 1u : 0u)));
        }
        return result.ToImmutable();
    }

    public static ImmutableDictionary<int, DetachmentState> ApplyMeleeLoss(UnitState unit, uint loss)
    {
        var result = unit.Detachments;
        foreach (var rank in Enum.GetValues<BattleRank>())
        {
            var group = result.Where(pair => unit.MemberPositions.TryGetValue(pair.Key, out var position) && position.Rank == rank)
                .ToImmutableDictionary();
            uint groupSoldiers = (uint)group.Values.Aggregate(0UL, (sum, detachment) => sum + detachment.Soldiers);
            uint groupLoss = Math.Min(loss, groupSoldiers);
            foreach (var pair in Resize(group, groupSoldiers - groupLoss)) result = result.SetItem(pair.Key, pair.Value);
            loss -= groupLoss;
        }
        if (loss == 0) return result;
        uint remaining = (uint)result.Values.Aggregate(0UL, (sum, detachment) => sum + detachment.Soldiers);
        return Resize(result, remaining - loss);
    }

    /// <summary>Resizes an allocation proportionally with stable largest-remainder rounding.</summary>
    public static ImmutableDictionary<int, DetachmentState> Resize(
        ImmutableDictionary<int, DetachmentState> detachments, uint soldiers)
    {
        if (detachments.IsEmpty) return detachments;
        ulong previous = 0;
        foreach (var detachment in detachments.Values) previous += detachment.Soldiers;
        if (previous == 0)
        {
            int first = detachments.Keys.Min();
            return detachments.SetItem(first, detachments[first] with { Soldiers = soldiers });
        }

        var shares = detachments.Select(pair =>
        {
            ulong product = (ulong)pair.Value.Soldiers * soldiers;
            return (pair.Key, Value: pair.Value, Count: (uint)(product / previous), Remainder: product % previous);
        }).ToArray();
        ulong allocated = 0;
        foreach (var share in shares) allocated += share.Count;
        var result = detachments.ToBuilder();
        foreach (var share in shares)
            result[share.Key] = share.Value with { Soldiers = share.Count };
        foreach (var share in shares.OrderByDescending(s => s.Remainder).ThenBy(s => s.Key).Take((int)(soldiers - allocated)))
            result[share.Key] = result[share.Key] with { Soldiers = result[share.Key].Soldiers + 1 };
        return result.ToImmutable();
    }
}
