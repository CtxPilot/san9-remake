using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.AI;

/// <summary>Uses the same commands and corps authority as a player; choices are deterministic.</summary>
internal static class OriginalAiEquipment
{
    internal static void Manage(SimulationEngine engine, EquipmentRulebook rules, ControlSeat seat, List<DomainEvent> events)
    {
        var initial = engine.Snapshot;
        if (seat.Kind != ControllerKind.Automatic || !initial.EquipmentInitialized || rules.Items.IsEmpty) return;
        var root = initial.Corps.FirstOrDefault(c => c.OwnerId == seat.ForceId && c.IsRoot);
        if (root is null) return;
        var context = new CommandContext(root.ControlMode == CorpsControlMode.Automatic ? CorpsControl.ControllerId(root.Id) : seat.ControllerId);
        var officers = initial.Officers.Where(p => p.OwnerId == seat.ForceId && p.IsServing && !p.IsDormant)
            .OrderByDescending(p => (long)p.BaseAbilities.Leadership + p.BaseAbilities.War)
            .ThenBy(p => p.Id).Select(p => p.Id).ToArray();
        int actions = 0;
        long Score(EquipmentInstance item)
        {
            var definition = rules.Items[item.DefinitionId]; var b = definition.BaseBonuses; var g = definition.BonusPerLevel;
            long score = (long)b.War + b.Intelligence + b.Politics + b.Leadership +
                ((long)g.War + g.Intelligence + g.Politics + g.Leadership) * (item.Level - 1);
            return score + (definition.TacticId is null ? 0 : 10) + (definition.EscapeMount ? 10 : 0);
        }
        foreach (int officer in officers)
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            if (actions >= 32) break;
            var world = engine.Snapshot;
            var current = world.Equipment.FirstOrDefault(item => item.OfficerId == officer && item.Equipped && rules.Items[item.DefinitionId].Slot == slot);
            long currentScore = current is null ? -1 : Score(current);
            foreach (var item in world.Equipment.Where(item => (item.OfficerId == officer || item.InventoryForceId == seat.ForceId) &&
                         !item.Equipped && rules.Items[item.DefinitionId].Slot == slot && Score(item) > currentScore)
                     .OrderByDescending(Score).ThenBy(item => item.Id))
            {
                long sequence = engine.Snapshot.Simulation.NextSequence;
                var command = new ConfigureEquipmentCommand(SimulationIdentity.Command(sequence, seat.ForceId), seat.ForceId, sequence, officer, slot, item.Id);
                if (!engine.Validate(context, command).Accepted) continue;
                var result = engine.Execute(context, command);
                if (result.Submission.Accepted) { events.AddRange(result.Events); actions++; }
                break;
            }
        }
        var snapshot = engine.Snapshot;
        var force = snapshot.Forces.Single(f => f.Id == seat.ForceId);
        if (force.EquipmentProduction is not null) return;
        root = snapshot.Corps.First(c => c.Id == root.Id);
        var place = snapshot.Settlements.Where(p => p.OwnerId == force.Id && !p.IsDormant && !p.IsDestroyed && p.CanHostPersons &&
                (p.CorpsId is null || p.CorpsId == root.Id) && CorpsControl.CanControlSettlement(snapshot, seat, context, p.Id))
            .OrderBy(p => p.Id).FirstOrDefault();
        if (place is null) return;
        int Need(EquipmentSlot slot) => officers.Count(id => !snapshot.Equipment.Any(item => item.OfficerId == id && item.Equipped &&
            rules.Items[item.DefinitionId].Slot == slot)) - snapshot.Equipment.Count(item => item.InventoryForceId == force.Id &&
            rules.Items[item.DefinitionId].Slot == slot);
        var recipe = rules.Items.Values.Where(item => item.ResearchProjectId is string research && force.CompletedResearch.Contains(research) &&
                item.ProductionCost <= root.Treasury.Money / 2 && Need(item.Slot) > 0)
            .OrderByDescending(item => Need(item.Slot)).ThenBy(item => item.ProductionCost).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        if (recipe is null) return;
        long next = snapshot.Simulation.NextSequence;
        var produce = new StartEquipmentProductionCommand(SimulationIdentity.Command(next, seat.ForceId), seat.ForceId, next, place.Id, recipe.Id);
        var started = engine.Execute(context, produce);
        if (started.Submission.Accepted) events.AddRange(started.Events);
    }
}
