using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed partial class MilitaryCoordinationTests
{
    [Theory]
    [InlineData(39, true)]
    [InlineData(40, true)]
    [InlineData(50, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(78, false)]
    public void StrategicAttackUsesTheOriginalRelationThresholdAndKeepsTheCampaign(int relation, bool expected)
    {
        var (definition, engine) = Setup(w => w with
        {
            Relations = [new(1, 2, relation)],
            Corps = w.Corps.Select(c => c.Id == 3 ? c with { Delegation = new GeneralDelegation() } : c).ToArray()
        });
        var batch = new MilitaryTurnBatch(null, new());
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.Equal(expected, events.OfType<UnitDeployedEvent>().Any());
        if (!expected) { Assert.Empty(batch.State.Campaigns); return; }
        var campaign = Assert.Single(batch.State.Reconcile(engine.Snapshot, definition.MilitaryRules).Campaigns);
        Assert.Equal(MilitaryCampaignStage.Marching, campaign.Stage);
        Assert.Equal(new UnitPolicies(false, false, true, true), engine.Snapshot.Units.Single(u => u.Id == campaign.UnitId).Policies);
    }

    private static ScenarioInitialState PolicyFixture()
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        return content with
        {
            Forces = content.Forces.Append(content.Forces[1] with { Id = 3, NameKey = "third", LeaderId = 4 }).ToArray(),
            Officers = content.Officers.Append(content.Officers[2] with { NameKey = "third-ruler", State = content.Officers[2].State with
                { Id = 4, OwnerId = 3, SettlementId = 2000 } }).ToArray(),
            Settlements = content.Settlements.Select(s => s.Id == 2000 ? s with { OwnerId = 3, CorpsId = 4 } :
                s.Id == 1002 ? s with { Kind = SettlementKind.City, Y = 2 } : s.Id == 1003 ? s with { Kind = SettlementKind.Facility } : s).ToArray(),
            Information = content.Information with { Corps = content.Information.Corps.Select(c => c.Id == 3 ? c with { Delegation = new GeneralDelegation() } : c)
                .Append(new(4, 3, 4, new(10000, 100000)) { IsRoot = true }).ToArray() },
            RegionCityIds = content.RegionCityIds.ToDictionary(p => p.Key, p => p.Value == 1003 ? 1002 : p.Value),
            Military = content.Military! with { Places = content.Military.Places.Select(p => p.Id == 1002 ? p with
                { Position = new(47, 12), Footprint = new(1, 1) } : p).ToImmutableArray() }
        };
    }

    [Fact]
    public void AllianceTargetRemainsAttackableAtFriendlyRelationsAndPublicConfirmationIsSubmitted()
    {
        var content = PolicyFixture();
        var (definition, engine) = Setup(w => w with
        {
            Relations = [new(1, 2, 78), new(1, 3, 78)],
            Simulation = w.Simulation with { Alliance = new(2, 1, [1, 3], 10) }
        }, content);
        var batch = new MilitaryTurnBatch(null, new());
        var events = new List<DomainEvent>();
        var report = Run(definition, engine, batch, events);
        Assert.Single(events.OfType<UnitDeployedEvent>());
        Assert.Equal(0, report.Rejected);
        Assert.All(batch.State.Reconcile(engine.Snapshot, definition.MilitaryRules).Campaigns,
            c => Assert.Equal(MilitaryCampaignStage.Marching, c.Stage));
        Assert.False(MilitaryCampaignPolicy.CanAttack(engine.Snapshot, 1, 3));
    }

    [Fact]
    public void AutomaticEntryPrioritizesTheAcceptedRequestAndRestoresTheSameDecision()
    {
        var content = PolicyFixture();
        var ordinary = GameSession.Start(content, 281);
        ordinary.EndControl(new("player"));
        Assert.Equal(1002, Assert.Single(ordinary.MilitaryCampaigns.Campaigns).ObjectiveId);

        content = content with { AttackRequests = [new(1, 3, 1003, 19)] };
        var session = GameSession.Start(content, 281, playerForceIds: [1, 2, 3]);
        var restored = SaveService.Decode(SaveService.Encode(session), content);
        EventAssert.Equal(session.EndControl(new("player")).Events, restored.EndControl(new("player")).Events);
        StateAssert.Equal(session.Capture(), restored.Capture());
        var campaign = Assert.Single(session.MilitaryCampaigns.Campaigns);
        Assert.Equal(1003, campaign.ObjectiveId);
        Assert.Equal(1003, session.Snapshot.Units.Single(u => u.Id == campaign.UnitId).Order!.SettlementId);
        var events = new List<DomainEvent>();
        for (int i = 0; i < 8 && session.Snapshot.Simulation.AttackRequests.Length > 0; i++)
            events.AddRange(session.AdvanceXun().Events);
        Assert.Contains(events.OfType<AttackRequestEndedEvent>(), e => e.Reason == AttackRequestEndReason.Fulfilled);
        Assert.Empty(session.Snapshot.Simulation.AttackRequests);
    }

    [Fact]
    public void BarbarianCombatDeploymentRetainsItsOriginalPursuitAndAttackPolicies()
    {
        var (definition, engine) = Setup(w => w with
        {
            Forces = w.Forces.Select(f => f.Id == 1 ? f with { IsBarbarian = true } : f).ToArray(),
            Corps = w.Corps.Select(c => c.Id == 3 ? c with { Delegation = new GeneralDelegation() } : c).ToArray()
        });
        var events = new List<DomainEvent>();
        Run(definition, engine, new(null, new()), events);
        var deployed = Assert.Single(events.OfType<UnitDeployedEvent>());
        Assert.Equal(new UnitPolicies(true, true, true, false), engine.Snapshot.Units.Single(u => u.Id == deployed.UnitId).Policies);
    }

    [Fact]
    public void AnUncommittedAttackRequestPrecedesReinforcingAnUnrelatedCampaign()
    {
        var (definition, engine) = Setup(w => w with
        {
            Simulation = w.Simulation with { AttackRequests = [new(1, 3, 1003, 2, 19)] },
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 10000 } : s).ToArray(),
            Units = [new(50, 1, new(47, 10), 1000) { FormationId = 10, Morale = 100, CorpsId = 3, CommanderId = 1,
                ServicePlaceId = 999, Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 1 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        }, PolicyFixture());
        var prior = Campaign(engine.Snapshot, 50) with { CorpsId = 3, ObjectiveId = 1002, TargetId = 1002 };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [prior] });
        var events = new List<DomainEvent>();
        MilitaryTurnRunner.Run(engine, definition, engine.Snapshot.Corps.Single(c => c.Id == 3), new(CorpsControl.ControllerId(3)), batch, events, MilitaryPlanningPass.Allocate);
        var deployed = Assert.Single(events.OfType<UnitDeployedEvent>());
        Assert.Empty(batch.State.Campaigns.Single(c => c.Id == prior.Id).Support);
        Assert.Equal(1003, batch.State.Campaigns.Single(c => c.UnitId == deployed.UnitId).ObjectiveId);
    }

    [Fact]
    public void AnExistingCampaignDoesNotBlockAnotherObjectiveWithAvailableOfficersAndSoldiers()
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        content = content with { Information = content.Information with { Corps = content.Information.Corps
            .Select(c => c.Id == 3 ? c with { Delegation = new GeneralDelegation() } : c).ToArray() } };
        var (definition, engine) = Setup(w => w with
        {
            Units = [new(50, 1, new(52, 7), 4000) { FormationId = 10, Morale = 100, CorpsId = 3, CommanderId = 1,
                ServicePlaceId = 999, Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 1 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        }, content);
        var prior = Campaign(engine.Snapshot, 50) with { CorpsId = 3, ObjectiveId = 1002, TargetId = 1002 };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [prior] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.Single(events.OfType<UnitDeployedEvent>());
        Assert.Equal(2, batch.State.Campaigns.Length);
        Assert.Contains(batch.State.Campaigns, c => c.Id != prior.Id && c.ObjectiveId != prior.ObjectiveId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACorpsDoesNotSupportAnOffensiveOutsideItsDelegatedObjective(bool domestic)
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        if (domestic) content = content with { Information = content.Information with { Corps = content.Information.Corps
            .Select(c => c.Id == 3 ? c with { Delegation = new DomesticDelegation() } : c).ToArray() } };
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 10000 } : s).ToArray(),
            Units = [new(50, 1, new(52, 7), 1000) { FormationId = 10, Morale = 100, CorpsId = 1, CommanderId = 1,
                ServicePlaceId = 999, Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 1 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        }, content);
        var prior = Campaign(engine.Snapshot, 50) with { ObjectiveId = 1002, TargetId = 1002 };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [prior] });
        MilitaryTurnRunner.Run(engine, definition, engine.Snapshot.Corps.Single(c => c.Id == 3), new(CorpsControl.ControllerId(3)), batch, [], MilitaryPlanningPass.Allocate);
        Assert.Empty(batch.State.Campaigns.Single(c => c.Id == prior.Id).Support);
        Assert.Empty(batch.State.Campaigns.Single(c => c.Id == prior.Id).Reservations);
        Assert.All(batch.State.Campaigns.Where(c => c.Id != prior.Id), c => Assert.Equal(1003, c.ObjectiveId));
        if (domestic) Assert.Single(batch.State.Campaigns);
    }
}
