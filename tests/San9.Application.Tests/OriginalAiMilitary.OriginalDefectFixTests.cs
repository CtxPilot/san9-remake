using System.Collections.Immutable;
using San9.Application.AI;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed partial class OriginalAiMilitaryTests
{
    private static ScenarioInitialState SplitTransportFixture()
    {
        var c = Fixture();
        return c with
        {
            Settlements = c.Settlements.Select(s => s.Id == 1 ? s with { Soldiers = 249000, SoldierCapacity = 250000 } :
                s with { Soldiers = 300000 }).Append(c.Settlements[0] with { Id = 3, X = 3, Soldiers = 100000 }).ToArray(),
            Officers = c.Officers.Select(p => p.State.Id is >= 2 and <= 6 ? p with { State = p.State with { SettlementId = 3 } } : p).ToArray(),
            CityConnections = [new(1, 2), new(2, 1), new(3, 1)],
            MilitaryCityConnections = [new(1, 2), new(2, 1), new(3, 1)],
            RegionCityIds = new(c.RegionCityIds) { [3] = 3 }, RegionNameKeys = new(c.RegionNameKeys) { [3] = "rear" },
            Military = c.Military! with { Places = c.Military.Places.Add(new(3, new(17, 7), new(1, 1))),
                RegionIds = Enumerable.Range(0, 60).Select(i => (int?)(i % 20 == 3 ? 3 : i % 20 < 10 ? 1 : 2)).ToImmutableArray() }
        };
    }

    [Theory]
    [InlineData(249000u, 0u, 0u, 1000u)]
    [InlineData(250000u, 0u, 0u, 0u)]
    [InlineData(249000u, 1000u, 0u, 0u)]
    [InlineData(249000u, 0u, 1000u, 0u)]
    public void OriginalTransportRespectsCapacityWoundedAndAlreadyIncoming(uint soldiers, uint wounded, uint incoming, uint expected)
    {
        var h = new Harness(SplitTransportFixture(), w => w with
        {
            Settlements = w.Settlements.Select(s => s.Id == 1 ? s with { Soldiers = soldiers, Wounded = wounded } : s).ToArray(),
            Officers = w.Officers.Select(p => p.Id == 2 && incoming > 0 ? p with { SettlementId = null, UnitId = 900 } : p).ToArray(),
            Units = incoming == 0 ? [] : [new(900, 1, new(22, 7), incoming)
            {
                FormationId = 500, CommanderId = 2, ServicePlaceId = 3, CorpsId = 1,
                Order = new(MilitaryOrderKind.Transport, null, 1),
                MemberPositions = San9.Tests.FormationTestData.Members(ImmutableDictionary<int, BattleRank>.Empty.Add(2, BattleRank.Middle))
            }],
            Simulation = w.Simulation with { NextUnitId = 901 }
        });
        Assert.Equal(expected == 0 ? 0 : 1, h.Run(OriginalAiWork.Transport, 1).Submitted);
        if (expected > 0)
        {
            var command = Assert.IsType<TransportUnitCommand>(Assert.Single(h.Commands));
            Assert.Equal(3, command.SettlementId);
            Assert.Equal(1, command.DestinationId);
            Assert.Equal(expected, command.Soldiers);
            Assert.Equal(0, h.Run(OriginalAiWork.Transport, 1).Submitted);
            Assert.Single(h.Commands);
        }
        else Assert.Empty(h.Commands);
    }

    [Theory]
    [InlineData(false, 0, 1)]
    [InlineData(false, 80, 0)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 80, 0)]
    public void OriginalAttackExcludesBarbariansWithoutChangingOrdinaryDiplomacy(bool barbarian, int relation, int expected)
    {
        var content = Fixture();
        content = content with
        {
            Forces = content.Forces.Select(f => f.Id == 2 ? f with { IsBarbarian = barbarian } : f).ToArray(),
            Information = content.Information with { Relations = [new(1, 2, relation)] }
        };
        var h = new Harness(content);
        var before = h.Engine.Snapshot;
        Assert.Equal(barbarian, before.Forces.Single(f => f.Id == 2).IsBarbarian);
        Assert.Equal(expected, h.Run(OriginalAiWork.Attack, 1).Submitted);
        if (expected > 0)
            Assert.Equal(2, Assert.IsType<DeployUnitCommand>(Assert.Single(h.Commands)).Order.SettlementId);
        else
        {
            Assert.Empty(h.Commands);
            StateAssert.Equal(before, h.Engine.Snapshot);
        }
    }

    [Fact]
    public void OriginalRecruitmentDoesNotPromoteTheNeighboringRearCity()
    {
        var content = SplitTransportFixture();
        content = content with
        {
            CityConnections = [.. content.CityConnections, new(1, 3)],
            Settlements = content.Settlements.Select(s => s with
                { Soldiers = 1000, DraftPopulation = s.Id == 3 ? 200000u : 100000u }).ToArray()
        };
        var h = new Harness(content);
        var definition = new ScenarioSessionDefinition(content);
        var geography = new OriginalAiGeography(content, definition.Rules.Military, h.Engine.Snapshot);
        var projection = new OriginalAiProjection(h.Engine, content, 1, geography, true);
        var inputs = projection.Domestic();
        var rear = inputs.Sites.Single(s => s.Settlement.Id == 3);
        Assert.False(rear.HasFrontierInCity);
        Assert.True(rear.HasExpandedFrontier);
        Assert.True(inputs.Sites.Single(s => s.Settlement.Id == 1).HasFrontierInCity);
        var commands = new List<DevelopmentCommand>();
        OriginalDomesticWork.Run(DomesticActivity.Recruit, 1, projection.Domestic,
            () => throw new InvalidOperationException("Recruitment does not roll"), command =>
            {
                commands.Add(command);
                Assert.True(h.Engine.Execute(new("player"), new DevelopCommand(Guid.NewGuid(), 1,
                    h.Engine.Snapshot.Simulation.NextSequence, command)).Submission.Accepted);
                return true;
            });
        Assert.Equal(1, Assert.Single(commands).SettlementId);
        Assert.True(h.Engine.Snapshot.Settlements.Single(s => s.Id == 1).Soldiers > 1000);
        Assert.Equal(1000u, h.Engine.Snapshot.Settlements.Single(s => s.Id == 3).Soldiers);
    }

    [Fact]
    public void OriginalRecruitmentIncludesTheCityJurisdictionFrontier()
    {
        var content = SplitTransportFixture();
        content = content with
        {
            Settlements = [.. content.Settlements, content.Settlements[0] with
                { Id = 4, X = 7, Kind = SettlementKind.Facility, Soldiers = 1000 }],
            RegionCityIds = new(content.RegionCityIds) { [4] = 3 },
            RegionNameKeys = new(content.RegionNameKeys) { [4] = "frontier-facility" },
            Military = content.Military! with
            {
                Places = content.Military.Places.Add(new(4, new(37, 7), new(1, 1))),
                RegionIds = content.Military.RegionIds.Select((r, i) => i % 20 is >= 6 and < 10 ? (int?)4 : r).ToImmutableArray()
            }
        };
        var h = new Harness(content);
        var definition = new ScenarioSessionDefinition(content);
        var geography = new OriginalAiGeography(content, definition.Rules.Military, h.Engine.Snapshot);
        Assert.False(geography.Frontier(3, 1));
        Assert.True(geography.Frontier(4, 1));
        Assert.True(geography.HasFrontierInCity(3, 1));
        Assert.True(new OriginalAiProjection(h.Engine, content, 1, geography, true).Domestic()
            .Sites.Single(s => s.Settlement.Id == 3).HasFrontierInCity);
    }
}
