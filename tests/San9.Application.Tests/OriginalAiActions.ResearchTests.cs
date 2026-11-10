using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using San9.Application.AI;
using San9.Application.Persistence;
using San9.Application.Presentation;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed partial class OriginalAiActionsTests
{
    [Fact]
    public void LegacyTreasureOnlyCatalogAlsoReceivesBasicEquipmentRecipes()
    {
        var basis = Fixture();
        var treasure = new TreasureDefinition(99, "旧宝物", 1, 2, 0, null);
        var state = new TreasureState(99, 1, TreasureDiscovery.Discovered, null);
        var legacy = LegacyEquipmentMigration.Convert([treasure], [state]);
        var content = basis with { Information = basis.Information with
        {
            Treasures = [treasure], TreasureStates = [state], Equipment = legacy.Definitions
        } };
        var rules = new ScenarioSessionDefinition(content).Rules;
        Assert.Contains("basic-spear", rules.Equipment.Items.Keys);
        Assert.Contains(rules.Equipment.Items.Values, item => item.LegacyTreasureId == 99);
        Assert.Contains(EquipmentPreset.ResearchId, rules.Military!.ResearchProjects.Keys);
    }

    [Fact]
    public void UnknownLegacyTreasureCategoryRemainsPlayableAsTreasureSlot()
    {
        var basis = Fixture();
        var treasure = new TreasureDefinition(99, "legacy-item", 19, 2, 0, null);
        var state = new TreasureState(99, 1, TreasureDiscovery.Discovered, null);
        var content = basis with { Information = basis.Information with { Treasures = [treasure], TreasureStates = [state] } };
        var session = GameSession.Start(content, 42);
        var item = Assert.Single(session.Snapshot.Equipment, item => item.LegacyTreasureId == 99);
        Assert.Equal(EquipmentSlot.Treasure, new ScenarioSessionDefinition(content).Rules.Equipment.Items[item.DefinitionId].Slot);
        var texts = new San9.Content.Text.TextCatalog(San9.Content.Text.TextCatalog.CurrentSchemaVersion,
            new Dictionary<string, string> { ["legacy-item"] = "旧宝物", ["person.1"] = "君主" });
        Assert.Equal("宝物", Assert.Single(TreasureCatalog.Read(session.View, texts).Rows).Cells[1].Text);
    }

    [Fact]
    public void PlayerResearchesMakesEquipsAndRestoresEquipment()
    {
        var basis = Fixture();
        var content = basis with
        {
            Units = [],
            Officers = basis.Officers.Select(person => person.State.UnitId is null ? person : person with
            {
                State = person.State with { UnitId = null, SettlementId = person.State.OwnerId }
            }).ToArray()
        };
        var session = GameSession.Start(content, 42);
        Assert.True(session.Execute(new("player"), new StartResearchCommand(Guid.NewGuid(), 1,
            session.Snapshot.Simulation.NextSequence, 1, EquipmentPreset.ResearchId)).Submission.Accepted);
        for (int i = 0; i < 4 && !session.Snapshot.Forces.Single(f => f.Id == 1).CompletedResearch.Contains(EquipmentPreset.ResearchId); i++)
            session.AdvanceXun();
        Assert.Contains(EquipmentPreset.ResearchId, session.Snapshot.Forces.Single(f => f.Id == 1).CompletedResearch);
        Assert.True(session.Execute(new("player"), new StartEquipmentProductionCommand(Guid.NewGuid(), 1,
            session.Snapshot.Simulation.NextSequence, 1, "basic-spear")).Submission.Accepted);
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        for (int i = 0; i < 4 && session.Snapshot.Forces.Single(f => f.Id == 1).EquipmentProduction is not null; i++)
        {
            EventAssert.Equal(session.AdvanceXun().Events, restored.AdvanceXun().Events);
            StateAssert.Equal(session.Capture(), restored.Capture());
        }
        var item = Assert.Single(session.Snapshot.Equipment, e => e.DefinitionId == "basic-spear" && e.InventoryForceId == 1);
        Assert.True(session.Execute(new("player"), new ConfigureEquipmentCommand(Guid.NewGuid(), 1,
            session.Snapshot.Simulation.NextSequence, 1, EquipmentSlot.MainHand, item.Id)).Submission.Accepted);
        restored = SaveService.Decode(SaveService.Encode(session), content);
        Assert.True(restored.Snapshot.Equipment.Single(e => e.Id == item.Id).Equipped);
        StateAssert.Equal(session.Capture(), restored.Capture());
    }

    [Fact]
    public void AiResearchesEquipsAndProducesPersonalEquipmentWithoutMixedTroops()
    {
        var content = Fixture();
        var definition = new ScenarioSessionDefinition(content);
        var seat = new ControlSeat("ai:2", 2, ControllerKind.Automatic);
        var initial = definition.CreateWorld(42).CreateSnapshot();
        initial = initial with { Simulation = initial.Simulation with { Boundary = SimulationBoundary.Stable, Controls = [seat], ActiveControl = 0 } };
        var researching = GameSession.EngineFactory(content)(WorldState.Restore(initial));
        List<DomainEvent> researchEvents = [];
        OriginalAiResearch.TryStart(researching, definition.MilitaryRules, seat, researchEvents, definition.Rules.Equipment);
        Assert.Equal(EquipmentPreset.ResearchId, Assert.Single(researchEvents.OfType<ResearchStartedEvent>()).ProjectId);
        var ready = initial with
        {
            Forces = initial.Forces.Select(force => force.Id == 2 ? force with { CompletedResearch = [EquipmentPreset.ResearchId] } : force).ToArray(),
            Equipment = [.. initial.Equipment, new EquipmentInstance(500, "basic-spear", null, 2, false)]
        };
        var first = GameSession.EngineFactory(content)(WorldState.Restore(ready));
        var second = GameSession.EngineFactory(content)(WorldState.Restore(ready));
        List<DomainEvent> firstEvents = [], secondEvents = [];
        OriginalAiEquipment.Manage(first, definition.Rules.Equipment, seat, firstEvents);
        OriginalAiEquipment.Manage(second, definition.Rules.Equipment, seat, secondEvents);
        Assert.True(first.Snapshot.Equipment.Single(item => item.Id == 500).Equipped);
        Assert.NotNull(first.Snapshot.Forces.Single(force => force.Id == 2).EquipmentProduction);
        Assert.Contains(firstEvents, e => e is EquipmentProductionStartedEvent);
        StateAssert.Equal(first.Snapshot, second.Snapshot);
        EventAssert.Equal(firstEvents, secondEvents);
    }

    [Fact]
    public void ExistingFieldUnitsReceiveDeterministicDetachmentsAndAiResearchesTheirEquipment()
    {
        var basis = Fixture();
        var troopTypes = new[]
        {
            new TroopTypeDefinition(100, "枪兵", 2, ImmutableDictionary<int, int>.Empty)
                { LevelResearchProjects = ImmutableDictionary<int, string>.Empty.Add(2, "spear") },
            new TroopTypeDefinition(200, "盾兵", 2, ImmutableDictionary<int, int>.Empty)
                { LevelResearchProjects = ImmutableDictionary<int, string>.Empty.Add(2, "shield") }
        };
        var content = basis with { Military = basis.Military! with
        {
            TroopTypes = [.. troopTypes],
            ResearchProjects = [new("spear", 1000, 30, []), new("shield", 100, 30, [])]
        } };
        var definition = new ScenarioSessionDefinition(content);
        var settings = new NewGameSettings { MixedTroopsEnabled = true };
        var first = definition.CreateWorld(42, settings: settings).CreateSnapshot();
        var second = definition.CreateWorld(42, settings: settings).CreateSnapshot();
        var firstUnit = first.Units.Single(unit => unit.Id == 20);
        Assert.Equal(firstUnit.Detachments, second.Units.Single(unit => unit.Id == 20).Detachments);
        Assert.Equal(firstUnit.Soldiers, firstUnit.Detachments.Values.Single().Soldiers);
        Assert.Equal(100, firstUnit.Detachments.Values.Single().TroopTypeId);

        var seat = new ControlSeat("ai:2", 2, ControllerKind.Automatic);
        var state = first with { Simulation = first.Simulation with
        {
            Boundary = SimulationBoundary.Stable, Controls = [seat], ActiveControl = 0
        } };
        var engine = GameSession.EngineFactory(content)(WorldState.Restore(state));
        List<DomainEvent> events = [];
        OriginalAiResearch.TryStart(engine, definition.MilitaryRules, seat, events);
        Assert.Equal("spear", Assert.Single(events.OfType<ResearchStartedEvent>()).ProjectId);
        Assert.Equal("spear", engine.Snapshot.Forces.Single(force => force.Id == 2).ActiveResearch?.ProjectId);
    }

    [Fact]
    public void ExistingSiegeFormationBecomesEquipmentWithoutRequiringResearch()
    {
        var basis = Fixture();
        var original = basis.Units.Single(unit => unit.Id == 20);
        var siegeFormation = basis.Military!.Formations[0] with
        {
            Id = 401, Combat = new(10, 10, 10, 10, 50), Attack = new(10, true)
        };
        var content = basis with
        {
            Units = basis.Units.Select(unit => unit.Id == original.Id ? unit with { FormationId = 401 } : unit).ToArray(),
            Military = basis.Military with
            {
                Formations = [basis.Military.Formations[0], siegeFormation], DefaultDeploymentFormationId = 400,
                TroopTypes = [new(100, "枪兵", 1, ImmutableDictionary<int, int>.Empty)],
                ResearchProjects = [new("siege", 100, 30, [])],
                SiegeEquipment = [new(10, "冲车", "siege", 401, 100)]
            }
        };
        var world = new ScenarioSessionDefinition(content).CreateWorld(42,
            settings: new NewGameSettings { MixedTroopsEnabled = true }).CreateSnapshot();
        var unit = world.Units.Single(item => item.Id == original.Id);
        Assert.Equal(400, unit.FormationId);
        Assert.Equal(10, unit.SiegeEquipmentId);
        Assert.Equal(original.MemberPositions, unit.MemberPositions);
        Assert.Empty(world.Forces.Single(force => force.Id == original.OwnerId).CompletedResearch);
    }

    [Fact]
    public void PreviousShapeSaveDefaultsMixedRulesToDisabled()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 42);
        var json = JsonNode.Parse(SaveService.Encode(session))!;
        var world = json["session"]!["world"]!;
        world["simulation"]!["newGame"]!.AsObject().Remove("mixedTroopsEnabled");
        foreach (var force in world["forces"]!.AsArray())
        {
            force!.AsObject().Remove("completedResearch");
            force.AsObject().Remove("activeResearch");
        }
        foreach (var unit in world["units"]!.AsArray())
        {
            unit!.AsObject().Remove("detachments");
            unit.AsObject().Remove("navalTrainingId");
            unit.AsObject().Remove("siegeEquipmentId");
        }
        var restored = SaveService.Decode(Encoding.UTF8.GetBytes(json.ToJsonString()), content);
        Assert.False(restored.Snapshot.Simulation.NewGame.MixedTroopsEnabled);
        Assert.All(restored.Snapshot.Units, unit => Assert.Empty(unit.Detachments));
        Assert.All(restored.Snapshot.Forces, force => Assert.Empty(force.CompletedResearch));
    }

    [Fact]
    public void MixedResearchSurvivesSaveAndCommandLogReplay()
    {
        var basis = Fixture();
        var content = basis with { Military = basis.Military! with
        {
            TroopTypes = [new(100, "枪兵", 1, ImmutableDictionary<int, int>.Empty)],
            ResearchProjects = [new("training", 100, 30, [])]
        } };
        var settings = new NewGameSettings { MixedTroopsEnabled = true };
        var first = GameSession.Start(content, 42, settings: settings);
        var replay = GameSession.Start(content, 42, settings: settings);
        Guid id = Guid.NewGuid();
        var request = first.Request(new CommandOperation(new StartResearchCommand(id, 1,
            first.Snapshot.Simulation.NextSequence, 1, "training")), id: id);
        var path = Path.GetTempFileName();
        try
        {
            CommandLogService.Write(path, CommandLogService.CreateHeader(first), [request]);
            Assert.Equal(SessionDisposition.Accepted, first.Apply(request).Disposition);
            Assert.Equal(1, CommandLogService.Replay(path, replay).Accepted);
            var restored = SaveService.Decode(SaveService.Encode(first), content);
            StateAssert.Equal(first.Capture(), restored.Capture());
            StateAssert.Equal(first.Capture(), replay.Capture());
            Assert.Equal("training", restored.Snapshot.Forces.Single(force => force.Id == 1).ActiveResearch?.ProjectId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PreviousShapePresetDefaultsEquipmentAndMemberDetachments()
    {
        var preset = new DeploymentPreset(Guid.NewGuid(), 1, "原编队",
            [new DeploymentPresetMember(1, new FormationSlot(0, 0), null)], 400, null,
            DeploymentSoldierMode.Fixed, 100);
        var json = JsonNode.Parse(CanonicalJson.Encode(preset))!;
        json.AsObject().Remove("navalTrainingId");
        json.AsObject().Remove("siegeEquipmentId");
        json["members"]![0]!.AsObject().Remove("detachment");
        var restored = StrictJson.Deserialize<DeploymentPreset>(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Null(restored.NavalTrainingId);
        Assert.Null(restored.SiegeEquipmentId);
        Assert.Null(Assert.Single(restored.Members).Detachment);
    }
}
