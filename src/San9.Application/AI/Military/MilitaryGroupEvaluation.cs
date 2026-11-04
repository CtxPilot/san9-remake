using System.Collections.Immutable;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

public enum MilitaryGroupRole { Field, Siege, Mobile }
public sealed record MilitaryOfficerCandidate(OfficerState Officer, OfficerAbilities Abilities, ImmutableArray<TacticRule> Tactics)
{
    public ImmutableHashSet<int> AccessibleTactics { get; init; } = [];
}
public sealed record MilitaryFormationPair(int Land, int? Naval = null);
public sealed record MilitaryGroupObjective(MilitaryPresence Target, WorldPosition SourcePosition, bool CanHostPersons)
{
    public bool RequiresCombat => !Target.Object.IsSettlement ||
        !CombatMath.IsSettlementDefeated(Target.Soldiers, Target.Morale, Target.Durability, CanHostPersons);
}
public sealed record MilitaryGroupMember(int OfficerId, int? TacticId, FormationPosition Position);
public sealed record MilitaryGroupOption(int FormationId, int? NavalFormationId, int CommanderId, uint Soldiers,
    long Money, long Score, ImmutableArray<MilitaryGroupMember> Members)
{
    public long ExpectedLoss { get; init; }
    public long OfficerRisk { get; init; }
}
public sealed record MilitaryGroupEvaluation(MilitaryGroupOption? Baseline, MilitaryGroupOption? Best,
    int BaselineEvaluations, int Improvements, int TacticOptions, int PositionOptions, bool ImprovementLimited)
{
    public int Evaluations => BaselineEvaluations + Improvements;
}

/// <summary>Every affordable, usable formation receives a task-correct baseline. The optional
/// improvement allowance never removes formation classes or target eligibility from that baseline.</summary>
public sealed class MilitaryGroupEvaluator(MilitaryRulebook rules, GovernanceRulebook governance,
    IReadOnlyDictionary<int, uint> rankCapacities, CompanionSettings companion)
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
        int maximumImprovements = 32, MilitaryGroupObjective? objective = null, int morale = 100,
        Func<IReadOnlyList<int>, uint>? sourceCapacity = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumImprovements);
        var target = objective?.Target;
        var people = pool.Where(p => p.Officer.OwnerId == force.Id && p.Officer.SettlementId == sourceId && p.Officer.IsServing &&
                !p.Officer.IsDormant && !p.Officer.Acted && p.Officer.UnitId is null)
            .OrderBy(p => p.Officer.Id).ToArray();
        var byId = people.ToDictionary(p => p.Officer.Id);
        var abilities = people.ToDictionary(p => p.Officer.Id, p => p.Abilities);
        bool settlement = target?.Object.IsSettlement ?? role == MilitaryGroupRole.Siege;
        var equipment = new MilitaryGroupEquipment(rules, companion, settlement, target, enemyEquipment);
        int baselines = 0, improvements = 0, positionsChecked = 0;
        MilitaryGroupOption? baseline = null, best = null;
        foreach (var pair in alternatives.Distinct().OrderBy(p => p.Land).ThenBy(p => p.Naval))
        {
            if (!rules.ValidFormationPair(pair.Land, pair.Naval) || !rules.CanSelectFormation(force, pair.Land) ||
                pair.Naval is int ship && !rules.CanSelectFormation(force, ship)) continue;
            var land = rules.Formations[pair.Land];
            var active = rules.Formation(pair.Land, pair.Naval, expectedPosition);
            long cost = land.DeploymentCost + (pair.Naval is int naval ? (long)rules.Formations[naval].DeploymentCost : 0);
            if (cost > availableMoney || people.Length == 0 || availableSoldiers == 0) continue;
            int capacity = Math.Min(land.OfficerCapacity, pair.Naval is int boat ? rules.Formations[boat].OfficerCapacity : int.MaxValue);
            var required = new[] { land.RequiredTacticId, pair.Naval is int vessel ? rules.Formations[vessel].RequiredTacticId : null }
                .OfType<int>().Where(t => !force.ProvidedTactics.Contains(t)).Distinct().ToArray();
            var seed = new List<MilitaryOfficerCandidate>();
            var uncovered = required.ToHashSet();
            while (uncovered.Count > 0 && seed.Count < capacity)
            {
                var specialist = people.Where(p => !seed.Contains(p)).OrderByDescending(p => uncovered.Count(t => Has(p, t)))
                    .ThenByDescending(p => p.Abilities.Leadership).ThenBy(p => p.Officer.Id).FirstOrDefault();
                if (specialist is null || !uncovered.Any(t => Has(specialist, t))) break;
                seed.Add(specialist);
                uncovered.RemoveWhere(t => Has(specialist, t));
            }
            if (uncovered.Count > 0 || capacity == 0) continue;
            // Capacity participates before selection and scoring, including the ruler's rank limit.
            var leader = people.Where(p => CommandCapacity(p) > 0 &&
                    (sourceCapacity?.Invoke([p.Officer.Id]) ?? availableSoldiers) > 0)
                .OrderByDescending(p => objective?.RequiresCombat == false
                    ? -CaptureRisk(p, BattleRank.Rear)
                    : (long)Math.Min(sourceCapacity?.Invoke([p.Officer.Id]) ?? availableSoldiers,
                        Math.Min(availableSoldiers, CommandCapacity(p))) * (p.Abilities.Leadership + OrdinaryCoefficient(active)))
                .ThenBy(p => p.Officer.Id).FirstOrDefault();
            if (leader is null) continue;
            if (seed.Count < capacity && !seed.Contains(leader)) seed.Add(leader);
            foreach (var candidate in people.Where(p => !seed.Contains(p))
                .OrderByDescending(p => equipment.Choose(p, active, BattleRank.Front, Math.Max(1, capacity),
                    Math.Min(availableSoldiers, CommandCapacity(leader)), leader.Abilities.Leadership).Value)
                .ThenByDescending(p => p.Abilities.Leadership).ThenBy(p => p.Officer.Id))
            {
                if (seed.Count >= capacity) break;
                seed.Add(candidate);
            }
            // Compare legal prefixes: a small mission need not consume every available member slot.
            for (int count = 1; count <= seed.Count; count++)
            {
                var members = seed.Take(count).ToArray();
                if (required.Any(t => !members.Any(p => Has(p, t)))) continue;
                baselines++;
                var option = Evaluate(pair, members);
                if (option is not null && Better(option, baseline)) baseline = option;
            }
        }
        best = baseline;
        if (best is not null)
        {
            var candidates = people.OrderByDescending(p => p.Abilities.Leadership).ThenBy(p => p.Officer.Id).ToArray();
            // Remove, add, replace and cross-row changes all spend the same bounded improvement allowance.
            bool changed = true;
            while (changed && improvements < maximumImprovements)
            {
                changed = false;
                var current = best;
                var pair = new MilitaryFormationPair(current.FormationId, current.NavalFormationId);
                var members = current.Members.Select(m => byId[m.OfficerId]).ToArray();
                using var rosterMoves = Variants(members, candidates, pair).Where(v => v.Length > 0).GetEnumerator();
                using var positionMoves = PositionVariants(current, pair).GetEnumerator();
                bool roster = true, positions = true;
                while ((roster || positions) && improvements < maximumImprovements)
                {
                    if (roster && (roster = rosterMoves.MoveNext()))
                    {
                        improvements++;
                        var option = Evaluate(pair, rosterMoves.Current);
                        if (option is not null && Better(option, best)) { best = option; changed = true; }
                    }
                    if (improvements >= maximumImprovements) break;
                    if (positions && (positions = positionMoves.MoveNext()))
                    {
                        improvements++;
                        var option = Evaluate(pair, members, positionMoves.Current);
                        if (option is not null && Better(option, best)) { best = option; changed = true; }
                    }
                }
            }
        }
        return new(baseline, best, baselines, improvements, equipment.TacticsChecked, positionsChecked,
            best is not null && improvements == maximumImprovements);

        bool Has(MilitaryOfficerCandidate p, int t) => p.AccessibleTactics.Contains(t) || p.Officer.LearnedTactics.Contains(t);
        uint CommandCapacity(MilitaryOfficerCandidate p) => CommanderCapacity.Read(p.Officer.Id == force.LeaderId,
            force.RankId is int rank && rankCapacities.TryGetValue(rank, out uint limit) ? limit : null,
            p.Officer.OfficeId is int office && governance.Offices.TryGetValue(office, out var appointment) ? appointment.CommandCapacity : null);
        int OrdinaryCoefficient(FormationRule f) => role switch
        {
            MilitaryGroupRole.Siege => (f.Combat?.AgainstGarrison ?? 0) + (f.Combat?.AgainstWalls ?? 0),
            MilitaryGroupRole.Mobile => f.MovementBudget * 2,
            _ => (f.Combat?.AgainstUnits ?? 0) + (f.Combat?.Defense ?? 0)
        };
        IEnumerable<MilitaryOfficerCandidate[]> Variants(MilitaryOfficerCandidate[] members, MilitaryOfficerCandidate[] candidates, MilitaryFormationPair pair)
        {
            foreach (var member in members) yield return members.Where(p => p != member).ToArray();
            int capacity = Math.Min(rules.Formations[pair.Land].OfficerCapacity, pair.Naval is int naval ? rules.Formations[naval].OfficerCapacity : int.MaxValue);
            foreach (var candidate in candidates.Where(p => !members.Contains(p)))
            {
                if (members.Length < capacity) yield return members.Append(candidate).ToArray();
                for (int slot = 0; slot < members.Length; slot++)
                {
                    var replacement = members.ToArray(); replacement[slot] = candidate;
                    yield return replacement;
                }
            }
        }
        IEnumerable<IReadOnlyDictionary<int, FormationPosition>> PositionVariants(MilitaryGroupOption option, MilitaryFormationPair pair)
        {
            foreach (var first in option.Members)
            {
                foreach (var second in option.Members.Where(p => p.OfficerId > first.OfficerId && p.Position.Rank != first.Position.Rank))
                {
                    var swapped = option.Members.ToDictionary(p => p.OfficerId, p => p.Position);
                    (swapped[first.OfficerId], swapped[second.OfficerId]) = (swapped[second.OfficerId], swapped[first.OfficerId]);
                    yield return swapped;
                }
                if (rules.Formation(pair.Land, pair.Naval, expectedPosition).Id != pair.Land) continue;
                foreach (var slot in rules.Formations[pair.Land].Positions.Where(p => p.Rank != first.Position.Rank && !option.Members.Any(m => m.Position == p)))
                {
                    var moved = option.Members.ToDictionary(p => p.OfficerId, p => p.Position);
                    moved[first.OfficerId] = slot;
                    yield return moved;
                }
            }
        }
        MilitaryGroupOption? Evaluate(MilitaryFormationPair pair, MilitaryOfficerCandidate[] members,
            IReadOnlyDictionary<int, FormationPosition>? assigned = null)
        {
            var land = rules.Formations[pair.Land];
            var active = rules.Formation(pair.Land, pair.Naval, expectedPosition);
            if (members.Length == 0 || land.Positions.Length < members.Length || active.Positions.Length < members.Length ||
                members.Length > land.OfficerCapacity || pair.Naval is int boat && members.Length > rules.Formations[boat].OfficerCapacity ||
                Missing(land) || pair.Naval is int requiredShip && Missing(rules.Formations[requiredShip])) return null;
            var commanderId = governance.SelectCommander(members.Select(p => p.Officer), abilities)!.Id;
            var commander = byId[commanderId];
            uint soldiers = Math.Min(availableSoldiers, Math.Min(CommandCapacity(commander), Math.Min(land.SoldierCapacity,
                pair.Naval is int naval ? rules.Formations[naval].SoldierCapacity : uint.MaxValue)));
            if (sourceCapacity is not null) soldiers = Math.Min(soldiers, sourceCapacity(members.Select(p => p.Officer.Id).ToArray()));
            if (soldiers == 0) return null;
            long money = land.DeploymentCost + (pair.Naval is int ship ? (long)rules.Formations[ship].DeploymentCost : 0);
            if (money > availableMoney) return null;
            var landSlots = land.Positions.OrderBy(p => p.Rank).ThenBy(p => p.Column).ToList();
            var navalSlots = active.Id == land.Id ? null : active.Positions.OrderBy(p => p.Rank).ThenBy(p => p.Column).Take(members.Length).ToArray();
            var slots = navalSlots is null ? landSlots.ToList() : landSlots.Take(members.Length).ToList();
            BattleRank Rank(FormationPosition p) => navalSlots is null ? p.Rank : navalSlots[landSlots.IndexOf(p)].Rank;
            var choices = new List<MilitaryGroupMember>();
            long tacticValue = 0, tacticDamage = 0, risk = 0;
            foreach (var person in members.OrderByDescending(p => equipment.Choose(p, active, BattleRank.Front, members.Length, soldiers, commander.Abilities.Leadership).Value).ThenBy(p => p.Officer.Id))
            {
                positionsChecked++;
                var position = assigned is not null ? assigned[person.Officer.Id] : slots
                    .OrderByDescending(p => equipment.Choose(person, active, Rank(p), members.Length, soldiers, commander.Abilities.Leadership).Value)
                    .ThenBy(p => CaptureRisk(person, Rank(p))).ThenBy(p => p.Rank)
                    .ThenBy(p => Math.Abs(2L * p.Column - land.Positions.Min(s => s.Column) - land.Positions.Max(s => s.Column))).ThenBy(p => p.Column).First();
                slots.Remove(position);
                var selected = equipment.Choose(person, active, Rank(position), members.Length, soldiers, commander.Abilities.Leadership);
                choices.Add(new(person.Officer.Id, selected.Tactic?.Id, position));
                tacticValue += selected.Value;
                tacticDamage += selected.AttackDamage;
                risk += CaptureRisk(person, Rank(position));
            }
            long score;
            long loss = 0;
            if (role == MilitaryGroupRole.Mobile) score = active.MovementBudget;
            else if (objective is not null && target is not null)
            {
                var travel = rules.Travel(pair.Land, pair.Naval, soldiers);
                // Formation screening uses an optimistic travel bound; the planner verifies the chosen route.
                int arrival = NavigationArrivalForecast.EarliestDays(travel, objective.SourcePosition, target.Area,
                    maximumRange: active.Attack is { } reach ? reach.Range + reach.MaximumElevationBonus : 1);
                long combatDays = 1;
                if (objective.RequiresCombat)
                {
                    if (active.Combat is not { } attack || TargetProfile(target) is not { } defense) return null;
                    var own = new CombatStrength(soldiers, morale, commander.Abilities.Leadership, 1, 10);
                    var enemy = new CombatStrength(target.Soldiers, target.Morale, 70, 1, 10);
                    long troopDamage = CombatMath.TroopDamage(own, enemy, attack, defense, settlement, settlement ? target.Durability : 0);
                    long walls = settlement && active.Attack?.DamagesWalls == true ? CombatMath.DurabilityDamage(soldiers, commander.Abilities.Leadership, attack.AgainstWalls, target.Durability) : 0;
                    combatDays = settlement && !objective.CanHostPersons ? Days((uint)target.Durability, walls) :
                        Math.Min(Days(target.Soldiers, troopDamage + tacticDamage),
                            settlement ? Days((uint)target.Durability, walls) : long.MaxValue / 4);
                    loss = CombatMath.TroopDamage(enemy, own, defense, attack, false, 0);
                }
                score = -(arrival + combatDays);
            }
            else score = soldiers / 100 * (long)(OrdinaryCoefficient(active) + commander.Abilities.Leadership) + tacticValue;
            return new(pair.Land, pair.Naval, commanderId, soldiers, money, score, choices.OrderBy(p => p.OfficerId).ToImmutableArray())
                { ExpectedLoss = loss, OfficerRisk = risk };
            bool Missing(FormationRule f) => f.RequiredTacticId is int t && !force.ProvidedTactics.Contains(t) && !members.Any(p => Has(p, t));
        }
        CombatProfile? TargetProfile(MilitaryPresence p) => !p.Object.IsSettlement && p.FormationId is int formation
            ? rules.Formation(formation, p.NavalFormationId, p.Area.Position).Combat
            : p.FacilityTypeId is int facility ? rules.Facilities[facility].Combat : rules.Places.GetValueOrDefault(p.Object.Id)?.Combat;
        static long Days(uint remaining, long damage) => remaining == 0 || damage <= 0 ? long.MaxValue / 4 :
            (remaining + damage - 1) / damage;
        static long CaptureRisk(MilitaryOfficerCandidate person, BattleRank rank) =>
            BattleCaptureRules.Chance(person.Abilities.War, 1, rank, true, false, false, 0) * (long)(person.Abilities.Leadership + person.Abilities.Intelligence);

    }

    private static bool Better(MilitaryGroupOption option, MilitaryGroupOption? other)
    {
        if (other is null) return true;
        if (option.Score != other.Score) return option.Score > other.Score;
        if (option.ExpectedLoss != other.ExpectedLoss) return option.ExpectedLoss < other.ExpectedLoss;
        if (option.Money != other.Money) return option.Money < other.Money;
        if (option.Members.Length != other.Members.Length) return option.Members.Length < other.Members.Length;
        if (option.OfficerRisk != other.OfficerRisk) return option.OfficerRisk < other.OfficerRisk;
        if (option.Soldiers != other.Soldiers) return option.Soldiers < other.Soldiers;
        if (option.FormationId != other.FormationId) return option.FormationId < other.FormationId;
        if (option.NavalFormationId != other.NavalFormationId) return Nullable.Compare(option.NavalFormationId, other.NavalFormationId) < 0;
        if (option.CommanderId != other.CommanderId) return option.CommanderId < other.CommanderId;
        return false;
    }
}
