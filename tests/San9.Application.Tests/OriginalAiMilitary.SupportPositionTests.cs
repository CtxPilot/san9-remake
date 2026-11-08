using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed partial class OriginalAiMilitaryTests
{
    [Theory]
    [InlineData(false, BattleRank.Front)]
    [InlineData(false, BattleRank.Middle)]
    [InlineData(false, BattleRank.Rear)]
    [InlineData(true, BattleRank.Front)]
    [InlineData(true, BattleRank.Middle)]
    [InlineData(true, BattleRank.Rear)]
    public void SinglePersonSupportUsesTheCenterRatherThanTheFifthFormationCandidate(bool transport, BattleRank fifth)
    {
        var content = transport ? PositionAuditTransportFixture() : SupportReliefFixture();
        content = content with
        {
            Military = content.Military! with
            {
                Formations = content.Military.Formations.Select(f => f.Id == 400 ? f with
                {
                    Positions = [new(1, BattleRank.Front), new(0, BattleRank.Middle), new(1, BattleRank.Middle), new(2, BattleRank.Middle), new(0, fifth)]
                } : f).ToImmutableArray()
            }
        };
        var h = new Harness(content, transport ? null : w => EnemyArmy(w, target: 3),
            draw: _ => throw new InvalidOperationException("Single-person fallback must not draw"));
        Assert.Equal(1, h.Run(transport ? OriginalAiWork.Transport : OriginalAiWork.Deploy).Submitted);
        var command = Assert.IsType<DeployUnitCommand>(Assert.Single(h.Commands));
        var person = Assert.Single(command.OfficerIds);
        Assert.Equal(transport ? 3 : 1, command.SettlementId);
        Assert.Equal(MilitaryOrderKind.Return, command.Order.Kind);
        Assert.Equal(new FormationPosition(1, BattleRank.Middle), command.Positions[person]);
        Assert.Equal(command.Positions[person], h.Engine.Snapshot.Units.Single(u => u.OwnerId == 1).MemberPositions[person]);
    }

    private static ScenarioInitialState PositionAuditTransportFixture()
    {
        var content = Fixture();
        return content with
        {
            Settlements = content.Settlements.Select(s => s with { X = s.Id == 1 ? 13 : 23 }).Append(content.Settlements[0] with
                { Id = 3, X = 3, Soldiers = 5000, Kind = SettlementKind.Facility })
                .Append(content.Settlements[0] with { Id = 4, X = 27, Soldiers = 0, Morale = 0 }).ToArray(),
            Officers = content.Officers.Select(p => p.State.Id == 2 ? p with
                { State = p.State with { SettlementId = 3 } } : p).ToArray(),
            CityConnections = [new(1, 2), new(2, 1), new(4, 1)],
            RegionCityIds = new() { [1] = 1, [2] = 2, [3] = 4, [4] = 4 },
            RegionNameKeys = new() { [1] = "region.1", [2] = "region.2", [3] = "region.3", [4] = "region.4" },
            Military = content.Military! with
            {
                Width = 30, TerrainIds = Enumerable.Repeat(0, 90).ToImmutableArray(),
                Terrains = [new(0, 40, null)],
                Formations = content.Military.Formations.Select(f => f with { MovementBudget = 100 }).ToImmutableArray(),
                Places = content.Military.Places.Select(p => p with { Position = new(p.Id == 1 ? 67 : 117, 7) }).ToImmutableArray().Add(new(3, new(17, 7), new(1, 1)))
                    .Add(new(4, new(137, 7), new(1, 1))),
                RegionIds = Enumerable.Range(0, 90).Select(i =>
                    (int?)(i % 30 < 6 ? 3 : i % 30 < 20 ? 1 : i % 30 > 26 ? 4 : 2)).ToImmutableArray(),
                TerritoryExits = Enumerable.Repeat(TerritoryExits.All, 90).ToImmutableArray()
            }
        };
    }
}
