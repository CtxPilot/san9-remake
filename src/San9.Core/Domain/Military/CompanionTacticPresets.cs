using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San9.Core.Domain;

public sealed record FacilityTacticPreset(int FacilityTypeId, FacilityTactics Tactics);
public sealed record NavalTacticReference(int FormationId, int TacticId);
public sealed record NavalTacticFallback(int FormationId, int PreferredTacticId, int FallbackTacticId, int ContactRange);

/// <summary>Optional authored facility and ship tactics, selected without changing the base definitions.</summary>
public sealed record CompanionTacticPresets(
    [property: JsonRequired] ImmutableArray<FacilityTacticPreset> FacilityTactics,
    [property: JsonRequired] NavalTacticFallback? NavalFallback,
    [property: JsonRequired] NavalTacticReference? SuppressedNavalTactic)
{
    internal void Validate(IReadOnlyDictionary<int, FacilityRule> facilities,
        IReadOnlyDictionary<int, FormationRule> formations, IReadOnlyDictionary<int, TacticRule> tactics)
    {
        if (FacilityTactics.IsDefault || FacilityTactics.Select(p => p.FacilityTypeId).Distinct().Count() != FacilityTactics.Length)
            throw new ArgumentException("Invalid companion facility tactic presets.");
        foreach (var preset in FacilityTactics)
        {
            if (!facilities.TryGetValue(preset.FacilityTypeId, out var facility) || !facility.CanHostPersons || preset.Tactics is null)
                throw new ArgumentException("Invalid companion facility tactic reference.");
            preset.Tactics.Validate(tactics);
        }
        bool ValidNaval(int formationId, int tacticId) => formations.TryGetValue(formationId, out var formation) &&
            formation.TravelMode == TravelMode.Water && formation.Tactics?.AutomaticOpportunityChance is not null &&
            tactics.TryGetValue(tacticId, out var tactic) && tactic.Effect == TacticEffect.Martial;
        if (NavalFallback is { } fallback && (fallback.ContactRange <= 0 || fallback.PreferredTacticId == fallback.FallbackTacticId ||
            !ValidNaval(fallback.FormationId, fallback.PreferredTacticId) || !ValidNaval(fallback.FormationId, fallback.FallbackTacticId) ||
            !formations[fallback.FormationId].Tactics!.AutomaticTactics.Contains(fallback.PreferredTacticId) ||
            !tactics[fallback.FallbackTacticId].AffectsFacilities))
            throw new ArgumentException("Invalid companion naval tactic fallback.");
        if (SuppressedNavalTactic is { } suppressed && !ValidNaval(suppressed.FormationId, suppressed.TacticId))
            throw new ArgumentException("Invalid companion suppressed naval tactic.");
    }
}
