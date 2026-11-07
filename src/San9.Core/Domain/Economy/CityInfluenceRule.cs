using System.Collections.Immutable;

namespace San9.Core.Domain;

/// <summary>Shared geographic inputs; derived coverage is never authored per cell.</summary>
public sealed record BattlefieldGeography(int FootprintScale, ImmutableArray<MapCell> BlockedCells)
{
    public static BattlefieldGeography Empty { get; } = new(1, []);
}
public sealed record CityInfluenceRule(int RadiusCells, int LandTacticBonus)
{
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(RadiusCells);
        ArgumentOutOfRangeException.ThrowIfNegative(LandTacticBonus);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(LandTacticBonus, 100);
    }
}
public sealed record RockfallRule(int PropagationSteps, int AttackRange);
