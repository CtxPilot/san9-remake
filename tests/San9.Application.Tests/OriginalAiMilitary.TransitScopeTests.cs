using System.Collections.Immutable;
using San9.Application.AI;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed partial class OriginalAiMilitaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FavoredReserveUsesIncomingThreatRatherThanOutgoingAttackScope(bool incoming)
    {
        var basis = AttackReserveFixture();
        var content = basis with { Military = basis.Military! with
        {
            TerrainIds = Enumerable.Range(0, 60).Select(i => i % 20 == (incoming ? 19 : 1) ? 1 : 0).ToImmutableArray(),
            Terrains = [new(0, 1, null), new(1, 700, null)],
            Formations = basis.Military.Formations.Select(f => f with { MovementBudget = 1000 }).ToImmutableArray()
        } };
        var h = new Harness(content);
        var network = h.TerrainRoutes.TransitNetwork(h.Rules, h.Engine.Snapshot);
        Assert.Equal(incoming, network.Find(3, 1) is not null);
        Assert.Equal(!incoming, network.Find(1, 3) is not null);
        Assert.Equal(incoming ? 0 : 1, h.Run(OriginalAiWork.Attack, 1, OriginalAiGroup.Favored).Submitted);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(200, false)]
    public void ReliefDoesNotUseParentCityToAdmitAnOutOfScopeSource(int cost, bool connected)
    {
        var basis = SupportReliefFixture();
        var content = basis with { Military = basis.Military! with
        {
            Terrains = [new(0, cost, null)],
            Formations = basis.Military.Formations.Select(f => f with { MovementBudget = 1000 }).ToImmutableArray()
        } };
        var h = new Harness(content, w => EnemyArmy(w, target: 3));
        var network = h.TerrainRoutes.TransitNetwork(h.Rules, h.Engine.Snapshot);
        Assert.Equal(connected, network.Find(1, 3) is not null);
        Assert.Equal(connected ? 1 : 0, h.Run(OriginalAiWork.Deploy, 1).Submitted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestSourcesUseForwardReferenceEdgesEvenWhenTheReverseAndActualRouteDisagree(bool forward)
    {
        var basis = Fixture();
        var content = basis with
        {
            CityConnections = [],
            Forces = basis.Forces.Append(new(3, "requester", 3, 50) { LeaderId = 101 }).ToArray(),
            Settlements = basis.Settlements.Append(basis.Settlements[1] with
                { Id = 3, OwnerId = 3, CorpsId = 3, X = 17 }).ToArray(),
            Officers = basis.Officers.Append(new("requester", new(101, 3, 3, new(30, 30, 30, 30), false)
                { Status = OfficerStatus.Ruler })).ToArray(),
            RegionCityIds = new(basis.RegionCityIds) { [3] = 3 },
            RegionNameKeys = new(basis.RegionNameKeys) { [3] = "requester" },
            Information = basis.Information with { Corps = basis.Information.Corps
                .Append(new(3, 3, 101, new(1000, 1000)) { IsRoot = true }).ToArray() },
            Military = basis.Military! with
            {
                Places = basis.Military.Places.Add(new(3, new(87, 7), new(1, 1))),
                TerrainIds = Enumerable.Range(0, 60).Select(i => i % 20 == (forward ? 1 : 13) ? 1 : 0).ToImmutableArray(),
                Terrains = [new(0, 1, null), new(1, 700, null)],
                Formations = basis.Military.Formations.Select(f => f with { MovementBudget = 1000 }).ToImmutableArray()
            }
        };
        var h = new Harness(content, w => w with
            { Simulation = w.Simulation with { AttackRequests = [new(1, 3, 2, 2, 19)] } });
        var network = h.TerrainRoutes.TransitNetwork(h.Rules, h.Engine.Snapshot);
        Assert.Equal(forward, network.Find(1, 2) is not null);
        Assert.Equal(!forward, network.Find(2, 1) is not null);
        var estimate = new MilitaryRouteQueryContext(h.Engine.Snapshot, h.Rules).Estimate(
            new(0, 1, new(7, 7), 10000) { FormationId = 400, Order = new(MilitaryOrderKind.AttackSettlement, null, 2) }, null);
        Assert.Equal(RouteReachability.Reachable, estimate.Status);
        Assert.InRange(estimate.Days!.Value, 0, 60);
        Assert.Equal(forward ? 1 : 0, h.Run(OriginalAiWork.FulfillRequest, 1).Submitted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterceptionDoesNotExpandIntoAnEmptyRegionEvenWhenItsParentCityIsLocal(bool physicalEndpoint)
    {
        var basis = WithConstruction(Fixture());
        var content = basis with
        {
            RegionNameKeys = new(basis.RegionNameKeys) { [4] = "field" },
            RegionCityIds = new(basis.RegionCityIds) { [4] = 1 },
            UnbuiltPlaces = physicalEndpoint ? basis.UnbuiltPlaces : basis.UnbuiltPlaces.Add(new(4, new(57, 7))),
            Settlements = physicalEndpoint ? basis.Settlements.Append(basis.Settlements[1] with
                { Id = 4, X = 11, Y = 2, Kind = SettlementKind.Facility }).ToArray() : basis.Settlements,
            Military = basis.Military! with
            {
                Places = physicalEndpoint ? basis.Military.Places.Add(basis.Military.Places[1] with
                    { Id = 4, Position = new(57, 12) }) : basis.Military.Places,
                RegionIds = basis.Military.RegionIds.Select((id, i) => i % 20 == 11 ? (int?)4 : id).ToImmutableArray()
            }
        };
        var h = new Harness(content, w =>
        {
            var enemy = EnemyArmy(w);
            return enemy with { Units = enemy.Units.Select(u => u with
                { FormationId = 600, Order = EnemyConstruction() }).ToImmutableArray() };
        });
        h.TerrainRoutes.TransitNetwork(h.Rules, h.Engine.Snapshot);
        var report = h.Run(OriginalAiWork.Deploy, 1);
        Assert.Equal(physicalEndpoint ? 1 : 0, report.Submitted);
        if (!physicalEndpoint) Assert.Equal(0, report.PathQueries);
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(80, false)]
    public void TransportCannotExpandPastReferenceScopeEvenWithFastActualMovement(int cost, bool connected)
    {
        var basis = PositionAuditTransportFixture();
        var content = basis with { Military = basis.Military! with { Terrains = [new(0, cost, null)] } };
        var h = new Harness(content);
        var network = h.TerrainRoutes.TransitNetwork(h.Rules, h.Engine.Snapshot);
        Assert.Equal(connected, network.Find(3, 1) is not null);
        var estimate = new MilitaryRouteQueryContext(h.Engine.Snapshot, h.Rules).Estimate(
            new(0, 1, new(17, 7), 5000) { FormationId = 400, Order = new(MilitaryOrderKind.Return, null, 1) }, null);
        Assert.Equal(RouteReachability.Reachable, estimate.Status);
        Assert.InRange(estimate.Days!.Value, 0, 60);
        Assert.Equal(connected ? 1 : 0, h.Run(OriginalAiWork.Transport).Submitted);
    }
}
