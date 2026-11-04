using San9.Core.Domain;

namespace San9.Application.AI.Military;

/// <summary>One target-aware equipment rule shared by deployment, refinement and existing units.</summary>
internal sealed class MilitaryGroupEquipment
{
    private readonly MilitaryRulebook rules;
    private readonly CompanionSettings companion;
    private readonly bool settlement;
    private readonly MilitaryPresence? target;
    private readonly HashSet<TacticEffect?> knownEnemy;
    private readonly Dictionary<(int Person, int Formation, BattleRank Rank, int Count, uint Soldiers, int Leadership), (TacticRule? Tactic, long Value, long AttackDamage)> cache = [];
    internal int TacticsChecked { get; private set; }

    internal MilitaryGroupEquipment(MilitaryRulebook rules, CompanionSettings companion, bool settlement,
        MilitaryPresence? target, IEnumerable<MilitaryEquipment>? enemyEquipment)
    {
        this.rules = rules; this.companion = companion; this.settlement = settlement; this.target = target;
        knownEnemy = (enemyEquipment ?? []).Where(e => e.Tactic.IsKnown && e.Tactic.Value is not null)
            .Select(e => rules.Tactics.GetValueOrDefault(e.Tactic.Value!.Value)?.Effect).ToHashSet();
    }

    internal (TacticRule? Tactic, long Value, long AttackDamage) Choose(MilitaryOfficerCandidate person, FormationRule formation, BattleRank rank, int count, uint soldiers, int leadership)
    {
        var key = (person.Officer.Id, formation.Id, rank, count, soldiers, leadership);
        if (cache.TryGetValue(key, out var cached)) return cached;
        bool Supporting(TacticRule t) => t.Effect is TacticEffect.CounterConfusion or TacticEffect.CounterTrap or TacticEffect.Heal or TacticEffect.Inspire;
        var usable = person.Tactics.Where(t => rules.SupportsTactic(t, formation, companion) &&
            (Supporting(t) || rules.SupportsTacticTarget(t, formation, companion, settlement))).ToArray();
        bool Adapted(TacticRule t) => t.Effect == TacticEffect.Martial && t.Category is int c && formation.Tactics?.FavoredCategories.Contains(c) == true;
        bool adapted = usable.Any(Adapted);
        TacticRule? selected = null; long bestValue = 0, selectedDamage = 0;
        foreach (var tactic in usable.Where(t => t.Effect != TacticEffect.Martial || !adapted || Adapted(t)))
        {
            TacticsChecked++;
            if (formation.Tactics is null) continue;
            bool favored = tactic.Category is int c && formation.Tactics.FavoredCategories.Contains(c);
            int? ability = companion.AbilitiesAffectTacticAllocation ? tactic.Effect == TacticEffect.Martial ? person.Abilities.Leadership : person.Abilities.Intelligence : null;
            int chance = TacticMath.AllocationChance(rank, count, favored, ability, companion.TacticAllocation);
            long value;
            if (tactic.Effect == TacticEffect.Martial)
            {
                int proficiency = tactic.Category is int category ? person.Officer.TacticProficiencies[category] : 0;
                bool tower = rules.CompanionCombat?.SiegeTowerFormationIds.Contains(formation.Id) == true;
                bool ranged = settlement && tactic.Category is 2 or 3 && !(tactic.Category == 3 && tower);
                uint damage = TacticMath.MartialDamage([new(tactic.Power, proficiency, person.Abilities.War, false)], false, 10, false,
                    target?.Soldiers ?? 10000, companion, new(soldiers, leadership, target?.Durability ?? 0, ranged)).Applied;
                value = (long)damage * chance * formation.Tactics.OpportunityChance / 10000;
            }
            else
            {
                int utility = tactic.Effect switch
                {
                    TacticEffect.CounterConfusion => knownEnemy.Overlaps([TacticEffect.Confusion, TacticEffect.Illusion, TacticEffect.Defection]) ? 600 : 100,
                    TacticEffect.CounterTrap => knownEnemy.Overlaps([TacticEffect.Trap, TacticEffect.TrapSelection]) ? 600 : 100,
                    TacticEffect.Heal or TacticEffect.Inspire => 200,
                    _ => 150
                };
                value = utility * (long)chance * person.Abilities.Intelligence / 10000;
            }
            if (selected is null || value > bestValue || value == bestValue && tactic.Id < selected.Id) { selected = tactic; bestValue = value; selectedDamage = tactic.Effect == TacticEffect.Martial ? value : 0; }
        }
        return cache[key] = (selected, bestValue, selectedDamage);
    }
}
