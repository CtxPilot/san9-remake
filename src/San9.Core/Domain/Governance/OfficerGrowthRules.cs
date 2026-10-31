using System.Text.Json.Serialization;

namespace San9.Core.Domain;

/// <summary>Base-ability growth and editing limits, independent of effective ability and event eligibility.</summary>
public sealed record OfficerGrowthRules([property: JsonRequired] int MaximumBaseAbility)
{
    public static OfficerGrowthRules Default { get; } = new(100);
    public const int MinimumEditableAbility = 1;

    public void Validate() => ArgumentOutOfRangeException.ThrowIfLessThan(MaximumBaseAbility, MinimumEditableAbility);

    public bool CanIncrease(int current) => current < MaximumBaseAbility;

    public int Increase(int current, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        return CanIncrease(current) ? (int)Math.Min(MaximumBaseAbility, (long)current + amount) : current;
    }
}
