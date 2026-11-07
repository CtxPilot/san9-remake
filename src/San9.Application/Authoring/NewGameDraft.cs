using System.Collections.Immutable;
using San9.Application.Presentation;
using San9.Content.Authoring;
using San9.Content.Scenarios;
using San9.Content.Text;

namespace San9.Application.Authoring;

public sealed class NewGameDraft(ScenarioInitialState source, TextCatalog text,
    IReadOnlyDictionary<string, string> portraits, CreatedOfficerLibrary library)
{
    public ScenarioInitialState Source { get; } = source;
    public TextCatalog Text { get; } = text;
    public NewGameSetup Setup { get; private set; } = new() { Library = library };
    public ImmutableArray<int> PlayerForceIds { get; private set; } = [];
    // Source and the library snapshot are fixed for this visit. Startup edits publish
    // their validated projection together, so queries reuse the same result.
    private NewGameComposition? _composition;
    public NewGameComposition Composition => _composition ??= NewGameComposer.Compose(Source, Setup, PlayerForceIds);
    public ScenarioInitialState Compose() => Composition.Scenario;
    public StrategySnapshot Preview() => new ScenarioSessionDefinition(Compose() with { PlayerForceId = null }, []).CreatePreview();
    public ScenarioForceSelection Selection() => ScenarioForceSelection.Read(Compose(),
        Composition.Definitions.AddText(Text), Composition.Definitions.AddPortraits(portraits));

    public void SetPlayerForces(IEnumerable<int> forceIds)
    {
        var selected = forceIds.Distinct().Order().ToImmutableArray();
        if (PlayerForceIds.SequenceEqual(selected)) return;
        var activated = ScenarioSessionDefinition.ActivatePlayerForces(Source, selected);
        var available = activated.Settlements.Where(s => !s.IsDormant).Select(s => s.Id).ToHashSet();
        var next = Setup with { Placements = Setup.Placements.Where(p => available.Contains(p.CityId)).ToImmutableArray() };
        var composition = NewGameComposer.Compose(Source, next, selected);
        PlayerForceIds = selected;
        Setup = next;
        _composition = composition;
    }

    public void Deploy(IEnumerable<Guid> people, int cityId, bool joinForce)
    {
        var ids = people.ToHashSet();
        if (ids.Any(id => Setup.Forces.Any(f => f.RulerId == id))) throw new InvalidDataException("君主不能重复部署。");
        Change(Setup with { Placements = Setup.Placements.Where(p => !ids.Contains(p.OfficerId))
            .Concat(ids.Order().Select(id => new OfficerPlacement(id, cityId, joinForce))).ToImmutableArray() });
    }

    public void CancelDeployment(IEnumerable<Guid> people)
    {
        var ids = people.ToHashSet();
        Change(Setup with { Placements = Setup.Placements.Where(p => !ids.Contains(p.OfficerId)).ToImmutableArray() });
    }

    public void SetForces(IEnumerable<CreatedForce> forces)
    {
        var next = forces.ToImmutableArray();
        var removedCities = Setup.Forces.Where(f => !next.Contains(f)).Select(f => f.CityId).ToHashSet();
        Change(Setup with { Forces = next, Placements = Setup.Placements.Where(p => !next.Any(f => f.RulerId == p.OfficerId) &&
            !(p.JoinForce && removedCities.Contains(p.CityId))).ToImmutableArray() });
    }

    private void Change(NewGameSetup next)
    {
        var composition = NewGameComposer.Compose(Source, next, PlayerForceIds);
        Setup = next;
        _composition = composition;
    }
}
