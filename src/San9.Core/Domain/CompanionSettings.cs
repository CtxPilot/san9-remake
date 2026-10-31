using System.Text.Json.Serialization;

namespace San9.Core.Domain;

/// <summary>Companion options supplied by the host, independently of game saves.</summary>
public sealed record CompanionSettings
{
    public static CompanionSettings Default { get; } = new();

    [JsonRequired] public bool PlayerAutoDomesticEnabled { get; init; }
    [JsonRequired] public bool PlayerAutoSearchEnabled { get; init; }
    [JsonRequired] public bool DynamicOfficerAbilityGrowth { get; init; }
    [JsonRequired] public int OfficerAbilityCap { get; init; } = 100;
    [JsonRequired] public bool UseFacilityTacticPreset { get; init; }
    [JsonRequired] public bool EnableNavalTacticFallback { get; init; }
    [JsonRequired] public bool SuppressAdvancedNavalTactic { get; init; }
    [JsonRequired] public bool BoostMartialTacticDamage { get; init; }
    [JsonRequired] public bool ReduceSmallUnitTacticDamage { get; init; }
    [JsonRequired] public bool ReduceRangedTacticDamageAgainstFacilities { get; init; }
    [JsonRequired] public int MartialTacticDamageCoefficient { get; init; } = 10;
    [JsonRequired] public bool ReduceBattleWounded { get; init; }
    [JsonRequired] public bool IncreaseDurabilityDefense { get; init; }
    [JsonRequired] public bool AbilitiesAffectTacticAllocation { get; init; }
    [JsonRequired] public bool MoraleAffectsTacticCounters { get; init; }
    [JsonRequired] public bool RequireNavalTacticTraining { get; init; }
    [JsonRequired] public bool RedistributeFormationTacticBonuses { get; init; }
    [JsonRequired] public bool LeadersInspireInCombat { get; init; }
    [JsonRequired] public bool DynamicTacticProficiencyGrowth { get; init; }
    [JsonRequired] public bool AdjustIntelligenceTacticSuccess { get; init; }
    [JsonRequired] public bool ConfusionPowerAffectsDuration { get; init; }
    [JsonRequired] public bool RewardTacticParticipants { get; init; }
    [JsonRequired] public bool AffinityAffectsMartialChains { get; init; }
    [JsonRequired] public bool ProficiencyAffectsSiegePower { get; init; }
    [JsonRequired] public bool HalveFacilityIntelligenceCooldown { get; init; }
    [JsonRequired] public bool DisableDailyCombatProficiencyGain { get; init; }
    [JsonRequired] public bool AdjustSnipingChance { get; init; }
    [JsonRequired] public int SnipingChanceCoefficient { get; init; } = 5;
    [JsonRequired] public TacticAllocationCoefficients TacticAllocation { get; init; } = TacticAllocationCoefficients.Default;

    public void Validate()
    {
        if (OfficerAbilityCap < 100)
            throw new ArgumentOutOfRangeException(nameof(OfficerAbilityCap), "The base ability cap must be at least 100.");
        if (MartialTacticDamageCoefficient is < 5 or > 10)
            throw new ArgumentOutOfRangeException(nameof(MartialTacticDamageCoefficient), "Martial tactic damage coefficient must be between 5 and 10.");
        if (SnipingChanceCoefficient is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(SnipingChanceCoefficient), "Sniping chance coefficient must be between 0 and 10.");
        ArgumentNullException.ThrowIfNull(TacticAllocation);
        TacticAllocation.Validate();
    }
}
