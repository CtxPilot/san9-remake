using San9.Core.Domain;

namespace San9.Application.AI;

internal sealed partial class OriginalAiMilitary
{
    private MilitaryWorkIndex? militaryIndex;
    private MilitaryWorkIndex MilitaryIndex
    {
        get { Refresh(); return militaryIndex ??= new(World.Units, corpsId, Owner, Corps.IsRoot); }
    }

    // Physical/order associations only. Activity, daily-cycle history, diplomacy and projected
    // obstacles remain each work's filters; embedded guards never become a second field army.
    private sealed class MilitaryWorkIndex
    {
        internal UnitState[] Field { get; }
        internal UnitState[] AttackCandidates { get; }
        internal Dictionary<int, long> UndeployedAt { get; }
        internal HashSet<int> ConstructionRegions { get; }

        internal MilitaryWorkIndex(IEnumerable<UnitState> units, int corps, int owner, bool root)
        {
            Field = units.Where(FieldUnit).ToArray();
            AttackCandidates = Field.Where(u => u.EffectiveOrder?.Kind is MilitaryOrderKind.Move or
                MilitaryOrderKind.AttackUnit or MilitaryOrderKind.AttackSettlement).ToArray();
            UndeployedAt = Field.Where(u => !u.HasEnteredDailyCycle && u.ServicePlaceId is not null)
                .GroupBy(u => u.ServicePlaceId!.Value).ToDictionary(g => g.Key, g => g.Sum(u => (long)u.Soldiers));
            ConstructionRegions = Field.Where(u => u.OwnerId == owner &&
                    (u.CorpsId == corps || u.CorpsId is null && root) && Active(u) &&
                    u.EffectiveOrder is { Kind: MilitaryOrderKind.Construct, Construction: not null })
                .Select(u => u.EffectiveOrder!.Construction!.RegionId).ToHashSet();
        }
    }
}
