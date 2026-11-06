using System.Text.Json;
using Godot;
using San9.Content.Packages;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Formation subpage. The parent owns loading, save, close and the shared draft.</summary>
public partial class FormationEditorPage : SharedDataEditorPage
{
    private readonly List<int> _visible = [];
    private ItemList _list = null!;
    private LineEdit _search = null!;
    private TabContainer _tabs = null!;
    private Label _title = null!;
    private HSplitContainer _split = null!;
    private FormationEditorPreview _preview = null!;
    private TextEdit? _json;
    private int? _current;
    private int _generation;
    private bool _building, _rawDirty;
    public override void Activate() { if (IsNodeReady() && !HasPendingChanges) RefreshAfterSave(); }
    public override bool HasPendingChanges => _rawDirty;
    public override bool CommitPending() => CommitJson();
    public override void RefreshAfterSave() { RefreshList(); ShowCurrent(); }
    private static readonly JsonSerializerOptions EditorJson = new(ScenarioContentFiles.DataJsonOptions)
    { WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };

    public override void _Ready()
    {
        _split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [222] }; AddChild(_split); _split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var sidePanel = new PanelContainer { CustomMinimumSize = new(200, 0) };
        sidePanel.AddThemeStyleboxOverride("panel", ContentEditorStyle.Surface("202833", "394758", 10)); _split.AddChild(sidePanel);
        var sidebar = new VBoxContainer(); sidePanel.AddChild(sidebar);
        _search = new LineEdit { PlaceholderText = "搜索阵形", Name = "FormationSearch" }; sidebar.AddChild(_search);
        _search.TextChanged += _ => RefreshList();
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, Name = "FormationList" }; sidebar.AddChild(_list);
        _list.ItemSelected += index => Select(_visible[(int)index]);
        Button(sidebar, "恢复打开时的值", () =>
        {
            if (_current is not int id || !CanProcess()) return;
            _rawDirty = false; Workspace.RestoreFormation(id); ShowCurrent(); RefreshList(); Status("已恢复，尚未保存。");
        }, "RestoreFormation");
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(550, 0) }; _split.AddChild(body);
        var hero = new HBoxContainer(); body.AddChild(hero);
        _title = new Label { Text = "选择一个阵形", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _title.AddThemeFontSizeOverride("font_size", 23); hero.AddChild(_title);
        _preview = new FormationEditorPreview { CustomMinimumSize = new(190, 100), MouseFilter = MouseFilterEnum.Ignore }; hero.AddChild(_preview);
        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Name = "FormationPages" }; body.AddChild(_tabs);
        _tabs.TabChanged += index =>
        {
            if (_building || !_rawDirty || index == 3) return;
            if (!CommitJson()) { _building = true; _tabs.CurrentTab = 3; _building = false; }
            else ShowCurrent();
        };
        _current = Workspace.Formations.FirstOrDefault()?.Id;
        if (_current is null) _title.Text = "该内容库没有阵形";
        RefreshList(); ShowCurrent();
    }

    private void RefreshList()
    {
        if (_building) return;
        _building = true; _list.Clear(); _visible.Clear();
        foreach (var formation in Workspace.Formations)
        {
            string name = Workspace.FormationNames.GetValueOrDefault(formation.Id, $"阵形 {formation.Id}");
            if (_search.Text.Length > 0 && !name.Contains(_search.Text, StringComparison.OrdinalIgnoreCase) && formation.Id.ToString() != _search.Text) continue;
            _visible.Add(formation.Id); _list.AddItem(name + (Workspace.IsFormationChanged(formation.Id) ? "  *" : ""));
            _list.SetItemTooltip(_visible.Count - 1, $"{name} · ID {formation.Id}");
            if (_current == formation.Id) _list.Select(_visible.Count - 1);
        }
        _building = false;
    }

    private void Select(int id)
    {
        if (_building || !CanProcess() || id == _current) return;
        if (!CommitJson()) { RefreshList(); return; }
        _current = id; ShowCurrent();
    }

    private void ShowCurrent()
    {
        if (_current is not int id) return;
        int tab = Math.Max(0, _tabs.CurrentTab);
        _building = true; _generation++; _json = null;
        foreach (Node child in _tabs.GetChildren()) { _tabs.RemoveChild(child); child.QueueFree(); }
        var rule = Workspace.GetFormation(id);
        BuildBasics(rule); BuildCombat(rule); BuildTactics(rule); BuildAdvanced(rule);
        _tabs.CurrentTab = Math.Min(tab, _tabs.GetTabCount() - 1); _building = false; UpdateHeading();
    }

    private void UpdateHeading()
    {
        if (_current is not int id) return;
        var rule = Workspace.GetFormation(id);
        _title.Text = Workspace.FormationNames.GetValueOrDefault(id, $"阵形 {id}") + (Workspace.IsFormationChanged(id) || _rawDirty ? " *" : "");
        _preview.Rule = rule;
    }

    private void Edit(int id, Func<FormationRule, FormationRule> change, bool rebuild = false)
    {
        if (_building || !CanProcess() || _current != id) return;
        Workspace.SetFormation(change(Workspace.GetFormation(id))); RefreshList(); UpdateHeading();
        if (rebuild) ShowCurrent();
        else if (_json is not null)
        {
            _building = true; _json.Text = JsonSerializer.Serialize(Workspace.GetFormation(id), EditorJson); _building = false;
        }
    }

    private bool CommitJson()
    {
        if (!_rawDirty || _json is null || _current is not int id) return true;
        try
        {
            var rule = JsonSerializer.Deserialize<FormationRule>(_json.Text, EditorJson) ?? throw new InvalidDataException("阵形数据为空。");
            if (rule.Id != id) throw new InvalidDataException("请保留当前阵形的身份 ID。");
            Workspace.SetFormation(rule); _rawDirty = false; RefreshList(); UpdateHeading(); return true;
        }
        catch (Exception error) { Status("完整数据尚未应用：" + error.Message, true); return false; }
    }

    private Action<T> Guard<T>(Action<T> action)
    {
        int generation = _generation;
        return value => { if (!_building && CanProcess() && generation == _generation) action(value); };
    }

    private static Button Button(Node parent, string title, Action action, string name = "")
    {
        var button = new Button { Text = title, CustomMinimumSize = new(0, 34) };
        if (name.Length > 0) button.Name = name;
        button.Pressed += action; parent.AddChild(button); return button;
    }

}
