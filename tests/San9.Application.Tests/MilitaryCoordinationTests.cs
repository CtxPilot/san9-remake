using System.Collections.Immutable;
using San9.Application.AI;
using San9.Application.AI.Military;
using San9.Application.Persistence;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class MilitaryCoordinationTests
{
    private static (ScenarioSessionDefinition Definition, SimulationEngine Engine) Setup(Func<WorldSnapshot, WorldSnapshot>? change = null,
        ScenarioInitialState? content = null)
    {
        content ??= MilitaryCampaignSessionTests.Fixture();
        var definition = new ScenarioSessionDefinition(content);
        var world = definition.CreateWorld(281).CreateSnapshot();
        world = change?.Invoke(world) ?? world;
        world = world with { Units = world.Units.Select(u => u with { MemberPositions = world.Officers.Where(p => p.UnitId == u.Id).ToImmutableDictionary(p => p.Id, _ => new FormationPosition(0, BattleRank.Middle)) }).ToArray() };
        world = world with { Simulation = world.Simulation with { Boundary = SimulationBoundary.Stable, NextUnitId = Math.Max(world.Simulation.NextUnitId, world.Units.Select(u => (long)u.Id + 1).DefaultIfEmpty(0).Max()) } };
        return (definition, GameSession.EngineFactory(content)(WorldState.Restore(world)));
    }
    private static MilitaryCampaign Campaign(WorldSnapshot world, int body, MilitaryCampaignStage stage = MilitaryCampaignStage.Marching) =>
        new(0, 1, 1, 999, 1003, 1003, body, stage, world.Clock, world.Clock, new(52, 7), 100)
        { InitialSoldiers = 4000, PlannedArrivalDay = world.Clock.DayNumber + 10 };
    private static MilitaryTravelQueries Queries(WorldSnapshot world, MilitaryRulebook rules, int routes = 8) =>
        new(MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 1), rules, new(rules), new(maximumDetailedRoutes: routes),
            (owner, other) => other is int force && owner != force);
    private static OriginalAiWorkReport Run(ScenarioSessionDefinition definition, SimulationEngine engine, MilitaryTurnBatch batch, List<DomainEvent> events) =>
        MilitaryTurnRunner.Run(engine, definition, engine.Snapshot.Corps.Single(c => c.Id == 3), new(CorpsControl.ControllerId(3)), batch, events);

    [Fact]
    public void BattlefieldSeparatesKnownReturnsFromPossibleFieldResponseAndDoesNotDuplicateBodies()
    {
        var (definition, engine) = Setup();
        var world = engine.Snapshot with { Settlements = engine.Snapshot.Settlements.Select(s => s.Id == 1003 ? s with { Soldiers = 1000 } : s).ToArray(), Units = [new(50, 2, new(87, 12), 3000)
            { FormationId = 10, Morale = 100, Order = new(MilitaryOrderKind.Return, null, 1003) }] };
        var field = MilitaryBattlefieldAnalysis.Assess(Queries(world, definition.MilitaryRules!), definition.MilitaryRules!, 1003, 20);
        var returning = Assert.Single(field.Contributions.Where(c => c.Source == new MapObjectReference(50, false)));
        Assert.Equal(MilitaryContributionKind.PublishedArrival, returning.Kind);
        Assert.NotNull(returning.ArrivalDay);
        Assert.Equal(4000, field.PublishedAt(20));
        var unknown = MilitaryBattlefieldAnalysis.Assess(Queries(world, definition.MilitaryRules!, 0), definition.MilitaryRules!, 1003, 20);
        Assert.True(unknown.HasUnresolvedArrivals);
        Assert.Equal(1000, unknown.PublishedAt(20));
        Assert.Equal(4000, unknown.PossibleAt(20));
    }

    [Fact]
    public void MobilizationRequiresPublicOfficerPresenceAndDoesNotReadEnemyActionsOrMoney()
    {
        var (definition, engine) = Setup();
        var world = engine.Snapshot with { Settlements = engine.Snapshot.Settlements.Select(s => s.Id == 2000 ? s with { Soldiers = 9000 } : s).ToArray() };
        Assert.DoesNotContain(MilitaryBattlefieldAnalysis.Assess(Queries(world, definition.MilitaryRules!), definition.MilitaryRules!, 1003, 20).Contributions,
            c => c.Kind == MilitaryContributionKind.Mobilization);
        world = world with { Officers = world.Officers.Select(p => p.Id == 2 ? p with { SettlementId = 2000, Acted = true } : p).ToArray(),
            Corps = world.Corps.Select(c => c.Id == 2 ? c with { Treasury = new(0, 0) } : c).ToArray() };
        var first = MilitaryBattlefieldAnalysis.Assess(Queries(world, definition.MilitaryRules!), definition.MilitaryRules!, 1003, 20);
        Assert.Contains(first.Contributions, c => c.Kind == MilitaryContributionKind.Mobilization && c.Source.Id == 2000 && c.Soldiers == 9000);
        var changed = world with { Officers = world.Officers.Select(p => p with { Acted = false }).ToArray(),
            Corps = world.Corps.Select(c => c with { Treasury = new(10000, 100000) }).ToArray() };
        Assert.Equal(first.Contributions.ToArray(), MilitaryBattlefieldAnalysis.Assess(Queries(changed, definition.MilitaryRules!), definition.MilitaryRules!, 1003, 20).Contributions.ToArray());
    }

    [Fact]
    public void CorpsSlicesCannotOverspendTheForceBudgetAndUnusedWorkPassesToLaterCorps()
    {
        var budget = new MilitaryQueryBudget(100, 2, 4);
        var first = budget.Slice(2);
        Assert.True(first.Spend(20));
        Assert.True(first.BeginSourceField());
        Assert.False(first.BeginSourceField());
        var last = budget.Slice(1);
        Assert.Equal(80, last.RemainingExpanded);
        Assert.False(last.Spend(100));
        Assert.Equal(100, budget.Expanded);
        Assert.True(last.BeginSourceField());
        Assert.False(budget.Slice(1).BeginSourceField());
    }

    [Fact]
    public void ReceivingLedgerIncludesWoundsAndActualInboundReturnsAndTransports()
    {
        var (_, engine) = Setup();
        var place = engine.Snapshot.Settlements.Single(s => s.Id == 1000) with { Soldiers = 1000, Wounded = 500, SoldierCapacity = 10000 };
        var world = engine.Snapshot with { Units = [new(50, 1, new(17, 12), 3000) { Wounded = 1000, Order = new(MilitaryOrderKind.Return, null, 1000) },
            new(51, 1, new(22, 12), 2000) { Order = new(MilitaryOrderKind.Transport, null, 1000) },
            new(52, 2, new(27, 12), 2000) { Order = new(MilitaryOrderKind.AttackSettlement, null, 1000) }] };
        Assert.Equal(2500, MilitaryResourceLedger.ReceivingSpace(world, place));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    public void AnotherCorpsSupportsTheSameCampaignWithBoundedWaitingAndActualDeparture(int arrivalDay)
    {
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 10000 } : s).ToArray(),
            Units = [new(50, 1, new(52, 7), 4000) { FormationId = 10, Morale = 100, CorpsId = 1, ServicePlaceId = 999, CommanderId = 1,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 1 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        });
        var state = new MilitaryCampaignState { NextId = 1, Campaigns = [Campaign(engine.Snapshot, 50) with { TargetId = 1002, PlannedArrivalDay = engine.Snapshot.Clock.DayNumber + arrivalDay }] };
        var batch = new MilitaryTurnBatch(null, state);
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        if (arrivalDay == 30)
        {
            Assert.Empty(events.OfType<UnitDeployedEvent>());
            var pending = Assert.Single(Assert.Single(batch.State.Campaigns).Reservations);
            Assert.True(pending.NotBeforeDay > engine.Snapshot.Clock.DayNumber);
            var later = engine.Snapshot with { Clock = engine.Snapshot.Clock.Advance().Advance() };
            engine = GameSession.EngineFactory(definition.Content)(WorldState.Restore(later));
            batch = new MilitaryTurnBatch(null, batch.State);
            Run(definition, engine, batch, events);
            Assert.Empty(Assert.Single(batch.State.Campaigns).Reservations);
        }
        var deployment = Assert.Single(events.OfType<UnitDeployedEvent>());
        var campaign = Assert.Single(batch.State.Campaigns);
        var support = Assert.Single(campaign.Support);
        Assert.Equal(deployment.UnitId, support.UnitId);
        Assert.Equal(3, support.CorpsId);
        Assert.Equal(50, campaign.UnitId);
        Assert.Equal(MilitaryCommitmentRole.Siege, support.Role);
        var saved = GameSession.Start(definition.Content, 281).Capture() with { World = engine.Snapshot, MilitaryCampaigns = batch.State,
            MilitaryIntelligence = MilitaryIntelligencePublication.Publish(engine.Snapshot, definition.MilitaryRules!).State };
        var session = GameSession.Restore(definition.Content, saved);
        StateAssert.Equal(session.Capture(), SaveService.Decode(SaveService.Encode(session), definition.Content).Capture());
    }

    [Fact]
    public void WholeCampaignWithdrawalAlsoOrdersParticipantsFromAnotherCorps()
    {
        var (definition, engine) = Setup(w => w with
        {
            Units = [new(50, 1, new(52, 7), 4000) { FormationId = 10, Morale = 100, CorpsId = 1, ServicePlaceId = 999, CommanderId = 1,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1003) },
                new(51, 1, new(37, 12), 3000) { FormationId = 10, Morale = 100, CorpsId = 3, ServicePlaceId = 1001, CommanderId = 3,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1003) }],
            Officers = w.Officers.Select(p => p.Id is 1 or 3 ? p with { UnitId = p.Id == 1 ? 50 : 51, SettlementId = null } : p).ToArray()
        });
        var campaign = Campaign(engine.Snapshot, 50, MilitaryCampaignStage.Withdrawing) with { Support = [new(51, 1001, 3, MilitaryCommitmentRole.Siege, 3000, null)] };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [campaign] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.Contains(events.OfType<UnitOrderChangedEvent>(), e => e.UnitId == 51 && e.Order?.Kind == MilitaryOrderKind.Return);
        Assert.Equal(MilitaryOrderKind.AttackSettlement, engine.Snapshot.Units.Single(u => u.Id == 50).Order!.Kind);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
    }
    [Fact]
    public void IdleFieldForceInterceptsWithoutPayingForAnotherDeparture()
    {
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1000 ? s with { Soldiers = 0 } : s).ToArray(),
            Units = [new(50, 2, new(32, 12), 3000) { FormationId = 10, Morale = 100, CorpsId = 2, ServicePlaceId = 1003, CommanderId = 2,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1000) },
                new(51, 1, new(22, 7), 10000) { FormationId = 10, Morale = 100, CorpsId = 3, ServicePlaceId = 1001, CommanderId = 3 }],
            Officers = w.Officers.Select(p => p.Id is 2 or 3 ? p with { UnitId = p.Id == 2 ? 50 : 51, SettlementId = null } : p).ToArray()
        });
        var batch = new MilitaryTurnBatch(null, new());
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        var rescue = Assert.Single(batch.State.Campaigns);
        Assert.Equal(MilitaryCampaignPurpose.Relief, rescue.Purpose);
        Assert.Equal(51, rescue.UnitId);
        Assert.Equal(1000, rescue.ObjectiveId);
        Assert.Contains(events.OfType<UnitOrderChangedEvent>(), e => e.UnitId == 51 && e.Order?.UnitId == 50);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
    }

    [Fact]
    public void ProactiveEvacuationCanUseAnOccupiedSafeDestinationAndTracksTheActualJourney()
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        content = content with { Officers = content.Officers.Append(new("resident", new(4, 1, 1000, new(80, 80, 80, 80), true) { BirthYear = 160 })).ToArray() };
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1001 ? s with { Soldiers = 0 } : s.Id == 1000 ? s with { Soldiers = 50000 } : s).ToArray(),
            Units = [new(50, 2, new(37, 12), 9000) { FormationId = 10, Morale = 100, CorpsId = 2, ServicePlaceId = 1003, CommanderId = 2,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1001) }],
            Officers = w.Officers.Select(p => p.Id == 2 ? p with { UnitId = 50, SettlementId = null } : p.Id == 4 ? p with { Acted = true } : p).ToArray()
        }, content);
        var batch = new MilitaryTurnBatch(null, new());
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        var evacuation = Assert.Single(batch.State.Campaigns.Where(c => c.Purpose == MilitaryCampaignPurpose.Evacuation));
        Assert.Equal(1001, evacuation.SourceId);
        Assert.Equal(1000, evacuation.TargetId);
        Assert.Equal(MilitaryCampaignStage.Assembling, evacuation.Stage);
        Assert.Equal(1001, engine.Snapshot.Officers.Single(p => p.Id == 3).SettlementId);
        Assert.Contains(events.OfType<TaskStartedEvent>(), e => e.Task.OfficerId == 3 && e.Task.Journey?.DestinationPlaceId == 1000);
        Assert.Equal(1, batch.State.EvacuatedPlaces[1001]);
    }

    [Fact]
    public void ReservedSoldiersOfficersAndMoneyRemainUnavailableToOtherCampaignsAndDomesticWork()
    {
        var (definition, engine) = Setup();
        var world = engine.Snapshot;
        var campaign = Campaign(world, 50) with { UnitId = null, Stage = MilitaryCampaignStage.Preparing,
            Reservations = [new(1001, 3, [3], 8000, 5000, world.Clock.DayNumber + 10, world.Clock.DayNumber + 20, MilitaryCommitmentRole.Siege)] };
        var state = new MilitaryCampaignState { NextId = 1, Campaigns = [campaign], EvacuatedPlaces = ImmutableDictionary<int, int>.Empty.Add(1001, 1) };
        var projection = new OriginalAiProjection(engine, definition.Content, 3, new(definition.Content, definition.MilitaryRules, world), true) { Commitments = state };
        var domestic = projection.Domestic();
        Assert.Equal(world.Corps.Single(c => c.Id == 3).Treasury.Money - 5000, domestic.Money);
        Assert.DoesNotContain(domestic.Sites.SelectMany(s => s.Settlement.OfficerIds), id => id == 3);
        Assert.Equal(8000, MilitaryResourceLedger.ReservedSoldiers(state, 1001));
        Assert.Equal(0, MilitaryResourceLedger.ReservedSoldiers(state, 1001, campaign.Id));
        var expired = state.Reconcile(world with { Clock = world.Clock.Advance().Advance().Advance() }, definition.MilitaryRules);
        Assert.Empty(expired.Campaigns.Single().Reservations);
    }

    [Fact]
    public void ResourceCostPreviewUsesTheActualPublicSubmissionWithoutSpendingOrAllocatingIdentities()
    {
        var (_, engine) = Setup();
        var before = engine.Snapshot;
        var command = new DeployUnitCommand(SimulationIdentity.Command(before.Simulation.NextSequence, 1), 1,
            before.Simulation.NextSequence, 1001, [3], 1000, 10, new(MilitaryOrderKind.AttackSettlement, null, 1002));
        var context = new CommandContext(CorpsControl.ControllerId(3));
        var preview = engine.PreviewResources(context, command);
        Assert.Equal(CommandRejection.None, preview.Rejection);
        StateAssert.Equal(before, engine.Snapshot);
        var actual = engine.Execute(context, command);
        Assert.True(actual.Submission.Accepted);
        Assert.Equal(preview.Changes.ToArray(), actual.Events.OfType<ResourceChangedEvent>().ToArray());
        Assert.Contains(preview.Changes, e => e.Account == new ResourceAccount(ResourceOwnerKind.Corps, 3) && e.Resource == ResourceKind.Money && e.Actual == -100);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1000, false)]
    public void RescueTransportsOnlyWhenItCanArriveBeforeTheTownFallsAndExtendSurvival(int wallAttack, bool useful)
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        var combat = content.Military!.Formations[0];
        content = content with { Military = content.Military with { Formations = [combat with { MovementBudget = 1 },
            combat with { Id = 30, MovementBudget = 10, IsCommon = false, Combat = combat.Combat! with { AgainstWalls = wallAttack } }, new(20, 40, 0, 10000, 1, new(1, 1), TravelMode.Land)],
            TransportFormations = new(20, null), Places = content.Military.Places.Select(p => p.Id == 1000 ? p with { Combat = new(1, 1000, 1000, 800, 10) } : p).ToImmutableArray() } };
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1000 ? s with { Soldiers = 1000, Morale = 100 } : s).ToArray(),
            Units = [new(50, 2, new(37, 12), 9000) { FormationId = 30, Morale = 100, CorpsId = 2, ServicePlaceId = 1003, CommanderId = 2,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1000) }],
            Officers = w.Officers.Select(p => p.Id == 2 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        }, content);
        var batch = new MilitaryTurnBatch(null, new());
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        var rescues = batch.State.Campaigns.Where(c => c.Purpose == MilitaryCampaignPurpose.Relief).ToArray();
        if (!useful)
        {
            Assert.Empty(rescues);
            Assert.DoesNotContain(engine.Snapshot.Units, u => u.OwnerId == 1 && u.Order?.Kind == MilitaryOrderKind.Transport);
            return;
        }
        var rescue = Assert.Single(rescues);
        Assert.Equal(MilitaryOrderKind.Transport, engine.Snapshot.Units.Single(u => u.Id == rescue.UnitId).Order!.Kind);
        Assert.Equal(1000, rescue.ObjectiveId);
        Assert.Equal(MilitaryCampaignStage.Marching, rescue.Stage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardWhoseFieldTargetDisappearedRejoinsTheExistingBattlefield(bool automaticallyReturning)
    {
        var (definition, engine) = Setup(w => w with
        {
            Units = [new(50, 1, new(52, 7), 4000) { FormationId = 10, Morale = 100, CorpsId = 1, ServicePlaceId = 999, CommanderId = 1,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) },
                new(51, 1, new(37, 12), 3000) { FormationId = 10, Morale = 100, CorpsId = 3, ServicePlaceId = 1001, CommanderId = 3,
                    Order = automaticallyReturning ? new(MilitaryOrderKind.Return, null, 1001) : null, AutonomousReturn = automaticallyReturning }],
            Officers = w.Officers.Select(p => p.Id is 1 or 3 ? p with { UnitId = p.Id == 1 ? 50 : 51, SettlementId = null } : p).ToArray()
        });
        var campaign = Campaign(engine.Snapshot, 50) with { TargetId = 1002, Support = [new(51, 1001, 3, MilitaryCommitmentRole.Field, 3000, null)] };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [campaign] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.Contains(events.OfType<UnitOrderChangedEvent>(), e => e.UnitId == 51 && e.Order?.SettlementId == 1002 && e.Order.Kind == MilitaryOrderKind.AttackSettlement);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
        Assert.Single(batch.State.Campaigns);
    }

    [Theory]
    [InlineData(35, MilitaryCampaignStage.Withdrawing)]
    [InlineData(45, MilitaryCampaignStage.Marching)]
    public void MajorLossesStopFurtherInvestmentButDoNotAbandonAnImminentCapture(int position, MilitaryCampaignStage expected)
    {
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 10000, Morale = 100 } : s).ToArray(),
            Units = [new(50, 1, new(position, 7), 1000) { FormationId = 10, Morale = 100, CorpsId = 3, ServicePlaceId = 1001, CommanderId = 3,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 3 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        });
        var campaign = Campaign(engine.Snapshot, 50) with { CorpsId = 3, TargetId = 1002, InitialSoldiers = 20000, LastPosition = new(position, 7) };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [campaign] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.Equal(expected, Assert.Single(batch.State.Campaigns).Stage);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
        if (expected == MilitaryCampaignStage.Withdrawing)
            Assert.Contains(events.OfType<UnitOrderChangedEvent>(), e => e.UnitId == 50 && e.Order?.Kind == MilitaryOrderKind.Return);
        else Assert.Equal(MilitaryOrderKind.AttackSettlement, engine.Snapshot.Units.Single(u => u.Id == 50).Order!.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithdrawalWithoutReceivingSpaceUsesAReachableRallyOrKeepsItsCurrentResistance(bool hasAlly)
    {
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s with { SoldierCapacity = Math.Max(1u, s.Soldiers) }).ToArray(),
            Units = new[] { new UnitState(51, 1, new(37, 12), 3000) { FormationId = 10, Morale = 100, CorpsId = 3, ServicePlaceId = 1001, CommanderId = 3,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1003) } }.Concat(hasAlly ? [new UnitState(50, 1, new(22, 7), 4000)
                    { FormationId = 10, Morale = 100, CorpsId = 1, ServicePlaceId = 999, CommanderId = 1 }] : []).ToArray(),
            Officers = w.Officers.Select(p => p.Id == 3 || p.Id == 1 && hasAlly ? p with { UnitId = p.Id == 1 ? 50 : 51, SettlementId = null } : p).ToArray()
        });
        var campaign = Campaign(engine.Snapshot, 51, MilitaryCampaignStage.Withdrawing) with { CorpsId = 3, LastPosition = new(37, 12) };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [campaign] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.DoesNotContain(events.OfType<UnitOrderChangedEvent>(), e => e.Order?.Kind == MilitaryOrderKind.Return);
        Assert.Equal(hasAlly ? MilitaryOrderKind.Move : MilitaryOrderKind.AttackSettlement, engine.Snapshot.Units.Single(u => u.Id == 51).Order!.Kind);
    }

    [Fact]
    public void NewOrdersDiscardOldWaitingAndWaypointsButKeepRealMovementRemainder()
    {
        var (definition, engine) = Setup();
        var world = engine.Snapshot with { Units = [new(50, 1, new(22, 7), 1000) { FormationId = 10, Morale = 100 }] };
        var rules = definition.MilitaryRules!;
        var observation = MilitaryIntelligencePublication.Publish(world, rules).Observe(world, rules, 1);
        var queries = new MilitaryTravelQueries(observation, rules, new(rules), new(), (_, other) => other == 2,
            new Dictionary<int, MilitaryOwnJourney> { [50] = new(9, 0, new(37, 7)) });
        var existing = queries.FromUnit(50, new(1001, true), verifyApproach: true);
        var reassigned = queries.FromUnit(50, new(1001, true), verifyApproach: true, newOrder: true);
        Assert.Equal(ArrivalEstimateStatus.Known, reassigned.Status);
        Assert.True(existing.Days > reassigned.Days + 9, System.Text.Json.JsonSerializer.Serialize(new { existing, reassigned }));
        var point = queries.FromUnitToPosition(50, new(17, 12), verifyApproach: true);
        Assert.Equal(ArrivalEstimateStatus.Known, point.Status);
        Assert.False(point.TargetIsMoving);
        Assert.True(point.Days < 9);
    }

}
