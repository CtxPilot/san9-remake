using System.Collections.Immutable;
using San9.Core.Domain;

namespace San9.Application.AI.Military;

public enum MilitaryGroupRole { Field, Siege, Mobile }
public sealed record MilitaryOfficerCandidate(OfficerState Officer, OfficerAbilities Abilities, ImmutableArray<TacticRule> Tactics)
{
    public ImmutableHashSet<int> AccessibleTactics { get; init; } = [];
}
public sealed record MilitaryFormationPair(int Land, int? Naval = null);
public sealed record MilitaryGroupMember(int OfficerId, int? TacticId, FormationPosition Position);
public sealed record MilitaryGroupOption(int FormationId, int? NavalFormationId, int CommanderId, uint Soldiers,
    long Money, long Score, ImmutableArray<MilitaryGroupMember> Members);
public sealed record MilitaryGroupEvaluation(MilitaryGroupOption? Baseline, MilitaryGroupOption? Best,
    int Evaluations, int TacticOptions, int PositionOptions, bool Incomplete);

/// <summary>A bounded formation experiment and reusable equipment evaluator. Scores compare own
/// capability, not victory probability. Public preparation remains authoritative for commands.</summary>
public sealed class MilitaryGroupEvaluator(MilitaryRulebook rules)
{
    public static MilitaryOfficerCandidate ObserveOwnOfficer(OfficerState officer, ForceState force,
        OfficerAbilityRules abilities, IReadOnlyCollection<TreasureState> holdings, TreasureRulebook treasures, MilitaryRulebook rules)
    {
        if (officer.OwnerId != force.Id) throw new ArgumentException("Military organization can only spend its own officers.");
        var available = TacticAvailability.ForOfficer(officer, force, holdings, treasures);
        return new(officer, abilities.Evaluate(officer, holdings), rules.Tactics.Values
            .Where(t => t.Selectable && available.ContainsKey(t.Id)).OrderBy(t => t.Id).ToImmutableArray())
            { AccessibleTactics = available.Keys.ToImmutableHashSet() };
    }

    public MilitaryGroupEvaluation Search(ForceState force, IReadOnlyList<MilitaryOfficerCandidate> pool,
        IEnumerable<MilitaryFormationPair> alternatives, int sourceId, uint availableSoldiers, long availableMoney,
        WorldPosition expectedPosition, MilitaryGroupRole role, IEnumerable<MilitaryEquipment>? enemyEquipment = null,
        int maximumEvaluations = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumEvaluations);
        var people = pool.Where(p => p.Officer.OwnerId == force.Id && p.Officer.SettlementId == sourceId && p.Officer.IsServing &&
                !p.Officer.IsDormant && !p.Officer.Acted && p.Officer.UnitId is null)
            .OrderByDescending(p => p.Abilities.Leadership).ThenBy(p => p.Officer.Id).ToArray();
        var pairs = alternatives.Distinct().OrderBy(p => p.Land).ThenBy(p => p.Naval).ToArray();
        var knownEnemy = (enemyEquipment ?? []).Where(e => e.Tactic.IsKnown && e.Tactic.Value is not null)
            .Select(e => rules.Tactics.GetValueOrDefault(e.Tactic.Value!.Value)?.Effect).ToHashSet();
        int evaluations = 0, tacticsChecked = 0, positionsChecked = 0;
        bool incomplete = false;
        MilitaryGroupOption? baseline = null, best = null;
        var seeds = new List<(MilitaryFormationPair Pair, MilitaryOfficerCandidate[] People, MilitaryGroupOption Option)>();
        // Every formation receives its basic check before any one group spends the improvement budget.
        foreach (var pair in pairs)
        {
            if (evaluations >= maximumEvaluations) { incomplete = true; break; }
            if (!rules.ValidFormationPair(pair.Land, pair.Naval) || !rules.CanSelectFormation(force, pair.Land) ||
                pair.Naval is int ship && !rules.CanSelectFormation(force, ship)) continue;
            var land = rules.Formations[pair.Land];
            var active = rules.Formation(pair.Land, pair.Naval, expectedPosition);
            int count = Math.Min(land.OfficerCapacity, pair.Naval is int naval ? rules.Formations[naval].OfficerCapacity : int.MaxValue);
            var seed = people.OrderByDescending(p => p.Abilities.Leadership + BestTacticValue(p, active, BattleRank.Middle, 1)).ThenBy(p => p.Officer.Id)
                .Take(count).ToArray();
            var option = Evaluate(pair, seed);
            if (option is null) continue;
            baseline ??= option;
            if (Better(option, best)) best = option;
            seeds.Add((pair, seed, option));
        }
        foreach (var seed in seeds.OrderByDescending(p => p.Option.Score).ThenBy(p => p.Pair.Land).ThenBy(p => p.Pair.Naval))
        {
            var members = seed.People;
            var local = seed.Option;
            foreach (var candidate in people)
            {
                if (members.Any(p => p.Officer.Id == candidate.Officer.Id)) continue;
                for (int slot = 0; slot < members.Length; slot++)
                {
                    var changed = members.ToArray(); changed[slot] = candidate;
                    var option = Evaluate(seed.Pair, changed);
                    if (option is not null && Better(option, local)) { members = changed; local = option; }
                    if (option is not null && Better(option, best)) best = option;
                    if (evaluations >= maximumEvaluations) { incomplete = true; break; }
                }
                if (evaluations >= maximumEvaluations) break;
            }
            if (evaluations >= maximumEvaluations) break;
        }
        return new(baseline, best, evaluations, tacticsChecked, positionsChecked, incomplete);

        MilitaryGroupOption? Evaluate(MilitaryFormationPair pair, MilitaryOfficerCandidate[] members)
        {
            if (evaluations >= maximumEvaluations) { incomplete = true; return null; }
            evaluations++;
            var land = rules.Formations[pair.Land];
            var active = rules.Formation(pair.Land, pair.Naval, expectedPosition);
            if (members.Length == 0 || land.Positions.Length < members.Length || active.Positions.Length < members.Length || availableSoldiers == 0 ||
                MissingRequirement(land) || pair.Naval is int requiredShip && MissingRequirement(rules.Formations[requiredShip]))
                return null;
            long money = land.DeploymentCost + (pair.Naval is int ship ? (long)rules.Formations[ship].DeploymentCost : 0);
            if (money > availableMoney) return null;
            uint soldiers = Math.Min(availableSoldiers, Math.Min(land.SoldierCapacity,
                pair.Naval is int naval ? rules.Formations[naval].SoldierCapacity : uint.MaxValue));
            var commander = members.OrderByDescending(p => p.Abilities.Leadership).ThenBy(p => p.Officer.Id).First();
            var choices = new List<MilitaryGroupMember>();
            var slots = land.Positions.OrderBy(p => p.Rank).ThenBy(p => p.Column).ToList();
            var navalRanks = active.Id != land.Id ? active.Positions.Take(members.Length).Select(p => p.Rank).ToArray() : null;
            long score = 0;
            foreach (var person in members.OrderByDescending(p => BestTacticValue(p, active, BattleRank.Front, members.Length)).ThenBy(p => p.Officer.Id))
            {
                // Columns of a row are equivalent. Do not branch on left/right permutations.
                var ranks = navalRanks is null ? slots.Select(p => p.Rank).Distinct() : navalRanks.Where(r => Enum.IsDefined(r)).Distinct();
                var rank = ranks.OrderByDescending(r => PositionValue(person, active, r, members.Length)).ThenBy(r => r).First();
                FormationPosition position;
                if (navalRanks is null)
                {
                    long center = (long)land.Positions.Min(p => p.Column) + land.Positions.Max(p => p.Column);
                    position = slots.Where(p => p.Rank == rank).OrderBy(p => Math.Abs(2L * p.Column - center)).ThenBy(p => p.Column).First();
                }
                else
                {
                    int ordinal = Array.IndexOf(navalRanks, rank);
                    position = land.Positions.OrderBy(p => p.Rank).ThenBy(p => p.Column).ElementAt(ordinal);
                    navalRanks[ordinal] = (BattleRank)(-1);
                }
                slots.Remove(position);
                var tactic = Tactic(person, active, rank, members.Length);
                choices.Add(new(person.Officer.Id, tactic?.Id, position));
                score += PositionValue(person, active, rank, members.Length);
            }
            var profile = active.Combat;
            int ordinary = role switch { MilitaryGroupRole.Siege => (profile?.AgainstGarrison ?? 0) + (profile?.AgainstWalls ?? 0),
                MilitaryGroupRole.Mobile => active.MovementBudget * 2, _ => (profile?.AgainstUnits ?? 0) + (profile?.Defense ?? 0) };
            score += soldiers / 100 * (long)(ordinary + commander.Abilities.Leadership);
            return new(pair.Land, pair.Naval, commander.Officer.Id, soldiers, money, score,
                choices.OrderBy(p => p.OfficerId).ToImmutableArray());

            bool MissingRequirement(FormationRule formation) => formation.RequiredTacticId is int required &&
                !members.Any(p => p.AccessibleTactics.Contains(required) || p.Officer.LearnedTactics.Contains(required) || force.ProvidedTactics.Contains(required));
        }

        IEnumerable<TacticRule> Options(MilitaryOfficerCandidate person, FormationRule formation)
        {
            bool Adapted(TacticRule t) => t.Effect == TacticEffect.Martial && t.Category is int category &&
                t.CompatibleFormations.Contains(formation.Id) && formation.Tactics?.FavoredCategories.Contains(category) == true;
            bool hasAdapted = person.Tactics.Any(Adapted);
            return person.Tactics.Where(t => t.Effect != TacticEffect.Martial || !hasAdapted || Adapted(t));
        }
        TacticRule? Tactic(MilitaryOfficerCandidate person, FormationRule formation, BattleRank rank, int members) =>
            Options(person, formation).OrderByDescending(t => Value(person, t, formation, rank, members)).ThenBy(t => t.Id).FirstOrDefault();
        long BestTacticValue(MilitaryOfficerCandidate p, FormationRule f, BattleRank rank, int count) =>
            Options(p, f).Select(t => Value(p, t, f, rank, count)).DefaultIfEmpty(0).Max();
        long PositionValue(MilitaryOfficerCandidate p, FormationRule f, BattleRank rank, int count)
        {
            positionsChecked++;
            int capture = BattleCaptureRules.Chance(p.Abilities.War, 1, rank, true, false, false, 0);
            return BestTacticValue(p, f, rank, count) - capture * (long)(p.Abilities.Intelligence + p.Abilities.Leadership);
        }
        long Value(MilitaryOfficerCandidate person, TacticRule tactic, FormationRule formation, BattleRank rank, int count)
        {
            tacticsChecked++;
            if (!tactic.CompatibleFormations.Contains(formation.Id) || formation.Tactics is null) return 0;
            bool favored = tactic.Category is int category && formation.Tactics.FavoredCategories.Contains(category);
            int chance = TacticMath.AllocationChance(rank, count, favored);
            if (tactic.Effect == TacticEffect.Martial)
            {
                int proficiency = tactic.Category is int c ? person.Officer.TacticProficiencies[c] : 0;
                uint damage = TacticMath.MartialDamage([new(tactic.Power, proficiency, person.Abilities.War, false)],
                    false, 10, false, 10000).Applied;
                return (long)damage * chance / 100;
            }
            int utility = tactic.Effect switch
            {
                TacticEffect.CounterConfusion => knownEnemy.Overlaps([TacticEffect.Confusion, TacticEffect.Illusion, TacticEffect.Defection]) ? 600 : 100,
                TacticEffect.CounterTrap => knownEnemy.Overlaps([TacticEffect.Trap, TacticEffect.TrapSelection]) ? 600 : 100,
                TacticEffect.Heal or TacticEffect.Inspire => 200,
                _ => 150
            };
            return utility * (long)chance * person.Abilities.Intelligence / 10000;
        }
    }

    private static bool Better(MilitaryGroupOption option, MilitaryGroupOption? other) => other is null || option.Score > other.Score ||
        option.Score == other.Score && (option.Money < other.Money || option.Money == other.Money && option.FormationId < other.FormationId);
}
