using System.Collections.Immutable;
using San9.Application.AI.Military;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Tests;

public sealed partial class MilitaryCoordinationTests
{
    [Fact]
    public void ExistingSiegeUnitUsesPublicSelectionAndKeepsItsCooldownAndActionState()
    {
        var content = MilitaryCampaignSessionTests.Fixture();
        content = content with
        {
            Officers = content.Officers.Select(p => p.State.Id == 3 ? p with { State = p.State with
                { LearnedTactics = [100, 200], EquippedTacticId = 100, TacticCooldown = 9, Acted = true } } : p).ToArray(),
            Military = content.Military! with
            {
                Formations = [content.Military.Formations[0] with { Tactics = new(100, 1, [0], []) }],
                Tactics = [new(100, 0, TacticEffect.Martial, 10000, 0, 0, true, false, [10]),
                    new(200, 3, TacticEffect.Martial, 10, 0, 0, true, true, [10])]
            }
        };
        var (definition, engine) = Setup(w => w with
        {
            Units = [new(50, 1, new(97, 7), 5000) { CorpsId = 3, FormationId = 10, CommanderId = 3,
                Morale = 100, ServicePlaceId = 1001, Order = new(MilitaryOrderKind.AttackSettlement, null, 1003) }],
            Officers = w.Officers.Select(p => p.Id == 3 ? p with { UnitId = 50, SettlementId = null, Acted = true, TacticCooldown = 9 } : p).ToArray()
        }, content);
        var events = new List<DomainEvent>();
        Run(definition, engine, new(null, new()), events);
        Assert.Contains(events.OfType<TacticSelectedEvent>(), e => e.OfficerId == 3 && e.TacticId == 200);
        var actual = engine.Snapshot.Officers.Single(p => p.Id == 3);
        Assert.Equal(200, actual.EquippedTacticId);
        Assert.Equal(9, actual.TacticCooldown);
        Assert.True(actual.Acted);
        Assert.Equal(10, engine.Snapshot.Units.Single(u => u.Id == 50).FormationId);
    }
}
