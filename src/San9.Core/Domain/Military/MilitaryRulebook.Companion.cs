using System.Collections.Immutable;

namespace San9.Core.Domain;

public sealed partial class MilitaryRulebook
{
    public FacilityTactics? FacilityTacticProfile(SettlementState settlement, CompanionSettings? companion = null)
    {
        if (!TryPlace(settlement, out var place) || place.FacilityTypeId is not int type) return null;
        return companion?.UseFacilityTacticPreset == true && CompanionTactics is { } presets &&
            presets.FacilityTactics.FirstOrDefault(p => p.FacilityTypeId == type) is { } preset
            ? preset.Tactics : Facilities[type].Tactics;
    }

    public ImmutableArray<int> AutomaticTacticIds(FormationRule formation, CompanionSettings companion)
    {
        var ids = formation.Tactics?.AutomaticTactics ?? [];
        if (companion.EnableNavalTacticFallback && CompanionTactics?.NavalFallback is { } fallback &&
            formation.Id == fallback.FormationId && !ids.Contains(fallback.FallbackTacticId))
            ids = ids.Add(fallback.FallbackTacticId);
        if (companion.SuppressAdvancedNavalTactic && CompanionTactics?.SuppressedNavalTactic is { } suppressed &&
            formation.Id == suppressed.FormationId)
            ids = ids.Remove(suppressed.TacticId);
        return ids;
    }

    public bool SupportsTactic(TacticRule tactic, FormationRule formation, CompanionSettings companion) =>
        tactic.CompatibleFormations.Contains(formation.Id) || companion.EnableNavalTacticFallback &&
        CompanionTactics?.NavalFallback is { } fallback && fallback.FormationId == formation.Id && fallback.FallbackTacticId == tactic.Id;

    public bool SupportsTacticTarget(TacticRule tactic, FormationRule formation, CompanionSettings companion, bool settlement) =>
        SupportsTactic(tactic, formation, companion) &&
        (!settlement || tactic.AffectsFacilities && TacticContactRange(tactic, formation, companion) is null);

    public int? TacticContactRange(TacticRule tactic, FormationRule formation, CompanionSettings companion) =>
        companion.EnableNavalTacticFallback && CompanionTactics?.NavalFallback is { } fallback &&
        fallback.FormationId == formation.Id && fallback.PreferredTacticId == tactic.Id
            ? fallback.ContactRange : tactic.ContactRange;
}
