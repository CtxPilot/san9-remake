using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using San9.Application.Persistence;
using San9.Application.Presentation;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Tests;

namespace San9.Application.Tests;

public sealed class FormationPositionRegressionTests
{
    [Fact]
    public void DraftColumnsRemainDistinctInSubmittedPositions()
    {
        var draft = new DeploymentDraft(DeploymentDraftTests.View(), 1);
        Assert.True(draft.SetMembers([1]));
        Assert.True(draft.SetFormation(100, false));
        var targets = new DeploymentTargets(draft);
        Assert.True(targets.Select(new(2, false)));
        Assert.True(targets.BeginPolicies());
        var id = Guid.NewGuid();
        Assert.True(draft.Move(1, new(0, 1)));
        var left = targets.CreateCommand(id, 0, true);
        Assert.True(draft.Move(1, new(2, 1)));
        var right = targets.CreateCommand(id, 0, true);
        Assert.Equal(new FormationPosition(0, BattleRank.Middle), left.Positions[1]);
        Assert.Equal(new FormationPosition(2, BattleRank.Middle), right.Positions[1]);
    }
}

public sealed partial class MilitarySessionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void FullPositionSurvivesDeploymentDetailsSaveAndCommandLog(int column)
    {
        var source = Fixture();
        var content = source with
        {
            Officers = [.. source.Officers, source.Officers[0] with
                { State = source.Officers[0].State with { Id = 2, BaseAbilities = new(50, 50, 50, 50) } }],
            Military = source.Military! with
            {
                Formations = source.Military.Formations.Select(f => f with
                    { Positions = [new(0, BattleRank.Middle), new(1, BattleRank.Middle), new(2, BattleRank.Middle), new(0, BattleRank.Rear), new(1, BattleRank.Rear)] }).ToImmutableArray()
            }
        };
        var session = GameSession.Start(content, 42);
        var header = CommandLogService.CreateHeader(session);
        var position = new FormationPosition(column, BattleRank.Middle);
        var command = new DeployUnitCommand(Guid.NewGuid(), 1, 0, 1, [2, 1], 400, 7, new(MilitaryOrderKind.Return, null, 2))
            { Positions = ImmutableDictionary<int, FormationPosition>.Empty.Add(1, position).Add(2, new(1, BattleRank.Middle)) };
        var request = session.Request(new CommandOperation(command), "player", command.CommandId);
        Assert.Equal(SessionDisposition.Accepted, session.Apply(request).Disposition);
        Assert.Equal(position, session.Snapshot.Units.Single().MemberPositions[1]);
        var bytes = SaveService.Encode(session);
        var restored = SaveService.Decode(bytes, content);
        Assert.Equal(position, restored.Snapshot.Units.Single().MemberPositions[1]);
        var board = UnitDetails.Read(restored.View, restored.View.MilitaryRules, MissionScenario.Texts(restored.View), restored.Snapshot.Units.Single().Id).Board;
        Assert.Equal(new San9.Content.Scenarios.FormationSlot(column, 1), board.Members.Single(m => m.OfficerId == 1).Position);
        Assert.Equal(new San9.Content.Scenarios.FormationSlot(1, 1), board.Members.Single(m => m.OfficerId == 2).Position);
        Assert.True(board.Members.Single(m => m.OfficerId == 1).IsCommander);
        string path = Path.Combine(Path.GetTempPath(), $"formation-position-{Guid.NewGuid():N}.jsonl");
        try
        {
            CommandLogService.Write(path, header, [request]);
            var replayed = GameSession.Start(content, 42);
            Assert.Null(CommandLogService.Replay(path, replayed).FirstFailure);
            StateAssert.Equal(session.Capture(), replayed.Capture());
        }
        finally { File.Delete(path); }

        var save = JsonNode.Parse(bytes)!;
        var unit = save["session"]!["world"]!["units"]![0]!;
        unit.AsObject().Remove("memberPositions");
        Assert.Equal(SaveFailure.InvalidFormat, Assert.Throws<SaveException>(() =>
            SaveService.Decode(Encoding.UTF8.GetBytes(save.ToJsonString()), content)).Failure);
    }

    [Theory]
    [InlineData(-1, BattleRank.Middle)]
    [InlineData(0, (BattleRank)3)]
    public void InvalidDeploymentPositionIsRejectedWithoutChangingTheWorld(int column, BattleRank rank)
    {
        var session = GameSession.Start(Fixture(), 42);
        var before = session.Capture();
        var command = new DeployUnitCommand(Guid.NewGuid(), 1, 0, 1, [1], 400, 7, new(MilitaryOrderKind.Return, null, 2))
            { Positions = ImmutableDictionary<int, FormationPosition>.Empty.Add(1, new(column, rank)) };

        Assert.Equal(CommandRejection.InvalidTask, session.Execute(new("player"), command).Submission.Rejection);
        StateAssert.Equal(before, session.Capture());
    }
}

public sealed partial class OfficerCommandTests
{
    [Fact]
    public void UnitDetailsKeepUnknownMemberPositionsUnspecified()
    {
        var world = Deployed();
        world = world with { Units = world.Units.Select(u => u with
            { MemberPositions = ImmutableDictionary<int, FormationPosition>.Empty }).ToArray() };
        var view = new StrategySnapshot(definition, world);

        var members = UnitDetails.Read(view, view.MilitaryRules, texts, 10).Board.Members;

        Assert.NotEmpty(members);
        Assert.All(members, member => Assert.Null(member.Position));
    }
}
