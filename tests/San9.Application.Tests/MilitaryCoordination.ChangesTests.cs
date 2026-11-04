using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed partial class MilitaryCoordinationTests
{
    [Theory]
    [InlineData(false, UnitCondition.Normal, MilitaryCampaignStage.Withdrawing)]
    [InlineData(true, UnitCondition.Normal, MilitaryCampaignStage.Marching)]
    [InlineData(true, UnitCondition.Routed, MilitaryCampaignStage.Withdrawing)]
    public void LateOrRoutedGuardDoesNotPromiseProtectionAgainstATimelyFieldResponse(bool timely, UnitCondition guardCondition, MilitaryCampaignStage expected)
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        var basic = content.Military!.Formations[0];
        content = content with { Military = content.Military with
        {
            Formations = [basic, basic with { Id = 30, IsCommon = false, Combat = new(3000, 3000, 1, 100, 10) },
                basic with { Id = 40, IsCommon = false, Combat = new(10000, 1000, 1000, 5000, 10) }],
            Places = content.Military.Places.Select(p => p.Id == 1002 ? p with { Combat = new(1, 1, 1, 2000, 10) } : p).ToImmutableArray()
        } };
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 20000, Morale = 100, Durability = 10000, MaxDurability = 10000 } :
                s.Id == 1001 ? s with { Soldiers = 0, Morale = 0 } : s.Id == 1000 ? s with { Soldiers = 50000, Morale = 100 } : s).ToArray(),
            Units = [new(50, 1, new(42, 7), 2000) { FormationId = 10, Morale = 100, CommandedThisXun = !timely, CorpsId = 3, CommanderId = 3, ServicePlaceId = 1001,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) },
                new(51, 1, timely ? new(52, 7) : new(107, 12), 10000) { FormationId = 40, Morale = 100, Condition = guardCondition, ConditionDaysRemaining = guardCondition == UnitCondition.Routed ? 3 : 0, CorpsId = 1, CommanderId = 1, ServicePlaceId = 999,
                    Order = new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 60 } },
                new(60, 2, new(52, 12), 3000) { FormationId = 30, Morale = 100, CorpsId = 2, CommanderId = 2, ServicePlaceId = 1003,
                    Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p with { SettlementId = null, UnitId = p.Id switch { 1 => 51, 2 => 60, _ => 50 } }).ToArray()
        }, content);
        var campaign = Campaign(engine.Snapshot, 50) with { CorpsId = 3, TargetId = 1002, InitialSoldiers = 12000,
            LastPosition = new(42, 7), TargetDurability = 10000, Support = [new(51, 999, 1, MilitaryCommitmentRole.Field, 10000, null)] };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [campaign] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        Assert.Equal(expected, Assert.Single(batch.State.Campaigns).Stage);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
        if (timely && guardCondition == UnitCondition.Normal) Assert.Equal(MilitaryOrderKind.AttackSettlement, engine.Snapshot.Units.Single(u => u.Id == 50).Order!.Kind);
        else Assert.NotEqual(MilitaryOrderKind.AttackSettlement, engine.Snapshot.Units.Single(u => u.Id == 50).Order!.Kind);
    }

    [Fact]
    public void RejectedSupportReleasesItsReservationWithoutSpendingOrInventingAUnit()
    {
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 10000, Morale = 100 } : s).ToArray(),
            Units = [new(50, 1, new(52, 7), 4000) { FormationId = 10, Morale = 100, CorpsId = 1, ServicePlaceId = 999, CommanderId = 1,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 1 ? p with { UnitId = 50, SettlementId = null } : p).ToArray()
        });
        var world = engine.Snapshot;
        var campaign = Campaign(world, 50) with { TargetId = 1002,
            Reservations = [new(1001, 3, [3], 5000, 100, world.Clock.DayNumber, world.Clock.DayNumber + 10, MilitaryCommitmentRole.Siege)] };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [campaign] });
        var events = new List<DomainEvent>();
        // The source is delegated; the player's direct context cannot submit on its behalf.
        var report = MilitaryTurnRunner.Run(engine, definition, world.Corps.Single(c => c.Id == 3), new("player"), batch, events);
        Assert.True(report.Rejected > 0);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
        Assert.Empty(Assert.Single(batch.State.Campaigns).Reservations);
        Assert.Empty(Assert.Single(batch.State.Campaigns).Support);
        StateAssert.Equal(world, engine.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedResourcesInvalidatePendingSupportBeforeCivilianSpending(bool money)
    {
        var (_, engine) = Setup();
        var world = engine.Snapshot;
        var campaign = Campaign(world, 50) with { UnitId = null, Stage = MilitaryCampaignStage.Preparing,
            Reservations = [new(1001, 3, [3], 8000, 5000, world.Clock.DayNumber + 10, world.Clock.DayNumber + 20, MilitaryCommitmentRole.Siege)] };
        world = money ? world with { Corps = world.Corps.Select(c => c.Id == 3 ? c with { Treasury = new(100, 100000) } : c).ToArray() } :
            world with { Settlements = world.Settlements.Select(s => s.Id == 1001 ? s with { Soldiers = 100 } : s).ToArray() };
        var state = new MilitaryCampaignState { NextId = 1, Campaigns = [campaign] }.Reconcile(world);
        Assert.Empty(Assert.Single(state.Campaigns).Reservations);
    }
    [Fact]
    public void EmergencyRecallTransfersTheGuardAndReevaluatesTheRemainingSiege()
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        var basic = content.Military!.Formations[0];
        content = content with
        {
            Officers = content.Officers.Append(new("guard", new(4, 1, 1000, new(95, 95, 95, 95), false) { BirthYear = 160 }))
                .Append(new("raider", new(5, 2, 1003, new(70, 70, 70, 70), false) { BirthYear = 160 })).ToArray(),
            Military = content.Military with
            {
                Formations = [basic, basic with { Id = 30, IsCommon = false, Combat = new(3000, 3000, 1, 100, 10) },
                    basic with { Id = 40, IsCommon = false, Combat = new(10000, 1000, 1000, 5000, 10) }],
                Places = content.Military.Places.Select(p => p.Id == 1002 ? p with { Combat = new(1, 1, 1, 2000, 10) } : p).ToImmutableArray()
            }
        };
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1002 ? s with { Soldiers = 20000, Morale = 100, Durability = 10000, MaxDurability = 10000 } :
                s.Id == 1001 ? s with { Soldiers = 0, Morale = 0 } : s).ToArray(),
            Units = [new(50, 1, new(7, 12), 2000) { FormationId = 10, Morale = 100, CorpsId = 3, CommanderId = 3, ServicePlaceId = 1001,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) },
                new(51, 1, new(22, 7), 10000) { FormationId = 40, Morale = 100, CorpsId = 3, CommanderId = 4, ServicePlaceId = 1000,
                    Order = new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 61 } },
                new(60, 2, new(17, 2), 1000) { FormationId = 10, Morale = 100, CorpsId = 2, CommanderId = 5, ServicePlaceId = 1003,
                    Order = new(MilitaryOrderKind.AttackSettlement, null, 1000) },
                new(61, 2, new(52, 12), 3000) { FormationId = 30, Morale = 100, CorpsId = 2, CommanderId = 2, ServicePlaceId = 1003,
                    Order = new(MilitaryOrderKind.AttackSettlement, null, 1002) }],
            Officers = w.Officers.Select(p => p.Id == 1 ? p : p with { SettlementId = null, UnitId = p.Id switch { 2 => 61, 3 => 50, 4 => 51, _ => 60 } }).ToArray()
        }, content);
        var parent = Campaign(engine.Snapshot, 50) with { CorpsId = 3, TargetId = 1002, InitialSoldiers = 12000,
            LastPosition = new(7, 12), TargetDurability = 10000, Support = [new(51, 1000, 3, MilitaryCommitmentRole.Field, 10000, null)] };
        var batch = new MilitaryTurnBatch(null, new() { NextId = 1, Campaigns = [parent] });
        var events = new List<DomainEvent>();
        Run(definition, engine, batch, events);
        var remaining = batch.State.Campaigns.Single(c => c.Id == 0);
        Assert.Empty(remaining.Support);
        Assert.Equal(MilitaryCampaignStage.Withdrawing, remaining.Stage);
        var rescue = Assert.Single(batch.State.Campaigns.Where(c => c.Purpose == MilitaryCampaignPurpose.Relief));
        Assert.Equal(51, rescue.UnitId);
        Assert.Equal(60, engine.Snapshot.Units.Single(u => u.Id == 51).Order!.UnitId);
        Assert.Empty(events.OfType<UnitDeployedEvent>());
        batch.State.Validate(engine.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrappedStaffPreservesItsGarrisonWhileTheOnlyCommanderCanLeaveAnEmptyPost(bool trappedStaff)
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        var formation = content.Military!.Formations[0];
        content = content with
        {
            Officers = trappedStaff ? content.Officers.Append(new("unavailable", new(4, 1, 1001, new(20, 20, 20, 20), true) { BirthYear = 160 })).ToArray() : content.Officers,
            Military = content.Military with { Formations = [formation, formation with { Id = 30, IsCommon = false, Combat = new(100, 3000, 1000, 100, 10) }] }
        };
        var (definition, engine) = Setup(w => w with
        {
            Units = [new(60, 2, new(37, 12), 5000) { FormationId = 30, Morale = 100, CorpsId = 2, CommanderId = 2, ServicePlaceId = 1003,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1001) }],
            Officers = w.Officers.Select(p => p.Id == 2 ? p with { UnitId = 60, SettlementId = null } : p).ToArray()
        }, content);
        var batch = new MilitaryTurnBatch(null, new());
        var events = new List<DomainEvent>();
        var report = Run(definition, engine, batch, events);
        if (trappedStaff)
        {
            Assert.Empty(events.OfType<UnitDeployedEvent>());
            Assert.Equal(20000u, engine.Snapshot.Settlements.Single(s => s.Id == 1001).Soldiers);
            Assert.False(engine.Snapshot.Officers.Single(p => p.Id == 3).Acted);
            Assert.Equal(MilitaryDecisionReason.SourceExposed, report.MilitaryReason);
            Assert.Equal(1, batch.State.EvacuatedPlaces[1001]);
        }
        else
        {
            Assert.Single(events.OfType<UnitDeployedEvent>());
            Assert.NotNull(engine.Snapshot.Officers.Single(p => p.Id == 3).UnitId);
        }
    }

    [Fact]
    public void SourceProtectionSupportsMaximumAuthoredGarrisons()
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        content = content with
        {
            Officers = content.Officers.Append(new("resident", new(4, 1, 1001, new(95, 95, 95, 95), true) { BirthYear = 160 })).ToArray(),
            Military = content.Military! with { Places = content.Military.Places.Select(p => p.Id == 1001 ? p with { Combat = new(1, 1, 1, 5000, 10) } : p).ToImmutableArray() }
        };
        var (definition, engine) = Setup(w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1001 ? s with { Soldiers = 500000, SoldierCapacity = 500000, Durability = 10000, MaxDurability = 10000 } : s).ToArray(),
            Units = [new(60, 2, new(37, 12), 1000) { FormationId = 10, Morale = 100, CorpsId = 2, CommanderId = 2, ServicePlaceId = 1003,
                Order = new(MilitaryOrderKind.AttackSettlement, null, 1001) }],
            Officers = w.Officers.Select(p => p.Id == 2 ? p with { UnitId = 60, SettlementId = null } : p).ToArray()
        }, content);
        var report = Run(definition, engine, new(null, new()), []);
        Assert.True(report.GroupEvaluations > 0);
        Assert.All(engine.Snapshot.Units.Where(u => u.OwnerId == 1), u => Assert.InRange(u.Soldiers, 1u, 10000u));
    }

}
