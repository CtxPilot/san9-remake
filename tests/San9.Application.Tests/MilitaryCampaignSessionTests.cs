using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class MilitaryCampaignSessionTests
{
    [Fact]
    public void AutomaticEntryDeploysFromPortAndRetainsSourceWhenTheCorridorChangesTheTarget()
    {
        var session = GameSession.Start(Fixture(), 281);
        var result = session.EndControl(new("player"));
        var deployed = Assert.Single(result.Events.OfType<UnitDeployedEvent>());
        Assert.Equal(1001, deployed.SettlementId);
        var campaign = Assert.Single(session.MilitaryCampaigns.Campaigns);
        Assert.Equal(1001, campaign.SourceId);
        Assert.Equal(1003, campaign.ObjectiveId);
        Assert.Equal(1002, campaign.TargetId);
        Assert.Equal(campaign.UnitId, deployed.UnitId);
        Assert.Equal(1002, session.Snapshot.Units.Single(u => u.Id == deployed.UnitId).Order!.SettlementId);
        Assert.Single(session.LastOriginalAiWork.Where(r => r.Executor == "military-campaign-v1"));
        Assert.DoesNotContain(session.LastOriginalAiWork, r => r.Executor == "original" &&
            r.Work is OriginalAiWork.Attack or OriginalAiWork.Deploy or OriginalAiWork.Retreat or OriginalAiWork.Transport or OriginalAiWork.FulfillRequest);
    }

    [Fact]
    public void WaterDepartureReevaluatesEquipmentForTheExpectedLandBattle()
    {
        var basis = Fixture();
        var land = basis.Military!.Formations[0] with { Tactics = new(100, 1, [0], []) };
        var ship = land with { Id = 20, TravelMode = TravelMode.Water, Tactics = new(100, 1, [4], []) };
        var content = basis with
        {
            Officers = basis.Officers.Select(p => p.State.Id == 3 ? p with { State = p.State with
                { LearnedTactics = [100, 200], TacticProficiencies = [100, 0, 0, 0, 900, 0, 0, 0, 0] } } : p).ToArray(),
            Military = basis.Military with
            {
                TerrainIds = Enumerable.Range(0, 72).Select(i => i == 25 ? 1 : 0).ToImmutableArray(),
                Terrains = [new(0, 1, null), new(1, null, 1) { IsWater = true }],
                Formations = [land, ship], BasicNavalFormationId = 20,
                Tactics = [new(100, 0, TacticEffect.Martial, 10, 0, 0, true, true, [10]),
                    new(200, 4, TacticEffect.Martial, 100, 0, 0, true, true, [20])]
            }
        };
        var session = GameSession.Start(content, 281);
        Assert.Single(session.EndControl(new("player")).Events.OfType<UnitDeployedEvent>());
        Assert.Equal(100, session.Snapshot.Officers.Single(p => p.Id == 3).EquippedTacticId);
    }

    [Fact]
    public void CampaignAndCommandsSurviveExactRecoveryWithoutDuplicatingTheCommitment()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 281);
        session.EndControl(new("player"));
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        StateAssert.Equal(session.Capture(), restored.Capture());
        for (int i = 0; i < 3; i++)
        {
            EventAssert.Equal(session.AdvanceXun().Events, restored.AdvanceXun().Events);
            StateAssert.Equal(session.Capture(), restored.Capture());
        }
        Assert.Equal(1001, session.MilitaryCampaigns.Campaigns.Single(c => c.Id == 0).SourceId);
    }

    [Fact]
    public void CampaignClearsTheIntermediateObjectiveAndCompletesTheOriginalGoalThroughPublicExecution()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 281, playerForceIds: [1, 2]);
        session.EndControl(new("player"));
        bool clearedIntermediate = false;
        for (int i = 0; i < 20 && session.Snapshot.Simulation.Boundary != SimulationBoundary.Terminal; i++)
        {
            session.AdvanceXun();
            clearedIntermediate |= session.Snapshot.Settlements.Single(s => s.Id == 1002).OwnerId == 1;
            if (session.MilitaryCampaigns.Campaigns.Any(c => c.Id == 0 && c.Stage == MilitaryCampaignStage.Completed)) break;
        }
        Assert.True(clearedIntermediate);
        var campaign = session.MilitaryCampaigns.Campaigns.Single(c => c.Id == 0);
        Assert.Equal(1001, campaign.SourceId);
        Assert.Equal(1003, campaign.ObjectiveId);
        Assert.True(session.Snapshot.Settlements.Single(s => s.Id == 1003).OwnerId == 1,
            System.Text.Json.JsonSerializer.Serialize(new { campaign, session.Snapshot.Units, session.Snapshot.Officers, session.Snapshot.Settlements }));
        Assert.Equal(MilitaryCampaignStage.Completed, campaign.Stage);
        StateAssert.Equal(session.Capture(), SaveService.Decode(SaveService.Encode(session), content).Capture());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssemblyUsesPublicPersonnelOrTransportAndWaitsForActualArrival(bool ship)
    {
        var basis = Fixture();
        var content = basis with
        {
            Settlements = basis.Settlements.Where(s => s.Id != 1002).Select(s => s.Id == 1000 ? s with
                { X = 15, Y = 1, Soldiers = ship ? 0u : 90000u, Morale = ship ? 0 : 100 } : s).ToArray(),
            Military = basis.Military! with
            {
                Places = basis.Military.Places.Where(p => p.Id != 1002).Select(p => p.Id == 1000 ? p with { Position = new(77, 7) } : p).ToImmutableArray(),
                Formations = [basis.Military.Formations[0] with { MovementBudget = 1 },
                    new(20, 40, 0, 10000, 1, new(1, 1), TravelMode.Land)],
                TransportFormations = ship ? new(20, null) : null
            },
            RegionCityIds = basis.RegionCityIds.Where(p => p.Key != 1002).ToDictionary(),
            RegionNameKeys = basis.RegionNameKeys.Where(p => p.Key != 1002).ToDictionary()
        };
        var session = GameSession.Start(content, 281, playerForceIds: [1, 2]);
        var result = session.EndControl(new("player"));
        var campaign = Assert.Single(session.MilitaryCampaigns.Campaigns);
        Assert.Equal(MilitaryCampaignStage.Assembling, campaign.Stage);
        Assert.Equal(1000, campaign.TargetId);
        Assert.Equal(1001, campaign.SourceId);
        Assert.Equal(3, campaign.DeliveryOfficerId);
        Assert.Equal(ship, campaign.UnitId is not null);
        if (ship) Assert.Single(result.Events.OfType<UnitDeployedEvent>());
        else Assert.Contains(result.Events.OfType<TaskStartedEvent>(), e => e.Task.OfficerId == 3);
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        bool attacked = false;
        for (int i = 0; i < 20 && !attacked; i++)
        {
            var advance = session.AdvanceXun();
            EventAssert.Equal(advance.Events, restored.AdvanceXun().Events);
            StateAssert.Equal(session.Capture(), restored.Capture());
            attacked = advance.Events.OfType<UnitDeployedEvent>().Any(e => e.SettlementId == 1000);
        }
        Assert.True(attacked);
        Assert.Equal(1001, session.MilitaryCampaigns.Campaigns.Single(c => c.Id == campaign.Id).SourceId);
    }

    [Fact]
    public void StalledCampaignStopsAddingAttackersAndUsesPublicReturnAfterRecovery()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 281, playerForceIds: [1, 2]);
        session.EndControl(new("player"));
        int unit = Assert.Single(session.MilitaryCampaigns.Campaigns).UnitId!.Value;
        var saved = session.Capture();
        session = GameSession.Restore(content, saved with { World = saved.World with
        {
            Units = saved.World.Units.Select(u => u.Id == unit ? u with
                { Condition = UnitCondition.Confused, ConditionDaysRemaining = 90 } : u).ToArray()
        } });
        var events = new List<DomainEvent>();
        for (int i = 0; i < 6; i++) events.AddRange(session.AdvanceXun().Events);
        Assert.Equal(MilitaryCampaignStage.Withdrawing, session.MilitaryCampaigns.Campaigns.Single(c => c.Id == 0).Stage);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
        bool returned = false;
        for (int i = 0; i < 6 && !returned; i++)
            returned = session.AdvanceXun().Events.OfType<UnitOrderChangedEvent>().Any(e =>
                e.UnitId == unit && e.Order?.Kind == MilitaryOrderKind.Return);
        Assert.True(returned);
    }

    [Fact]
    public void CampaignSaveRejectsMissingStateAndInvalidReferences()
    {
        var content = Fixture();
        var session = GameSession.Start(content, 281);
        session.EndControl(new("player"));
        var saved = session.Capture();
        var invalid = saved.MilitaryCampaigns with { Campaigns = saved.MilitaryCampaigns.Campaigns
            .Select(c => c with { ObjectiveId = int.MaxValue }).ToImmutableArray() };
        Assert.Throws<ArgumentException>(() => GameSession.Restore(content, saved with { MilitaryCampaigns = invalid }));
        var json = System.Text.Json.Nodes.JsonNode.Parse(SaveService.Encode(session))!;
        json["session"]!.AsObject().Remove("militaryCampaigns");
        Assert.Throws<SaveException>(() => SaveService.Decode(System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()), content));
    }

    [Fact]
    public void DirectPlayerControlAndDomesticDelegationDoNotDeploy()
    {
        foreach (bool direct in new[] { true, false })
        {
            var content = Fixture();
            content = content with { Information = content.Information with { Corps = content.Information.Corps.Select(c => c.Id == 3 ? c with
                { ControlMode = direct ? CorpsControlMode.Direct : CorpsControlMode.Automatic,
                    Delegation = direct ? new GeneralDelegation() : new DomesticDelegation() } : c).ToArray() } };
            var session = GameSession.Start(content, 281);
            var result = session.EndControl(new("player"));
            Assert.Empty(result.Events.OfType<UnitDeployedEvent>());
            Assert.Empty(session.MilitaryCampaigns.Campaigns);
        }
    }

    [Fact]
    public void PublicPreparationRejectsUnfundedSourceWithoutInventingCommitments()
    {
        var content = Fixture();
        content = content with { Information = content.Information with { Corps = content.Information.Corps.Select(c =>
            c with { Treasury = new(0, 0) }).ToArray() } };
        var session = GameSession.Start(content, 281);
        var result = session.EndControl(new("player"));
        Assert.Empty(result.Events.OfType<UnitDeployedEvent>());
        Assert.Empty(session.MilitaryCampaigns.Campaigns);
        Assert.Equal(20000u, session.Snapshot.Settlements.Single(s => s.Id == 1001).Soldiers);
    }

    internal static ScenarioInitialState Fixture()
    {
        var basis = G01ScenarioTests.Fixture();
        var positions = new[] { (999, 2, 2), (1000, 7, 2), (1001, 7, 7), (1002, 47, 7), (1003, 102, 7), (2000, 107, 2) };
        return basis with
        {
            Settlements = positions.Select(p => new SettlementInitialState(p.Item1, $"place.{p.Item1}", p.Item1 < 1002 ? 1 : 2,
                p.Item2 / 5, p.Item3 / 5, p.Item1 == 1001 ? 20000u : p.Item1 == 1003 ? 1000u : 0u,
                100, 100, p.Item1 is 1001 or 1003 ? 100 : 0, []) { Kind = p.Item1 is 1001 or 1002 ? SettlementKind.Facility : SettlementKind.City,
                    CorpsId = p.Item1 is 1000 or 1001 ? 3 : p.Item1 == 999 ? 1 : 2 }).ToArray(),
            Officers = [new("general", new(3, 1, 1001, new(95, 90, 80, 90), false) { BirthYear = 160 }),
                new("own", new(1, 1, 999, new(95, 90, 80, 90), false) { Status = OfficerStatus.Ruler, BirthYear = 160 }),
                new("opponent", new(2, 2, 1003, new(40, 40, 40, 40), false) { Status = OfficerStatus.Ruler, BirthYear = 160 })],
            Forces = [new(1, "own", 1, 100) { LeaderId = 1 }, new(2, "opponent", 2, 100) { LeaderId = 2 }],
            Information = new([], [], [], [new(1, 1, 1, new(10000, 100000))
                { IsRoot = true },
                new(3, 1, 3, new(10000, 100000)) { ControlMode = CorpsControlMode.Automatic, Delegation = new CaptureSettlementDelegation(1003) },
                new(2, 2, 2, new(10000, 100000)) { IsRoot = true }], []),
            RegionCityIds = new() { [999] = 999, [1000] = 1000, [1001] = 1000, [1002] = 2000, [1003] = 1003, [2000] = 2000 },
            RegionNameKeys = positions.ToDictionary(p => p.Item1, p => $"region.{p.Item1}"),
            CityConnections = [], MilitaryCityConnections = [],
            Military = new(24, 3, Enumerable.Repeat(0, 72).ToImmutableArray(), [new(0, 1, null)],
                [new(10, 10, 100, 10000, 1, new(1, 1), TravelMode.Land)
                    { Positions = [new(0, BattleRank.Middle)], Attack = new(1, true), Combat = new(100, 1000, 1000, 100, 10) }],
                positions.Select(p => new BattlefieldPlace(p.Item1, new(p.Item2, p.Item3), p.Item1 == 1002 ? new(1, 11) : new(1, 1))
                    { Combat = new(1, 1, 1, 1, 10) }).ToImmutableArray()),
            OriginalAi = OriginalAiDefinition.Default with { WorkRules = OriginalAiDefinition.Default.WorkRules.ToImmutableDictionary(p => p.Key,
                p => p.Value with { OpeningWaitDays = 10000 }) }
        };
    }
}
