using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI.Military;

internal sealed partial class MilitaryCorpsPlanner
{
    private void EquipExistingUnits(MilitaryTravelQueries queries)
    {
        var world = engine.Snapshot;
        var force = world.Forces.Single(f => f.Id == corps.OwnerId);
        foreach (var unit in world.Units.Where(u => Controls(u) && !u.IsEmbedded && !u.IsPersonnel))
        {
            if (rules.TacticFormation(unit) is null) continue;
            var goal = unit.EffectiveOrder switch
            {
                { Kind: MilitaryOrderKind.AttackSettlement, SettlementId: int place } => new MapObjectReference(place, true),
                { Kind: MilitaryOrderKind.AttackUnit, UnitId: int enemy } => new MapObjectReference(enemy, false),
                _ => null
            };
            if (goal is null || queries.Observation.Forces.Find(goal) is not { } target) continue;
            var formation = rules.Formation(unit, target.Area.Position);
            var people = world.Officers.Where(p => p.UnitId == unit.Id && p.IsServing).ToArray();
            int leadership = unit.CommanderId is int leader ? engine.GetAbilities(world)[leader].Leadership : 0;
            var equipment = new MilitaryGroupEquipment(rules, engine.Companion, goal.IsSettlement, target, RelevantEquipment(queries, target));
            foreach (var person in people)
            {
                var own = MilitaryGroupEvaluator.ObserveOwnOfficer(person, force, definition.Rules.Abilities, world.Treasures, definition.Rules.Treasures, rules);
                var selected = equipment.Choose(own, formation, rules.Rank(unit, person.Id, target.Area.Position), people.Length, unit.Soldiers, leadership).Tactic?.Id;
                if (selected == person.EquippedTacticId) continue;
                var identity = Identity();
                Submit(new SelectTacticCommand(identity.Id, corps.OwnerId, identity.Sequence, person.Id, selected));
            }
        }
    }
}
