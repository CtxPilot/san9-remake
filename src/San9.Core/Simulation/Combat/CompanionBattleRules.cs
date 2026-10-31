using San9.Core.Domain;

namespace San9.Core.Simulation;

public sealed record LeaderInspiredEvent(GameClock Clock, int ActorId, int TacticId,
    TacticOccurrence Occurrence, BattleObjectContext Opponent) : DomainEvent;

/// <summary>Effective companion combat rules; authored definitions remain immutable.</summary>
internal static class CompanionBattleRules
{
    internal static int LinkMultiplier(WorldMutation world, FormationRule formation, MilitaryRulebook military)
    {
        var exchange = world.Companion.RedistributeFormationTacticBonuses ? military.CompanionCombat?.LinkBonusExchange : null;
        int? other = exchange is null ? null : formation.Id == exchange.FirstId ? exchange.SecondId :
            formation.Id == exchange.SecondId ? exchange.FirstId : null;
        return other is int id ? military.Formations[id].Tactics!.LinkMultiplier : formation.Tactics!.LinkMultiplier;
    }

    internal static int SnipingBonus(WorldMutation world, FormationRule formation, MilitaryRulebook military)
    {
        var exchange = world.Companion.RedistributeFormationTacticBonuses ? military.CompanionCombat?.SnipingBonusExchange : null;
        int? other = exchange is null ? null : formation.Id == exchange.FirstId ? exchange.SecondId :
            formation.Id == exchange.SecondId ? exchange.FirstId : null;
        return other is int id ? military.Formations[id].SnipingChanceBonus : formation.SnipingChanceBonus;
    }

    internal static bool PassesNavalTraining(WorldMutation world, UnitState unit, OfficerState actor, SimulationRules rules)
    {
        if (!world.Companion.RequireNavalTacticTraining) return true;
        var training = rules.Military!.CompanionCombat?.NavalTraining.FirstOrDefault(n => n.FormationId == rules.Military.Formation(unit).Id);
        return training is null || training.LearnedTacticIds.Any(id =>
            MilitaryTactics.HasTactic(world, unit.OwnerId, [actor.Id], id, rules.Treasures)) ||
            world.NextRandom("military-tactic-naval-training") % 2 != 0;
    }

    internal static int SiegeCoefficient(WorldMutation world, UnitState unit, int coefficient, MilitaryRulebook military)
    {
        if (!world.Companion.ProficiencyAffectsSiegePower || military.CompanionCombat?.SiegeProficiency is not { } rule ||
            !rule.FormationIds.Contains(military.Formation(unit).Id)) return coefficient;
        int proficiency = world.ServingUnitMembers(unit.Id).Select(p => p.TacticProficiencies[rule.Category]).DefaultIfEmpty(0).Max();
        long numerator = ((long)proficiency - 600) * coefficient;
        long delta = numerator >= 0 ? numerator / 2048 : -((-numerator + 2047) / 2048);
        return (int)Math.Clamp(coefficient + delta, 0, int.MaxValue);
    }

    internal static int Cooldown(WorldMutation world, BattleSource source, TacticRule tactic) =>
        source.IsSettlement && world.Companion.HalveFacilityIntelligenceCooldown && tactic.Effect != TacticEffect.Martial
            ? tactic.MinimumInterval / 2 : tactic.MinimumInterval;

    internal static void InspireLeader(WorldMutation world, BattleSource source, BattleSource opponent,
        SimulationRules rules, List<DomainEvent> events)
    {
        if (!world.Companion.LeadersInspireInCombat || rules.Military!.CompanionCombat?.LeaderInspirationTacticId is not int tacticId ||
            !source.IsCombatant(world) || source.Soldiers(world) == 0 || source.Morale(world) >= 70) return;
        OfficerState? commander;
        if (source.IsSettlement)
        {
            if (world.Settlements[source.Id].ConfusionDaysRemaining > 0) return;
            commander = LeadershipMaintenance.Commander(world, source.Id);
        }
        else
        {
            var unit = world.Units[source.Id];
            if (unit.Condition != UnitCondition.Normal) return;
            commander = unit.CommanderId is int id ? world.Officers[id] : null;
        }
        if (commander is not { Injury: 0, Status: OfficerStatus.Ruler or OfficerStatus.Governor } ||
            !TacticCombat.Roll(world, "military-leader-inspire", 4)) return;
        var ability = world.Abilities(commander);
        int delta = (int)Math.Min(100, Math.Max(6, ((long)ability.War + ability.Intelligence + ability.Politics + ability.Leadership) / 24));
        int previous = source.Morale(world);
        var occurrence = TacticOccurrence.Capture(world, source, MilitaryCombat.TargetOrder(source), rules.Military.Tactics[tacticId],
            rules.Military, rules.Treasures);
        source.Reward(world, 0, delta);
        events.Add(new LeaderInspiredEvent(world.Clock, commander.Id, tacticId, occurrence, BattleObjectContext.Capture(world, opponent)));
        events.Add(new TacticMoraleChangedEvent(world.Clock, source.Id, source.IsSettlement, previous, source.Morale(world), tacticId, occurrence));
    }
}
