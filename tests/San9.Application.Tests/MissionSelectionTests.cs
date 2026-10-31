using System.Collections.Immutable;
using San9.Tests;
using San9.Application.Persistence;
using San9.Application.Presentation;
using San9.Core.Domain;
using San9.Content.Interface;
using San9.Content.Text;

namespace San9.Application.Tests;

public sealed class MissionSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SoleRescueTargetIsPreselectedAndAdviserControlsDialogue(bool hasAdviser)
    {
        var basis = MissionScenario.Create();
        var content = basis with
        {
            Forces = basis.Forces.Select(f => f.Id == 1 ? f with { AdviserId = hasAdviser ? 2 : null } : f).ToArray(),
            Officers = basis.Officers.Where(p => hasAdviser || p.State.Id != 2).Select(p => p.State.Id == 2 ?
                p with { State = p.State with { Status = OfficerStatus.Strategist } } : p).ToArray()
        };
        var session = GameSession.Start(content, 42);
        var before = session.Capture();
        var draft = new MissionDraft(session, 1, "rescue");
        Assert.Equal(10, draft.TargetId);
        Assert.False(draft.CanSelectTarget);
        Assert.Equal(hasAdviser, draft.CanSelectActor);
        Assert.True(draft.CanExecute);
        string[] roles = ["strategy-favorable", "strategy-unfavorable"];
        var texts = MissionScenario.Texts(draft.View) with { Messages = roles.ToDictionary(role => role,
            role => new MessageDefinition([new MessageLiteral(role)])) };
        var messages = roles.ToDictionary(role => role, role => new DecisionMessageDefinition(role, [], "adviser"));
        var dialogue = MissionPresentation.Advice(draft, messages, session.Messages(texts), texts);
        if (hasAdviser) Assert.Equal(2, Assert.Single(dialogue).SpeakerId);
        else Assert.Empty(dialogue);
        Assert.Same(before, session.Capture());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void EmptyAndMultipleRescueTargetsDoNotChooseOrAdvise(int count)
    {
        var basis = MissionScenario.Create();
        var unit = basis.Units.Single(u => u.Id == 10);
        var officer = basis.Officers.Single(p => p.State.UnitId == 10);
        var content = basis with
        {
            Units = count == 0 ? basis.Units.Where(u => u.Id != 10).ToArray() :
                [.. basis.Units, unit with { Id = 110, CommanderId = 103 }],
            Officers = count == 0 ? basis.Officers.Where(p => p.State.UnitId != 10).ToArray() :
                [.. basis.Officers, officer with { State = officer.State with { Id = 103, UnitId = 110 } }]
        };
        var session = GameSession.Start(content, 42);
        var before = session.Capture();
        var draft = new MissionDraft(session, 1, "rescue");
        Assert.Equal(count, draft.Targets.Length);
        Assert.Null(draft.TargetId);
        Assert.Null(draft.Advice);
        Assert.False(draft.CanExecute);
        Assert.Equal(count > 1, draft.CanSelectTarget);
        Assert.Same(before, session.Capture());
    }

    [Fact]
    public void SolePrisonerDeterminesRecipientWhileRumorStillRequiresBothForces()
    {
        var basis = MissionScenario.Create();
        var content = basis with
        {
            Officers = basis.Officers.Select(p => p.State.Id == 3 ? p with { State = p.State with
                { UnitId = null, SettlementId = 2, Status = OfficerStatus.Captive } } : p).ToArray(),
            Units = basis.Units.Where(u => u.Id != 10).ToArray()
        };
        var session = GameSession.Start(content, 42);
        var before = session.Capture();
        var returning = new MissionDraft(session, 1, "return");
        Assert.Equal(3, returning.SecondaryId);
        Assert.Equal(2, returning.TargetId);
        Assert.False(returning.CanSelectTarget);
        Assert.True(returning.CanExecute);
        var rumor = new MissionDraft(session, 1, "rumor");
        Assert.Null(rumor.TargetId);
        Assert.Null(rumor.SecondaryId);
        Assert.True(rumor.CanSelectTarget);
        Assert.False(rumor.CanSelectSecondaryTarget);
        Assert.Null(rumor.Advice);
        Assert.True(rumor.SetTarget(2));
        Assert.Null(rumor.SecondaryId);
        Assert.True(rumor.CanSelectSecondaryTarget);
        Assert.Same(before, session.Capture());
    }

    [Theory]
    [InlineData("false-report", 20)]
    [InlineData("disorder", 20)]
    [InlineData("inspire", 10)]
    [InlineData("rescue", 10)]
    public void UnitStrategyMenuAndTargetsRequireDailyCycleEntry(string kind, int target)
    {
        var basis = MissionScenario.Create();
        foreach (bool entered in new[] { false, true })
        {
            var content = basis with { Units = basis.Units.Select(u => u.Id == target ? u with
                { HasEnteredDailyCycle = entered, CommandedThisXun = true, DepartureDaysRemaining = 9,
                    Order = u.Order ?? new(MilitaryOrderKind.Move, new(100, 7), null) } : u).ToArray() };
            var session = GameSession.Start(content, 42);
            var before = session.Capture();
            var draft = new MissionDraft(session, 1, kind);
            var menu = MapCommandMenu.Settlement(session.View, 1).SelectMany(e => e.Children).Single(e => e.Id == kind);
            Assert.Equal(entered, menu.UnavailableReasonKey is null);
            Assert.Equal(entered, draft.Targets.Contains(target));
            Assert.Equal(entered, draft.SetTarget(target));
            Assert.Same(before, session.Capture());
        }
    }

    [Fact]
    public void UnitStrategyTargetsUseCurrentRegionAndExcludeIndirectNeighbor()
    {
        var basis = MissionScenario.Create();
        var content = basis with
        {
            CityConnections = [.. basis.CityConnections, new(2, 3)],
            Units = basis.Units.Select(u => u.Id == 20 ? u with { ServicePlaceId = 2 } : u).ToArray(),
            Military = basis.Military! with
            {
                RegionIds = Enumerable.Range(0, 90).Select(i => (int?)(i % 30 >= 5 ? 3 : 1)).ToImmutableArray()
            }
        };
        var session = GameSession.Start(content, 42);
        var draft = new MissionDraft(session, 1, "false-report");

        Assert.Empty(draft.Targets);
        Assert.False(draft.SetTarget(20));
    }

    [Theory]
    [InlineData("burn", false, CommandTargetMarker.Hostile)]
    [InlineData("seize", false, CommandTargetMarker.Hostile)]
    [InlineData("false-report", false, CommandTargetMarker.Hostile)]
    [InlineData("disorder", false, CommandTargetMarker.Hostile)]
    [InlineData("inspire", false, CommandTargetMarker.Friendly)]
    [InlineData("rescue", false, CommandTargetMarker.Friendly)]
    [InlineData("request", true, CommandTargetMarker.Attack)]
    public void TargetMarkersDistinguishHostileStrategiesFriendlySupportAndAttackRequests(
        string kind, bool secondary, CommandTargetMarker expected)
    {
        var draft = new MissionDraft(GameSession.Start(MissionScenario.Create(),42),1,kind);
        Assert.Equal(expected,draft.MapMarker(secondary));
    }

    [Theory]
    [InlineData("alienate",4)] [InlineData("burn",2)] [InlineData("seize",2)]
    [InlineData("rescue",4)] [InlineData("gift",5)] [InlineData("request",5)]
    public void CommandPrioritySelectsTheRelevantAbilityAndSurvivesPageSwitches(string kind, int first)
    {
        var basis = MissionScenario.Create();
        var content = basis with { Officers = [.. basis.Officers.Select(p => p.State.Id == 2 ? p with { State = p.State with { BaseAbilities = new(95,10,30,20) } } : p),
            new("clever",new(4,1,1,new(10,95,20,95),false)), new("politician",new(5,1,1,new(15,20,95,15),false))] };
        var session = GameSession.Start(content,42);
        var draft = new MissionDraft(session,1,kind);
        var texts = MissionScenario.Texts(draft.View);
        var before = session.Capture();
        Assert.Equal(first,draft.ActorId);
        var pages = MissionOfficerCatalog.Pages(draft,MissionScenario.Texts(draft.View));
        var primary = MissionOfficerCatalog.Read(draft,texts,0);
        Assert.Equal(first,primary[0].Id);
        Assert.Contains(draft.ActorPriorityField,pages[0].Columns.Select(c => c.Id));
        for (int page = 1; page < pages.Count; page++)
            Assert.Equal(primary.Select(r => r.Id),MissionOfficerCatalog.Read(draft,texts,page).Select(r => r.Id));
        Assert.Same(before, session.Capture());
    }

    [Fact]
    public void InspirationUsesIntelligenceBeforeKnowledgeAndKnowledgeBreaksTies()
    {
        var basis = MissionScenario.Create();
        var content = basis with { Officers = [.. basis.Officers,
            new("clever",new(4,1,1,new(10,95,20,20),false)),
            new("inspirer",new(5,1,1,new(10,90,20,20),false) { LearnedTactics = [101] })],
            Military = basis.Military! with { Tactics = [new(101,7,TacticEffect.Inspire,0,0,0,false,false,[])] } };
        var draft = new MissionDraft(GameSession.Start(content,42),1,"inspire");
        Assert.Equal(new[] {4,5,1,2},draft.Actors);
        var rows = MissionOfficerCatalog.Read(draft,MissionScenario.Texts(draft.View),0);
        int column = Array.FindIndex(MissionOfficerCatalog.Pages(draft,MissionScenario.Texts(draft.View))[0].Columns,c => c.Id == "inspiration");
        Assert.Equal("○",rows.Single(r => r.Id == 5).Cells[column].Text);
        Assert.Equal("×",rows.Single(r => r.Id == 4).Cells[column].Text);
    }

    [Fact]
    public void AlienateTargetPageProjectsTravelWithoutChangingDraftOrSimulation()
    {
        var session = GameSession.Start(MissionScenario.Create(),42);
        var draft = new MissionDraft(session,1,"alienate");
        int? selected = draft.TargetId;
        var before = session.Capture();
        var pages = MissionOfficerCatalog.Pages(draft,MissionScenario.Texts(draft.View),targets:true);
        var rows = MissionOfficerCatalog.Read(draft,MissionScenario.Texts(draft.View),0,targets:true);
        var row = rows.Single(r => r.Id == 22);
        var arrival = row.Cells[Array.FindIndex(pages[0].Columns,c => c.Id == "estimate")];
        Assert.True(arrival.Number > 0);
        Assert.Equal(selected,draft.TargetId);
        Assert.Same(before, session.Capture());
    }

    [Fact]
    public void MapSelectionKeepsFacilityAndUnitIdentityDistinct()
    {
        var basis = MissionScenario.Create();
        var content = basis with { Units = basis.Units.Select(u => u.Id == 10 ? u with { Id = 2 } : u).ToArray(),
            Officers = basis.Officers.Select(p => p.State.UnitId == 10 ? p with { State = p.State with { UnitId = 2 } } : p).ToArray() };
        var session = GameSession.Start(content,42);
        var before = session.Capture();
        var facility = new CommandTargetSelection(session.View,CommandMapTargetKind.Settlement,[2],CommandTargetMarker.Hostile);
        var unit = new CommandTargetSelection(session.View,CommandMapTargetKind.Unit,[2],CommandTargetMarker.Friendly);
        Assert.Equal(2,facility.Resolve(new CommandMapObject(2,false)));
        Assert.Null(facility.Resolve(new CommandMapObject(2,true)));
        Assert.Equal(2,unit.Resolve(new CommandMapObject(2,true)));
        Assert.Null(unit.Resolve(new CommandMapObject(2,false)));
        Assert.Same(before, session.Capture());
    }

}
