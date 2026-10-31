namespace San9.Core.Domain;

/// <summary>Value-equal member-count coefficients shared by personal tactic allocation.</summary>
public sealed record TacticAllocationCoefficients(int One, int Two, int Three, int Four, int FiveOrMore)
{
    public static TacticAllocationCoefficients Default { get; } = new(20, 14, 10, 9, 8);

    internal void Validate()
    {
        if (One is < 0 or > 20 || Two is < 0 or > 20 || Three is < 0 or > 20 ||
            Four is < 0 or > 20 || FiveOrMore is < 0 or > 20)
            throw new ArgumentOutOfRangeException(nameof(TacticAllocationCoefficients), "Tactic allocation coefficients must be between 0 and 20.");
    }

    public int ForMembers(int members)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(members);
        Validate();
        return members switch { 1 => One, 2 => Two, 3 => Three, 4 => Four, _ => FiveOrMore };
    }
}
