using System.Collections.Immutable;
using San9.Application.AI;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed partial class OriginalAiMilitaryTests
{
    [Fact]
    public void ConstructionInAnEmptyTransitRegionDoesNotCancelTheAttack()
    {
        var content = WithConstruction(Fixture());
        var h = new Harness(content, w => w with
        {
            Officers = w.Officers.Select(p => p.Id == 12 ? p with { SettlementId = null, UnitId = 900 } : p).ToImmutableArray(),
            Units = [new(900, 1, new(17, 12), 1000)
            {
                FormationId = 600, CorpsId = 1, ServicePlaceId = 1,
                Order = new(MilitaryOrderKind.Construct, null, null) { Construction = new(3, 700, new(27, 7), 12) },
                MemberPositions = ImmutableDictionary<int, FormationPosition>.Empty.Add(12, new(0, BattleRank.Middle))
            }],
            Simulation = w.Simulation with { NextUnitId = 901 }
        });
        AssertAttackAlignmentCommandAvailable(content, h.Engine, 20000);
        Assert.DoesNotContain(h.Engine.Snapshot.Settlements, s => s.Id == 3);
        Assert.Equal(1, h.Run(OriginalAiWork.Attack, 1).Submitted);
        Assert.Equal(2, Assert.IsType<DeployUnitCommand>(Assert.Single(h.Commands)).Order.SettlementId);
    }

    [Theory]
    [InlineData(SettlementKind.City, 2, true)]
    [InlineData(SettlementKind.Facility, null, true)]
    [InlineData(SettlementKind.Facility, 2, false)]
    public void InterveningTargetKeepsItsStageWithoutNewKindOwnershipOrFrontlineGates(SettlementKind kind, int? owner, bool front)
    {
        var basis = Fixture();
        var content = basis with
        {
            Settlements = [.. basis.Settlements, basis.Settlements[1] with
                { Id = 3, X = 5, Y = 1, Kind = kind, OwnerId = owner, CorpsId = null }],
            RegionCityIds = new(basis.RegionCityIds) { [3] = kind == SettlementKind.City ? 3 : 2, [4] = 1 },
            RegionNameKeys = new(basis.RegionNameKeys) { [3] = "intervening", [4] = "gap" },
            UnbuiltPlaces = front ? [] : [new(4, new(17, 7))],
            Military = basis.Military! with
            {
                Places = basis.Military.Places.Add(new(3, new(27, 7), new(1, 1)) { Combat = basis.Military.Places[1].Combat }),
                RegionIds = Enumerable.Range(0, 60).Select(i => (int?)(i % 20 < 3 ? 1 :
                    i % 20 < 5 && !front ? 4 : i % 20 < 7 ? 3 : 2)).ToImmutableArray()
            }
        };
        var h = new Harness(content);
        Assert.Equal(1, h.Military.Run(OriginalAiWork.Attack, 20, h.State,
            new(1, OriginalAiMode.TargetSettlement, null, 2)).Submitted);
        Assert.Equal(3, Assert.IsType<DeployUnitCommand>(Assert.Single(h.Commands)).Order.SettlementId);
    }

    [Fact]
    public void ConstructionCanUseAnotherSourceWhenTheRouteOriginHasOnlyOneIdleOfficer()
    {
        var basis = AttackAlignmentConstruction();
        var content = basis with
        {
            Settlements = [.. basis.Settlements, basis.Settlements[0] with
                { Id = 4, X = 2, Soldiers = 50000, Kind = SettlementKind.Facility }],
            Officers = basis.Officers.Select(p => p.State.OwnerId == 1 && p.State.Id != 1
                ? p with { State = p.State with { SettlementId = 4 } } : p).ToArray(),
            RegionCityIds = new(basis.RegionCityIds) { [4] = 1 },
            RegionNameKeys = new(basis.RegionNameKeys) { [4] = "builder" },
            Military = basis.Military! with
                { Places = basis.Military.Places.Add(new(4, new(12, 7), new(1, 1)) { FacilityTypeId = 700 }) }
        };
        var h = new Harness(content);
        Assert.Equal(1, h.Run(OriginalAiWork.Attack).Submitted);
        var command = Assert.IsType<ConstructFacilityCommand>(Assert.Single(h.Commands));
        Assert.Equal(4, command.SettlementId);
        Assert.Equal(3, command.RegionId);
    }

    [Fact]
    public void ConstructionConflictOnAnAdditionalSourcesRouteDoesNotExcludeItsTroops()
    {
        var basis = WithConstruction(Fixture());
        var content = basis with
        {
            Settlements = [.. basis.Settlements,
                basis.Settlements[0] with { Id = 3, X = 1, Y = 7, Soldiers = 0, Morale = 0,
                    Kind = SettlementKind.Facility, CanHostPersons = false, MerchantPresenceChance = 0, Icons = [] },
                basis.Settlements[0] with { Id = 4, X = 1, Y = 12, Soldiers = 50000 }],
            UnbuiltPlaces = [],
            Officers = basis.Officers.Select(p => p.State.OwnerId == 1 && p.State.Id is > 1 and < 12
                ? p with { State = p.State with { SettlementId = 4 } } : p).ToArray(),
            RegionCityIds = new(basis.RegionCityIds) { [4] = 4 },
            RegionNameKeys = new(basis.RegionNameKeys) { [4] = "additional-source" },
            Military = basis.Military! with
            {
                Height = 20, TerrainIds = Enumerable.Repeat(0, 400).ToImmutableArray(),
                TerritoryExits = Enumerable.Repeat(TerritoryExits.All, 400).ToImmutableArray(),
                Places = basis.Military.Places.Add(new(3, new(7, 37), new(1, 1)) { FacilityTypeId = 700 })
                    .Add(new(4, new(7, 62), new(1, 1)) { FacilityTypeId = 700 }),
                RegionIds = Enumerable.Range(0, 400).Select(i => (int?)(i / 20 >= 10 ? 4 : i / 20 >= 4 ? 3 :
                    i % 20 < 10 ? 1 : 2)).ToImmutableArray(),
                ConstructionSites = [new(3, new(27, 37), [700])]
            }
        };
        var h = new Harness(content, w => w with
        {
            Officers = w.Officers.Select(p => p.Id == 12 ? p with { SettlementId = null, UnitId = 900 } : p).ToImmutableArray(),
            Units = [new(900, 1, new(87, 57), 1000)
            {
                FormationId = 600, CorpsId = 1, ServicePlaceId = 1,
                Order = new(MilitaryOrderKind.Construct, null, null) { Construction = new(3, 700, new(27, 37), 12) },
                MemberPositions = ImmutableDictionary<int, FormationPosition>.Empty.Add(12, new(0, BattleRank.Middle))
            }],
            Simulation = w.Simulation with { NextUnitId = 901 }
        });
        Assert.Equal(1, h.Run(OriginalAiWork.Attack).Submitted);
        var command = Assert.IsType<DeployUnitCommand>(Assert.Single(h.Commands));
        Assert.Equal(4, command.SettlementId);
        Assert.Equal(2, command.Order.SettlementId);
    }
}
