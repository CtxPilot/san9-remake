using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI;

internal static class OriginalAiArmament
{
    internal static void Manage(SimulationEngine engine, MilitaryRulebook? rules, ControlSeat seat, List<DomainEvent> events)
    {
        var world = engine.Snapshot;
        if (seat.Kind != ControllerKind.Automatic || !world.Simulation.NewGame.MixedTroopsEnabled || rules is null || rules.Armaments.IsEmpty) return;
        var force = world.Forces.FirstOrDefault(item => item.Id == seat.ForceId && !item.IsDormant && !item.IsEliminated);
        var root = world.Corps.FirstOrDefault(item => item.OwnerId == seat.ForceId && item.IsRoot);
        if (force is null || root is null) return;
        var context = new CommandContext(root.ControlMode == CorpsControlMode.Automatic ? CorpsControl.ControllerId(root.Id) : seat.ControllerId);
        foreach (int unitId in world.Units.Where(unit => unit.OwnerId == force.Id && !unit.Detachments.IsEmpty)
                     .OrderBy(unit => unit.Id).Select(unit => unit.Id).Take(10))
        {
            long sequence = engine.Snapshot.Simulation.NextSequence;
            var command = new ResupplyUnitArmamentsCommand(SimulationIdentity.Command(sequence, force.Id), force.Id, sequence, unitId);
            if (engine.Validate(context, command).Accepted)
            {
                var result = engine.Execute(context, command);
                if (result.Submission.Accepted) events.AddRange(result.Events);
            }
            var currentUnit = engine.Snapshot.Units.Single(item => item.Id == unitId);
            foreach (var (officerId, detachment) in currentUnit.Detachments.OrderBy(pair => pair.Key))
            foreach (var slot in Enum.GetValues<ArmamentSlot>())
            {
                if ((slot == ArmamentSlot.Weapon ? detachment.Weapon : detachment.Armor) is not null || detachment.Soldiers == 0) continue;
                var owner = engine.Snapshot.Forces.Single(item => item.Id == seat.ForceId);
                var issueChoice = rules.Armaments.Values.Where(item => item.Slot == slot && item.Supports(detachment.TroopTypeId) &&
                        (owner.ArmamentInventory?.GetValueOrDefault(item.Id) ?? 0) > 0 &&
                        (item.ResearchProjectId is null || owner.CompletedResearch.Contains(item.ResearchProjectId)))
                    .OrderByDescending(item => slot == ArmamentSlot.Weapon ? item.AttackBonus : item.DefenseBonus)
                    .ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
                if (issueChoice is null) continue;
                sequence = engine.Snapshot.Simulation.NextSequence;
                var configure = new ConfigureUnitArmamentCommand(SimulationIdentity.Command(sequence, force.Id), force.Id, sequence,
                    unitId, officerId, slot, issueChoice.Id);
                if (!engine.Validate(context, configure).Accepted) continue;
                var configured = engine.Execute(context, configure);
                if (configured.Submission.Accepted) events.AddRange(configured.Events);
            }
        }
        world = engine.Snapshot;
        force = world.Forces.Single(item => item.Id == seat.ForceId);
        var place = world.Settlements.Where(item => item.OwnerId == force.Id && !item.IsDormant && !item.IsDestroyed && item.CanHostPersons &&
                (item.CorpsId is null || item.CorpsId == root.Id) && CorpsControl.CanControlSettlement(world, seat, context, item.Id))
            .OrderBy(item => item.Id).FirstOrDefault();
        if (place is null) return;
        long soldiers = world.Settlements.Where(item => item.OwnerId == force.Id).Sum(item => (long)item.Soldiers) +
            world.Units.Where(item => item.OwnerId == force.Id).Sum(item => (long)item.Soldiers);
        if (soldiers == 0) return;
        long Stock(ArmamentSlot slot) => (force.ArmamentInventory ?? System.Collections.Immutable.ImmutableDictionary<string, uint>.Empty)
            .Where(pair => rules.Armaments.TryGetValue(pair.Key, out var item) && item.Slot == slot).Sum(pair => (long)pair.Value) +
            (force.ArmamentProduction is { } progress && rules.Armaments.TryGetValue(progress.DefinitionId, out var pending) && pending.Slot == slot
                ? (long)progress.Batches * ArmamentInventory.SetsPerBatch : 0);
        var choice = rules.Armaments.Values.Where(item => item.ResearchProjectId is null || force.CompletedResearch.Contains(item.ResearchProjectId))
            .Where(item => Stock(item.Slot) < Math.Min(soldiers, 5000))
            .OrderBy(item => Stock(item.Slot)).ThenBy(item => item.PurchaseCostPerThousand)
            .ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        if (choice is null) return;
        if (force.ArmamentProduction is null && choice.ProductionCostPerThousand is int cost && cost <= root.Treasury.Money / 4)
        {
            long sequence = engine.Snapshot.Simulation.NextSequence;
            var command = new StartArmamentProductionCommand(SimulationIdentity.Command(sequence, force.Id), force.Id, sequence, place.Id, choice.Id, 1);
            if (engine.Validate(context, command).Accepted)
            {
                var result = engine.Execute(context, command);
                if (result.Submission.Accepted) events.AddRange(result.Events);
            }
        }
        world = engine.Snapshot;
        force = world.Forces.Single(item => item.Id == seat.ForceId);
        root = world.Corps.Single(item => item.Id == root.Id);
        if (Stock(choice.Slot) >= 1000 || choice.PurchaseCostPerThousand > (long)root.Treasury.Money / 4 + (force.ArmamentMigrationCredit ?? 0)) return;
        long next = world.Simulation.NextSequence;
        var purchase = new PurchaseArmamentCommand(SimulationIdentity.Command(next, force.Id), force.Id, next, place.Id, choice.Id, 1);
        if (!engine.Validate(context, purchase).Accepted) return;
        var bought = engine.Execute(context, purchase);
        if (bought.Submission.Accepted) events.AddRange(bought.Events);
    }
}
