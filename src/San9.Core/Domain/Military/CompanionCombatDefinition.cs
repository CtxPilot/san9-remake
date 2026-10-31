using System.Collections.Immutable;

namespace San9.Core.Domain;

public sealed record FormationBonusExchange(int FirstId, int SecondId);
public sealed record NavalTacticTraining(int FormationId, ImmutableArray<int> LearnedTacticIds);
public sealed record SiegeProficiencyRule(int Category, ImmutableArray<int> FormationIds);

/// <summary>Authored combat references, independent of AI formation preferences and original numeric identities.</summary>
public sealed record CompanionCombatDefinition(
    FormationBonusExchange? LinkBonusExchange,
    FormationBonusExchange? SnipingBonusExchange,
    ImmutableArray<NavalTacticTraining> NavalTraining,
    SiegeProficiencyRule? SiegeProficiency,
    ImmutableArray<int> SiegeTowerFormationIds,
    int? LeaderInspirationTacticId)
{
    internal void Validate(IReadOnlyDictionary<int, FormationRule> formations, IReadOnlyDictionary<int, TacticRule> tactics)
    {
        bool ValidPair(FormationBonusExchange pair) => pair.FirstId != pair.SecondId &&
            formations.TryGetValue(pair.FirstId, out var first) && formations.TryGetValue(pair.SecondId, out var second) &&
            first.TravelMode == TravelMode.Land && second.TravelMode == TravelMode.Land &&
            first.Tactics is not null && second.Tactics is not null;
        if (LinkBonusExchange is { } link && !ValidPair(link) ||
            SnipingBonusExchange is { } sniping && !ValidPair(sniping))
            throw new ArgumentException("Invalid companion formation bonus exchange.");
        if (NavalTraining.IsDefault || NavalTraining.Select(n => n.FormationId).Distinct().Count() != NavalTraining.Length ||
            NavalTraining.Any(n => !formations.TryGetValue(n.FormationId, out var f) || f.TravelMode != TravelMode.Water ||
                n.LearnedTacticIds.IsDefaultOrEmpty || n.LearnedTacticIds.Distinct().Count() != n.LearnedTacticIds.Length ||
                n.LearnedTacticIds.Any(id => !tactics.TryGetValue(id, out var t) || t.Effect != TacticEffect.FormationTechnology)))
            throw new ArgumentException("Invalid companion naval training references.");
        bool ValidLandReferences(ImmutableArray<int> ids) => !ids.IsDefault && ids.Distinct().Count() == ids.Length &&
            ids.All(id => formations.TryGetValue(id, out var f) && f.TravelMode == TravelMode.Land && f.Combat is not null);
        if (!ValidLandReferences(SiegeTowerFormationIds) || SiegeProficiency is { } siege &&
            (siege.Category is < 0 or > 8 || !ValidLandReferences(siege.FormationIds)))
            throw new ArgumentException("Invalid companion siege formation references.");
        if (LeaderInspirationTacticId is int inspire && (!tactics.TryGetValue(inspire, out var tactic) || tactic.Effect != TacticEffect.Inspire))
            throw new ArgumentException("Invalid companion leader inspiration tactic reference.");
    }
}
