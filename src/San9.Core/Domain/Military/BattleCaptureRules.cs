namespace San9.Core.Domain;

public enum BattleRank { Front, Middle, Rear }

/// <summary>Capture probability from resolved battle inputs; does not inspect global armies or consume random state.</summary>
public static class BattleCaptureRules
{
    public static int Chance(int effectiveAbility, int nearbyAttackers, BattleRank rank,
        bool hasRetreatCity, bool hasEscapeMount, bool doublesCapture, int noRetreatCaptureBonus)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(effectiveAbility);
        ArgumentOutOfRangeException.ThrowIfNegative(nearbyAttackers);
        if (!Enum.IsDefined(rank)) throw new ArgumentOutOfRangeException(nameof(rank));
        if (noRetreatCaptureBonus is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(noRetreatCaptureBonus));
        int bonus = hasRetreatCity ? 0 : noRetreatCaptureBonus;
        if (bonus == 100) return 100;
        if (hasEscapeMount || nearbyAttackers == 0) return bonus;
        long ability = Math.Max(0, 3L * Math.Max(0, effectiveAbility - 90) + 2L * Math.Min(effectiveAbility, 90) - 160) +
            Math.Max(0, Math.Min(effectiveAbility, 80) - 60) + Math.Max(1, Math.Min(effectiveAbility, 60) / 2);
        long baseChance = Math.Max(1, (120 - ability) / 3);
        // The smallest k with k*(k+1)/2 >= N gives diminishing returns for additional captors.
        long k = (long)Math.Sqrt(2L * nearbyAttackers);
        if (k * (k + 1) / 2 < nearbyAttackers) k++;
        long chance = baseChance * (nearbyAttackers + k * (k - 1) / 2) / k;
        if (doublesCapture) chance *= 2;
        chance = rank switch
        {
            BattleRank.Front => chance * 3 / 2,
            BattleRank.Rear => chance / 2,
            _ => chance
        };
        return (int)Math.Min(100, chance + bonus);
    }
}
