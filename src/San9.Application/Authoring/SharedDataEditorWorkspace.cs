using System.Collections.Immutable;
using System.Text.Json;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Application.Authoring;

/// <summary>An editor visit over shared catalogs and person defaults, published as complete player documents.</summary>
public sealed class SharedDataEditorWorkspace
{
    private readonly ScenarioDefinitions _opened;
    private ScenarioDefinitions _saved;
    private byte[]? _expectedPlayer, _expectedOfficers;
    private readonly HashSet<int> _changedOfficers = [];
    private readonly OfficerDefaultsCatalog _openedOfficers;
    private OfficerDefaultsCatalog _savedOfficers;
    public OfficerDefaultsCatalog OfficerCatalog { get; private set; }
    public string OfficerPath { get; }
    public string PlayerOfficerPath => PlayerDefinitionFiles.PlayerPath(OfficerPath);
    private readonly Dictionary<string, byte[]> _inputs;
    private sealed record ScenarioReference(string Id, string Root, ScenarioContentManifest Manifest);
    private readonly ScenarioReference[] _scenarios;
    public ScenarioDefinitions Definitions { get; private set; }
    public string DefinitionPath { get; }
    public string PlayerDefinitionPath => PlayerDefinitionFiles.PlayerPath(DefinitionPath);
    public bool HasPlayerCopy => _expectedPlayer is not null || _expectedOfficers is not null;
    public string ReferenceScenarioId { get; }
    public IReadOnlyDictionary<int, string> FormationNames => Definitions.Information.Formations.ToDictionary(f => f.Id, f => f.Name);
    public ImmutableArray<FormationRule> Formations => Definitions.Military?.Formations
        .Where(f => Definitions.Information.Formations.Any(c => c.Id == f.Id)).ToImmutableArray() ?? [];
    public IReadOnlyList<OfficeDefinition> Offices => Definitions.Information.Offices;
    public IReadOnlyList<RankDefinition> Ranks => Definitions.Information.Ranks;
    public IReadOnlyList<OfficerDefaults> Officers => OfficerCatalog.Officers;
    public ImmutableArray<TacticRule> Tactics => Definitions.Military?.Tactics
        .Where(t => Definitions.Information.Tactics.Any(c => c.Id == t.Id)).ToImmutableArray() ?? [];
    public int ScenarioCount => _scenarios.Length;
    public bool HasChanges => !ContentComparison.Equal(Definitions, _saved) || _changedOfficers.Count != 0;

    private SharedDataEditorWorkspace(string path, string reference, ScenarioDefinitions definitions,
        string officerPath, OfficerDefaultsCatalog officers, Dictionary<string, byte[]> inputs, byte[]? expectedPlayer,
        byte[]? expectedOfficers, ScenarioReference[] scenarios)
    {
        DefinitionPath = path; ReferenceScenarioId = reference;
        _opened = _saved = Definitions = definitions;
        OfficerPath = officerPath; OfficerCatalog = _openedOfficers = _savedOfficers = officers;
        _expectedOfficers = expectedOfficers;
        _inputs = inputs; _expectedPlayer = expectedPlayer; _scenarios = scenarios;
    }

    public static async Task<SharedDataEditorWorkspace> OpenAsync(string contentRoot, string? referenceScenarioId = null,
        CancellationToken token = default)
    {
        contentRoot = Path.GetFullPath(contentRoot);
        var files = new ContentReadSession();
        var catalog = await files.ReadAsync<ContentCatalog>(Path.Combine(contentRoot, "catalog.json"), token);
        ContentCatalogFiles.Validate(contentRoot, catalog);
        var reference = referenceScenarioId is null ? catalog.Scenarios.FirstOrDefault()
            : catalog.Scenarios.SingleOrDefault(s => s.Id == referenceScenarioId);
        if (reference is null) throw new InvalidDataException("找不到共享数据编辑的参考剧本。");
        var entries = new List<ScenarioReference>();
        foreach (var entry in catalog.Scenarios)
        {
            string package = PackageFiles.Resolve(contentRoot, entry.Package);
            var manifest = await files.ReadAsync<ScenarioContentManifest>(Path.Combine(package, "content.json"), token);
            if (manifest.SchemaVersion != ScenarioContent.CurrentSchemaVersion)
                throw new InvalidDataException("内容包版本不符，请先重建世界内容。");
            entries.Add(new(entry.Id, ScenarioContentFiles.ResolveResourceRoot(package, manifest.ResourceRoot), manifest));
        }
        var selected = entries.Single(e => e.Id == reference.Id);
        string path = PackageFiles.Resolve(selected.Root, selected.Manifest.Definitions);
        string activePath = PlayerDefinitionFiles.ActivePath(path);
        var definitions = await files.ReadAsync<ScenarioDefinitions>(activePath, token);
        var inputs = files.Snapshots();
        byte[]? expectedPlayer = activePath == path ? null : inputs[activePath];
        if (definitions.SchemaVersion != ScenarioDefinitions.CurrentSchemaVersion)
            throw new InvalidDataException("共享定义版本不符，请先重建世界内容。");
        string officerPath = PackageFiles.Resolve(selected.Root, selected.Manifest.OfficerDefaults);
        string activeOfficers = PlayerDefinitionFiles.ActivePath(officerPath);
        var officers = await files.ReadAsync<OfficerDefaultsCatalog>(activeOfficers, token);
        if (officers.SchemaVersion != OfficerDefaultsCatalog.CurrentSchemaVersion)
            throw new InvalidDataException("人物资料版本不符，请重建内容包。");
        byte[]? expectedOfficers = activeOfficers == officerPath ? null : files.Snapshots()[activeOfficers];
        if ((definitions.Military is null || definitions.Military.Formations.IsEmpty && definitions.Military.Tactics.IsEmpty) &&
            definitions.Information.Offices.Length == 0 && definitions.Information.Ranks.Length == 0 && officers.Officers.Length == 0)
            throw new InvalidDataException("该内容库没有当前支持编辑的数据。");
        var scenarios = entries.Where(e => PackageFiles.Resolve(e.Root, e.Manifest.Definitions)
            .Equals(path, StringComparison.OrdinalIgnoreCase) || PackageFiles.Resolve(e.Root, e.Manifest.OfficerDefaults)
            .Equals(officerPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        return new SharedDataEditorWorkspace(path, reference.Id, definitions, officerPath, officers,
            files.Snapshots(), expectedPlayer, expectedOfficers, scenarios);
    }

    public FormationRule GetFormation(int id) => Formations.Single(f => f.Id == id);
    public bool IsFormationChanged(int id) => !ContentComparison.Equal(GetFormation(id), _saved.Military!.Formations.Single(f => f.Id == id)) ||
        !ContentComparison.Equal(GetFormationCatalog(id), _saved.Information.Formations.Single(f => f.Id == id));

    public void SetFormation(FormationRule formation)
    {
        ContentStructure.Validate(formation);
        var formations = Definitions.Military?.Formations ?? [];
        int index = -1;
        for (int i = 0; i < formations.Length; i++) if (formations[i].Id == formation.Id) { index = i; break; }
        if (index < 0) throw new InvalidDataException("只能修改现有阵形，不能改变阵形身份。");
        Definitions = Definitions with { Military = Definitions.Military! with { Formations = formations.SetItem(index, formation) } };
        Definitions = Project(Definitions, _opened);
    }

    public void RestoreFormation(int id)
    {
        SetFormation(_opened.Military!.Formations.Single(f => f.Id == id));
        SetFormationCatalog(_opened.Information.Formations.Single(f => f.Id == id));
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

    public OfficeDefinition GetOffice(int id) => Definitions.Information.Offices.Single(o => o.Id == id);
    public bool IsOfficeChanged(int id) => !ContentComparison.Equal(GetOffice(id), _saved.Information.Offices.Single(o => o.Id == id));

    public void SetOffice(OfficeDefinition office)
    {
        ContentStructure.Validate(office);
        var offices = Definitions.Information.Offices.ToArray();
        int index = Array.FindIndex(offices, o => o.Id == office.Id);
        if (index < 0) throw new InvalidDataException("只能修改现有官职，不能改变官职身份。");
        offices[index] = office;
        Definitions = Definitions with { Information = Definitions.Information with { Offices = offices } };
    }

    public void RestoreOffice(int id) => SetOffice(_opened.Information.Offices.Single(o => o.Id == id));

    public RankDefinition GetRank(int id) => Definitions.Information.Ranks.Single(r => r.Id == id);
    public bool IsRankChanged(int id) => !ContentComparison.Equal(GetRank(id), _saved.Information.Ranks.Single(r => r.Id == id));

    public void SetRank(RankDefinition rank)
    {
        ContentStructure.Validate(rank);
        var ranks = Definitions.Information.Ranks.ToArray();
        int index = Array.FindIndex(ranks, r => r.Id == rank.Id);
        if (index < 0) throw new InvalidDataException("只能修改现有爵位，不能改变爵位身份。");
        ranks[index] = rank;
        Definitions = Definitions with { Information = Definitions.Information with { Ranks = ranks } };
    }

    public void RestoreRank(int id) => SetRank(_opened.Information.Ranks.Single(r => r.Id == id));

    public OfficerDefaults GetOfficer(int id) => Officers.Single(p => p.Id == id);
    public bool IsOfficerChanged(int id) => _changedOfficers.Contains(id);
    public void SetOfficer(OfficerDefaults value)
    {
        ContentStructure.Validate(value);
        var entries = Officers.ToArray();
        int index = Array.FindIndex(entries, p => p.Id == value.Id);
        if (index < 0) throw new InvalidDataException("只能修改现有武将。");
        var original = _openedOfficers.Officers.Single(p => p.Id == value.Id);
        if (value.NameKey != original.NameKey || value.CourtesyNameKey != original.CourtesyNameKey || value.BiographyKey != original.BiographyKey)
            throw new InvalidDataException("请保留人物文本引用身份。");
        entries[index] = value;
        OfficerCatalog = OfficerCatalog with { Officers = entries };
        if (ContentComparison.Equal(value, _savedOfficers.Officers.Single(p => p.Id == value.Id))) _changedOfficers.Remove(value.Id);
        else _changedOfficers.Add(value.Id);
    }
    public void RestoreOfficer(int id) => SetOfficer(_openedOfficers.Officers.Single(p => p.Id == id));
    public TacticRule GetTactic(int id) => Tactics.Single(t => t.Id == id);
    public MilitaryCatalogDefinition GetTacticCatalog(int id) => Definitions.Information.Tactics.Single(t => t.Id == id);
    public bool IsTacticChanged(int id) => !ContentComparison.Equal(GetTactic(id), _saved.Military!.Tactics.Single(t => t.Id == id)) ||
        !ContentComparison.Equal(GetTacticCatalog(id), _saved.Information.Tactics.Single(t => t.Id == id));
    public void SetTactic(TacticRule value)
    {
        ContentStructure.Validate(value);
        var tactics = Definitions.Military?.Tactics ?? [];
        int index = -1;
        for (int i = 0; i < tactics.Length; i++) if (tactics[i].Id == value.Id) { index = i; break; }
        if (index < 0) throw new InvalidDataException("只能修改现有兵法。");
        if (value.Effect != _opened.Military!.Tactics.Single(t => t.Id == value.Id).Effect)
            throw new InvalidDataException("本页保留兵法效果类型。");
        Definitions = Definitions with { Military = Definitions.Military! with { Tactics = tactics.SetItem(index, value) } };
    }
    public void SetTacticCatalog(MilitaryCatalogDefinition value)
    {
        var entries = Definitions.Information.Tactics.ToArray();
        int index = Array.FindIndex(entries, t => t.Id == value.Id);
        if (index < 0) throw new InvalidDataException("找不到兵法。");
        entries[index] = value;
        Definitions = Definitions with { Information = Definitions.Information with { Tactics = entries } };
    }
    public void RestoreTactic(int id)
    {
        SetTactic(_opened.Military!.Tactics.Single(t => t.Id == id));
        SetTacticCatalog(_opened.Information.Tactics.Single(t => t.Id == id));
    }

    private async Task<ContentReadSession> ValidateForSaveAsync(CancellationToken token)
    {
        ContentStructure.Validate(Definitions);
        foreach (var formation in Formations)
            if ((!formation.Positions.IsEmpty || formation.Attack is not null) && formation.Positions.Length != formation.OfficerCapacity)
                throw new InvalidDataException($"{FormationNames.GetValueOrDefault(formation.Id, formation.Id.ToString())}：武将上限 {formation.OfficerCapacity} 与阵位数 {formation.Positions.Length} 不一致，请在阵位页调整。");
        // Save owns a fresh read session. Shared geography and person defaults are
        // parsed once for this operation, even when many scenarios reference them.
        var files = new ContentReadSession();
        foreach (var entry in _scenarios)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var document = await files.ReadAsync<ScenarioDocument>(PackageFiles.Resolve(entry.Root, entry.Manifest.Scenario), token);
                string officersPath = PackageFiles.Resolve(entry.Root, entry.Manifest.OfficerDefaults);
                var officers = officersPath.Equals(OfficerPath, StringComparison.OrdinalIgnoreCase) ? OfficerCatalog
                    : await files.ReadAsync<OfficerDefaultsCatalog>(PlayerDefinitionFiles.ActivePath(officersPath), token);
                string definitionsPath = PackageFiles.Resolve(entry.Root, entry.Manifest.Definitions);
                var definitions = definitionsPath.Equals(DefinitionPath, StringComparison.OrdinalIgnoreCase) ? Definitions
                    : await files.ReadAsync<ScenarioDefinitions>(PlayerDefinitionFiles.ActivePath(definitionsPath), token);
                var geography = await files.ReadAsync<ScenarioGeography>(PackageFiles.Resolve(entry.Root, entry.Manifest.Geography), token);
                if (officers.SchemaVersion != OfficerDefaultsCatalog.CurrentSchemaVersion)
                    throw new InvalidDataException("人物默认资料版本不符。");
                var scenario = document.Compile(definitions, officers.Officers, geography);
                scenario.Validate();
                var messages = await ReadMessagesAsync(entry, files, token);
                MessageGraph.ValidateCatalogReferences(messages, scenario.Information);
                foreach (var person in OfficerComposition.Profiles(document.Officers, [.. officers.Officers, .. document.AdditionalOfficerDefaults]))
                    if (person.Biography is { } biography) MessageStyles.ValidateMarkup(biography);
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException)
            { throw new InvalidDataException($"剧本 {entry.Id} 的共享数据校验失败：{error.Message}", error); }
        }
        return files;
    }

    private static async Task<Dictionary<string, MessageDefinition>> ReadMessagesAsync(ScenarioReference entry,
        ContentReadSession files, CancellationToken token)
    {
        Dictionary<string, MessageDefinition> shared = new(StringComparer.Ordinal), own = new(StringComparer.Ordinal);
        if (entry.Manifest.SharedMessages is { } path)
        {
            string full = PackageFiles.Resolve(entry.Root, path);
            var library = await files.ReadAsync<MessageLibraryManifest>(full, token);
            if (library.SchemaVersion != MessageLibraryManifest.CurrentSchemaVersion) throw new InvalidDataException("消息库版本不符。");
            foreach (string relative in library.Files)
            foreach (var (key, value) in await files.ReadAsync<Dictionary<string, MessageDefinition>>(PackageFiles.Resolve(Path.GetDirectoryName(full)!, relative), token))
                if (!shared.TryAdd(key, value)) throw new InvalidDataException($"重复消息：{key}");
        }
        foreach (string relative in entry.Manifest.Messages)
        foreach (var (key, value) in await files.ReadAsync<Dictionary<string, MessageDefinition>>(PackageFiles.Resolve(entry.Root, relative), token))
            if (!own.TryAdd(key, value)) throw new InvalidDataException($"重复消息：{key}");
        return ScenarioContentFiles.ComposeMessages(shared, own, entry.Manifest.MessageOverrides);
    }

    public async Task SaveAsync(CancellationToken token = default)
    {
        if (!HasChanges) return;
        await EnsureUnchangedAsync(_inputs, token);
        var validation = await ValidateForSaveAsync(token);
        // Worlds are read at save time; check that both the editor source and that
        // validation snapshot are still current before replacing the player copy.
        await EnsureUnchangedAsync(_inputs, token);
        await EnsureUnchangedAsync(validation.Snapshots(), token);
        var published = await ScenarioDataFiles.WritePlayerDocumentsAsync([
            new(PlayerDefinitionPath, Definitions, _expectedPlayer), new(PlayerOfficerPath, OfficerCatalog, _expectedOfficers)], token);
        _expectedPlayer = published[PlayerDefinitionPath]; _expectedOfficers = published[PlayerOfficerPath];
        _inputs.Remove(DefinitionPath); _inputs.Remove(OfficerPath);
        _inputs[PlayerDefinitionPath] = _expectedPlayer; _inputs[PlayerOfficerPath] = _expectedOfficers;
        _saved = Definitions; _savedOfficers = OfficerCatalog; _changedOfficers.Clear();
    }

    private static async Task EnsureUnchangedAsync(IReadOnlyDictionary<string, byte[]> inputs, CancellationToken token)
    {
        foreach (var (path, expected) in inputs)
            if (!(await File.ReadAllBytesAsync(path, token)).AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("内容文件已被其他操作修改，请重新打开编辑器或重试保存。当前草稿已保留。");
    }

    /// <summary>Operation-local reuse only. Nothing survives into a later save.</summary>
    private sealed class ContentReadSession
    {
        private sealed class Entry(byte[] bytes)
        {
            public byte[] Bytes { get; } = bytes;
            public Dictionary<Type, object> Values { get; } = [];
        }
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

        public async Task<T> ReadAsync<T>(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            path = Path.GetFullPath(path);
            if (!_entries.TryGetValue(path, out var entry))
            {
                entry = new(await File.ReadAllBytesAsync(path, token));
                _entries.Add(path, entry);
            }
            if (entry.Values.TryGetValue(typeof(T), out var parsed)) return (T)parsed;
            using var stream = new MemoryStream(entry.Bytes, writable: false);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, ScenarioContentFiles.DataJsonOptions, token)
                ?? throw new InvalidDataException($"内容文件为空：{path}");
            ContentStructure.Validate(value);
            entry.Values.Add(typeof(T), value);
            return value;
        }

        public Dictionary<string, byte[]> Snapshots() => _entries.ToDictionary(e => e.Key, e => e.Value.Bytes, StringComparer.OrdinalIgnoreCase);
    }

    private static ScenarioDefinitions Project(ScenarioDefinitions definitions, ScenarioDefinitions opened)
    {
        var rules = definitions.Military!.Formations.ToDictionary(f => f.Id);
        var originals = opened.Military!.Formations.ToDictionary(f => f.Id);
        var presentations = opened.Information.Formations.ToDictionary(f => f.Id, f => f.Presentation);
        return definitions with { Information = definitions.Information with
        {
            Formations = definitions.Information.Formations.Select(catalog =>
            {
                if (presentations[catalog.Id] is not { } presentation || !rules.TryGetValue(catalog.Id, out var rule)) return catalog;
                bool changedPositions = !rule.Positions.SequenceEqual(originals[rule.Id].Positions);
                return catalog with { Presentation = presentation with
                {
                    Columns = changedPositions ? Math.Max(1, rule.Positions.Select(p => p.Column + 1).DefaultIfEmpty(1).Max()) : presentation.Columns,
                    Slots = rule.Positions.Select(p => new FormationSlot(p.Column, p.Row)).ToImmutableArray(),
                    VisionRadius = rule.Vision is { } vision ? (int)Math.Min(int.MaxValue, ((long)vision.Radius + MapCoordinates.UnitsPerCell - 1) / MapCoordinates.UnitsPerCell) : 0,
                    VisionAngleDegrees = rule.Vision?.ArcDegrees ?? 0,
                    BackgroundRole = changedPositions ? null : presentation.BackgroundRole,
                    AvailableRole = changedPositions ? null : presentation.AvailableRole
                } };
            }).ToArray()
        } };
    }

}
