using System.Collections.Immutable;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Core.Domain;

namespace San9.Application.Authoring;

public sealed partial class SharedDataEditorWorkspace
{
    private readonly Dictionary<int, (FormationRule Rule, FormationCatalogDefinition Catalog)> _formationBaselines;
    private readonly Dictionary<int, (TacticRule Rule, TacticCatalogDefinition Catalog)> _tacticBaselines;

    public FormationRule GetFormation(int id) => Formations.Single(f => f.Id == id);
    public bool IsFormationChanged(int id) => !ContentComparison.Equal(GetFormation(id), _saved.Military!.Formations.SingleOrDefault(f => f.Id == id)) ||
        !ContentComparison.Equal(GetFormationCatalog(id), _saved.Information.Formations.SingleOrDefault(f => f.Id == id));

    public void SetFormation(FormationRule formation)
    {
        ContentStructure.Validate(formation);
        var formations = Definitions.Military?.Formations ?? [];
        int index = -1;
        for (int i = 0; i < formations.Length; i++) if (formations[i].Id == formation.Id) { index = i; break; }
        if (index < 0) throw new InvalidDataException("只能修改现有阵形，不能改变阵形身份。");
        Definitions = Definitions with { Military = Definitions.Military! with { Formations = formations.SetItem(index, formation) } };
        Definitions = ProjectFormation(Definitions, _formationBaselines[formation.Id], formation);
    }

    public void RestoreFormation(int id)
    {
        SetFormation(_formationBaselines[id].Rule);
        SetFormationCatalog(_formationBaselines[id].Catalog);
    }
    public FormationCatalogDefinition GetFormationCatalog(int id) => Definitions.Information.Formations.Single(f => f.Id == id);
    public void SetFormationCatalog(FormationCatalogDefinition value)
    {
        var entries = Definitions.Information.Formations.ToArray();
        int index = Array.FindIndex(entries, f => f.Id == value.Id);
        if (index < 0) throw new InvalidDataException("找不到阵形。");
        entries[index] = value;
        Definitions = Definitions with { Information = Definitions.Information with { Formations = entries } };
    }

    public TacticRule GetTactic(int id) => Tactics.Single(t => t.Id == id);
    public TacticCatalogDefinition GetTacticCatalog(int id) => Definitions.Information.Tactics.Single(t => t.Id == id);
    public bool IsTacticChanged(int id) => !ContentComparison.Equal(GetTactic(id), _saved.Military!.Tactics.SingleOrDefault(t => t.Id == id)) ||
        !ContentComparison.Equal(GetTacticCatalog(id), _saved.Information.Tactics.SingleOrDefault(t => t.Id == id));
    public void SetTactic(TacticRule value)
    {
        ContentStructure.Validate(value);
        var tactics = Definitions.Military?.Tactics ?? [];
        int index = -1;
        for (int i = 0; i < tactics.Length; i++) if (tactics[i].Id == value.Id) { index = i; break; }
        if (index < 0) throw new InvalidDataException("只能修改现有兵法。");
        if (value.Effect != _tacticBaselines[value.Id].Rule.Effect)
            throw new InvalidDataException("本页保留兵法效果类型。");
        Definitions = Definitions with { Military = Definitions.Military! with { Tactics = tactics.SetItem(index, value) } };
    }
    public void SetTacticCatalog(TacticCatalogDefinition value)
    {
        var entries = Definitions.Information.Tactics.ToArray();
        int index = Array.FindIndex(entries, t => t.Id == value.Id);
        if (index < 0) throw new InvalidDataException("找不到兵法。");
        entries[index] = value;
        Definitions = Definitions with { Information = Definitions.Information with { Tactics = entries } };
    }
    public void RestoreTactic(int id)
    {
        SetTactic(_tacticBaselines[id].Rule);
        SetTacticCatalog(_tacticBaselines[id].Catalog);
    }

    public bool IsCreatedFormation(int id) => Formations.Any(f => f.Id == id) &&
        _baseDefinitions.Military?.Formations.Any(f => f.Id == id) != true;
    public bool IsCreatedTactic(int id) => Tactics.Any(t => t.Id == id) &&
        _baseDefinitions.Military?.Tactics.Any(t => t.Id == id) != true;
    public IReadOnlyList<FormationCatalogDefinition> FormationAppearances => _baseDefinitions.Information.Formations
        .DistinctBy(f => f.MapAppearanceId).ToArray();
    public IReadOnlyList<TacticCatalogDefinition> TacticAppearances(TacticEffect effect) => _baseDefinitions.Information.Tactics
        .Where(t => t.BattleAppearanceId is not null && _baseDefinitions.Military!.Tactics.Any(r => r.Id == t.Id && r.Effect == effect))
        .DistinctBy(t => t.BattleAppearanceId).ToArray();

    public async Task<FormationRule> CreateFormationAsync(int? copyFrom = null, CancellationToken token = default)
    {
        var draft = Definitions;
        var source = copyFrom is int original ? GetFormation(original) : null;
        var appearance = source is not null ? GetFormationCatalog(source.Id) :
            FormationAppearances.FirstOrDefault(f => _baseDefinitions.Military!.Formations.Any(r => r.Id == f.Id && r.TravelMode == TravelMode.Land))
                ?? throw new InvalidDataException("请先提供可复用的陆上阵形外观。");
        int id = await AllocateDefinitionIdentityAsync(DefinitionKind.Formation, token);
        EnsureDefinitionDraft(draft, token);
        var rule = source is not null ? source with { Id = id } :
            new FormationRule(id, 50, 0, 100000, 1, new(3, 3), TravelMode.Land)
            {
                Positions = [new(1, BattleRank.Middle)], Vision = new(15, 360),
                Combat = new(10, 10, 0, 10, 50), Attack = new(10, false), Tactics = new(15, 2, [], [])
            };
        var catalog = source is not null ? appearance with { Id = id, Name = appearance.Name + "副本" } :
            new FormationCatalogDefinition(id, "新阵形") { MapAppearanceId = appearance.MapAppearanceId,
                Presentation = new(3, 3, [new(1, 1)], 3, 360, null, null) };
        Definitions = draft with
        {
            NextFormationId = (long)id + 1,
            Military = draft.Military! with { Formations = draft.Military.Formations.Add(rule) },
            Information = draft.Information with { Formations = [.. draft.Information.Formations, catalog] }
        };
        _formationBaselines.Add(id, (rule, catalog));
        return rule;
    }

    public async Task<TacticRule> CreateTacticAsync(int? copyFrom = null, CancellationToken token = default)
    {
        if (Definitions.Military is null) throw new InvalidDataException("内容库没有军事规则。");
        var draft = Definitions;
        var source = copyFrom is int original ? GetTactic(original) : null;
        int id = await AllocateDefinitionIdentityAsync(DefinitionKind.Tactic, token);
        EnsureDefinitionDraft(draft, token);
        var rule = source is not null ? source with { Id = id } : new TacticRule(id, 0, TacticEffect.Martial, 10, 5, 100, true, false, []);
        var catalog = source is not null ? GetTacticCatalog(source.Id) with { Id = id, Name = GetTacticCatalog(source.Id).Name + "副本" } :
            new TacticCatalogDefinition(id, "新兵法") { BattleAppearanceId = TacticAppearances(TacticEffect.Martial).FirstOrDefault()?.BattleAppearanceId };
        Definitions = draft with
        {
            NextTacticId = (long)id + 1,
            Military = draft.Military with { Tactics = draft.Military.Tactics.Add(rule) },
            Information = draft.Information with { Tactics = [.. draft.Information.Tactics, catalog] }
        };
        _tacticBaselines.Add(id, (rule, catalog));
        return rule;
    }

    public async Task DeleteFormationAsync(int id, CancellationToken token = default)
    {
        if (!IsCreatedFormation(id)) throw new InvalidDataException("仅能删除自建阵形，原有阵形可编辑、可复制。");
        var draft = Definitions;
        var candidate = draft with
        {
            Military = draft.Military! with { Formations = draft.Military.Formations.Remove(GetFormation(id)) },
            Information = draft.Information with { Formations = draft.Information.Formations.Where(f => f.Id != id).ToArray() }
        };
        await ValidateDefinitionDeletionAsync(draft, candidate, "阵形", token);
        _formationBaselines.Remove(id);
    }

    public async Task DeleteTacticAsync(int id, CancellationToken token = default)
    {
        if (!IsCreatedTactic(id)) throw new InvalidDataException("仅能删除自建兵法，原有兵法可编辑、可复制。");
        var draft = Definitions;
        var candidate = draft with
        {
            Military = draft.Military! with { Tactics = draft.Military.Tactics.Remove(GetTactic(id)) },
            Information = draft.Information with { Tactics = draft.Information.Tactics.Where(t => t.Id != id).ToArray() }
        };
        await ValidateDefinitionDeletionAsync(draft, candidate, "兵法", token);
        _tacticBaselines.Remove(id);
    }

    private static void ValidateMilitaryProfiles(ScenarioDefinitions definitions, IEnumerable<OfficerDefaults> profiles, string scope)
    {
        var tactics = (definitions.Military?.Tactics ?? []).Select(t => t.Id).ToHashSet();
        foreach (var person in profiles)
            if (person.LearnedTactics.FirstOrDefault(id => !tactics.Contains(id), -1) is >= 0 and var missing)
                throw new InvalidDataException($"{scope}：{person.Name}（{person.Id}）的已学兵法引用不存在的兵法 {missing}。");
    }
    private static ScenarioDefinitions ProjectFormation(ScenarioDefinitions definitions,
        (FormationRule Rule, FormationCatalogDefinition Catalog) baseline, FormationRule rule)
    {
        int index = Array.FindIndex(definitions.Information.Formations, f => f.Id == rule.Id);
        if (index < 0 || baseline.Catalog.Presentation is not { } presentation)
            return definitions;
        var original = baseline.Rule;
        bool changedPositions = !rule.Positions.SequenceEqual(original.Positions);
        bool changedVision = rule.Vision != original.Vision;
        int visionRadius = rule.Vision is { } vision
            ? (int)Math.Min(int.MaxValue, ((long)vision.Radius + MapCoordinates.UnitsPerCell - 1) / MapCoordinates.UnitsPerCell) : 0;
        var catalogs = definitions.Information.Formations.ToArray();
        catalogs[index] = catalogs[index] with { Presentation = presentation with
        {
            Columns = changedPositions ? Math.Max(1, rule.Positions.Select(p => p.Column + 1).DefaultIfEmpty(1).Max()) : presentation.Columns,
            Slots = changedPositions ? rule.Positions.Select(p => new FormationSlot(p.Column, p.Row)).ToImmutableArray() : presentation.Slots,
            VisionRadius = changedVision ? visionRadius : presentation.VisionRadius,
            VisionAngleDegrees = changedVision ? rule.Vision?.ArcDegrees ?? 0 : presentation.VisionAngleDegrees,
            BackgroundRole = changedPositions ? null : presentation.BackgroundRole,
            AvailableRole = changedPositions ? null : presentation.AvailableRole
        } };
        return definitions with { Information = definitions.Information with
        {
            Formations = catalogs
        } };
    }

}
