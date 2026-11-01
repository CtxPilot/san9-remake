using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Application.Presentation;

public enum DeploymentTacticMode { Formation, Infantry, Cavalry, MountedArchery, Crossbow, Intelligence }

public sealed partial class DeploymentDraft
{
    public bool IsTacticCompatible(int officerId) => Tactics.TryGetValue(officerId, out var id) && id is int tactic &&
        FormationId is int formation && Rules.Formations[formation].Tactics is not null &&
        Rules.Tactics[tactic].CompatibleFormations.Contains(formation);

    /// <summary>Applies one explicit batch to this draft, without issuing commands or evaluating combat targets.</summary>
    public void ConfigureTactics(DeploymentTacticMode mode = DeploymentTacticMode.Formation)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Tactics = Members.ToImmutableDictionary(id => id, id => RecommendTactic(id, mode));
    }

    private int? RecommendTactic(int officerId, DeploymentTacticMode mode)
    {
        int? before = Tactics[officerId];
        if (FormationId is not int formationId || Rules.Formations[formationId].Tactics is not { } formation)
            return before;

        // Keep a martial family when equally favored; power is comparable only within that family.
        int? currentCategory = before is int current ? Rules.Tactics[current].Category : null;
        return AvailableTactics(officerId)
            .Where(t => t.CompatibleFormations.Contains(formationId) && MatchesMode(t, mode))
            .OrderBy(t => mode == DeploymentTacticMode.Formation &&
                t.Category is int category && formation.FavoredCategories.Contains(category) ? 0 : 1)
            .ThenBy(t => t.Effect == TacticEffect.Martial ? 0 : 1 + IntelligencePreference(t.Effect))
            .ThenBy(t => t.Effect == TacticEffect.Martial ? t.Category == currentCategory ? -1 : t.Category : 0)
            .ThenByDescending(t => t.Power)
            .ThenBy(t => t.Id == before ? 0 : 1)
            .ThenBy(t => t.Id).FirstOrDefault()?.Id ?? before;
    }

    private static bool MatchesMode(TacticRule tactic, DeploymentTacticMode mode) => mode switch
    {
        DeploymentTacticMode.Formation => true,
        DeploymentTacticMode.Infantry => tactic.Effect == TacticEffect.Martial && tactic.Category == 0,
        DeploymentTacticMode.Cavalry => tactic.Effect == TacticEffect.Martial && tactic.Category == 1,
        DeploymentTacticMode.MountedArchery => tactic.Effect == TacticEffect.Martial && tactic.Category == 2,
        DeploymentTacticMode.Crossbow => tactic.Effect == TacticEffect.Martial && tactic.Category == 3,
        DeploymentTacticMode.Intelligence => tactic.Effect is not TacticEffect.Martial,
        _ => false
    };

    private static int IntelligencePreference(TacticEffect effect) => effect switch
    {
        TacticEffect.Illusion => 0,
        TacticEffect.Sorcery => 1,
        TacticEffect.Heal => 2,
        TacticEffect.Defection => 3,
        TacticEffect.CounterConfusion => 4,
        TacticEffect.TrapSelection => 5,
        TacticEffect.Confusion => 6,
        TacticEffect.CounterTrap => 7,
        TacticEffect.Inspire => 8,
        TacticEffect.Taunt => 9,
        _ => 10
    };
}
