using System.Text.Json.Serialization;

namespace San9.Core.Domain;

/// <summary>Base-ability growth and editing limits, independent of effective ability and event eligibility.</summary>
public sealed record OfficerGrowthRules([property: JsonRequired] int MaximumBaseAbility)
{
    public static OfficerGrowthRules Default { get; } = new(100);
    public const int MinimumEditableAbility = 1;

    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaximumBaseAbility, MinimumEditableAbility);

    public bool CanIncrease(int current) => current < MaximumBaseAbility;

    public OfficerGrowthRules Resolve(CompanionSettings companion) => MaximumBaseAbility == companion.OfficerAbilityCap
        ? this : this with { MaximumBaseAbility = companion.OfficerAbilityCap };

    public static int GrowthChance(int current, bool dynamic, bool belongsToPlayer) => !dynamic ? 3 :
        (int)Math.Max(1, 5 - Math.Max(0L, (long)current - 35) / 15) + (belongsToPlayer ? 0 : 1);

    public int Increase(int current, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return CanIncrease(current) ? (int)Math.Min(MaximumBaseAbility, (long)current + amount) : current;
    }
}
