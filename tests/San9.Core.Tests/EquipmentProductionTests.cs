using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Core.Tests;

public sealed class EquipmentProductionTests
{
    private static readonly EquipmentRulebook Catalog = new([new EquipmentDefinition("spear", EquipmentSlot.MainHand,
        EquipmentRarity.Ordinary, new(2, 0, 0, 0), new(1, 0, 0, 0))
        { Name = "枪", ResearchProjectId = "forge", ProductionCost = 200, ProductionDays = 30 }]);

    private static SimulationEngine Engine(bool researched = true, uint funds = 1000)
    {
        var force = new ForceState(1, 0) { LeaderId = 1, CompletedResearch = researched ? ["forge"] : [] };
        var world = WorldState.Create(new(190, 1, 1), 5, [force],
            [new SettlementState(1, 1, 0, 100, 100, 0, 0, 0, 100, 0, 100)],
            [new OfficerState(1, 1, 1, new(50, 50, 50, 50), false) { Status = OfficerStatus.Ruler }],
            controls: [new("player", 1, ControllerKind.Player)],
            corps: [new CorpsState(1, 1, 1, new(funds, 1000)) { IsRoot = true }], equipment: []);
        return new SimulationEngine(world, new SimulationRules { Equipment = Catalog });
    }

    [Fact]
    public void ProductionRequiresResearchAndFundsAndRejectsDuplicateOrder()
    {
        var command = new StartEquipmentProductionCommand(Guid.NewGuid(), 1, 0, 1, "spear");
        Assert.Equal(CommandRejection.InvalidTask, Engine(researched: false).Execute(new("player"), command).Submission.Rejection);
        Assert.Equal(CommandRejection.InsufficientFunds, Engine(funds: 100).Execute(new("player"), command).Submission.Rejection);
        var engine = Engine();
        Assert.True(engine.Execute(new("player"), command).Submission.Accepted);
        Assert.Equal(800u, engine.Snapshot.Corps.Single().Treasury.Money);
        var next = command with { CommandId = Guid.NewGuid(), Sequence = engine.Snapshot.Simulation.NextSequence };
        Assert.Equal(CommandRejection.InvalidTask, engine.Execute(new("player"), next).Submission.Rejection);
    }

    [Fact]
    public void EquipmentResearchWorksWithoutMixedTroopMode()
    {
        var state = WorldState.Restore(Engine(researched: false).CaptureStableState());
        var world = state.BeginMutation(equipment: Catalog);
        var military = new MilitaryRulebook(new MilitaryDefinition(1, 1, [0], [new(0, 1, null)], [], [])
        { ResearchProjects = [new("forge", 100, 30, [])] });
        var rules = new SimulationRules { Equipment = Catalog, Military = military };
        Assert.False(world.Simulation.NewGame.MixedTroopsEnabled);
        Assert.Equal(CommandRejection.None, MilitaryResearch.Start(world,
            new StartResearchCommand(Guid.NewGuid(), 1, 0, 1, "forge"), rules, []));
        for (int i = 0; i < 30; i++) MilitaryResearch.AdvanceDay(world, military, []);
        Assert.Contains("forge", world.Forces[1].CompletedResearch);
        Assert.Equal(CommandRejection.None, EquipmentProduction.Start(world,
            new StartEquipmentProductionCommand(Guid.NewGuid(), 1, 1, 1, "spear"), Catalog, []));
    }

    [Fact]
    public void ThirtyDaysProduceIndependentInventoryInstanceAcrossRestore()
    {
        var engine = Engine();
        Assert.True(engine.Execute(new("player"), new StartEquipmentProductionCommand(Guid.NewGuid(), 1, 0, 1, "spear")).Submission.Accepted);
        var world = WorldState.Restore(engine.CaptureStableState());
        var first = world.BeginMutation(equipment: Catalog);
        for (int i = 0; i < 15; i++) EquipmentProduction.AdvanceDay(first, Catalog, []);
        world.Commit(first);
        world = WorldState.Restore(world.CreateSnapshot());
        var second = world.BeginMutation(equipment: Catalog);
        for (int i = 0; i < 15; i++) EquipmentProduction.AdvanceDay(second, Catalog, []);
        Assert.Null(second.Forces[1].EquipmentProduction);
        var item = Assert.Single(second.AllEquipment.Values);
        Assert.Equal(1, item.InventoryForceId);
        Assert.False(item.Equipped);
        Assert.Equal("spear", item.DefinitionId);
        world.Commit(second);
        var completed = new SimulationEngine(world, new SimulationRules { Equipment = Catalog });
        Assert.True(completed.Execute(new("player"), new ConfigureEquipmentCommand(Guid.NewGuid(), 1,
            completed.Snapshot.Simulation.NextSequence, 1, EquipmentSlot.MainHand, item.Id)).Submission.Accepted);
        Assert.Equal(52, completed.GetAbilities(completed.Snapshot)[1].War);
        Assert.True(completed.Execute(new("player"), new TransferEquipmentCommand(Guid.NewGuid(), 1,
            completed.Snapshot.Simulation.NextSequence, item.Id, null)).Submission.Accepted);
        Assert.Equal(50, completed.GetAbilities(completed.Snapshot)[1].War);
    }

    [Fact]
    public void OwnershipChangeCancelsProduction()
    {
        var engine = Engine();
        Assert.True(engine.Execute(new("player"), new StartEquipmentProductionCommand(Guid.NewGuid(), 1, 0, 1, "spear")).Submission.Accepted);
        var world = WorldState.Restore(engine.CaptureStableState()).BeginMutation(equipment: Catalog);
        List<DomainEvent> events = [];
        MilitaryResearch.CancelAt(world, 1, events);
        Assert.Null(world.Forces[1].EquipmentProduction);
        Assert.Single(events.OfType<EquipmentProductionCancelledEvent>());
        Assert.Empty(world.AllEquipment);
    }
}
