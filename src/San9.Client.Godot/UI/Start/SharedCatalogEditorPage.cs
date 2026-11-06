using Godot;

namespace San9.Client.Godot;

/// <summary>Search, selection and guarded fields for shared definition pages.</summary>
public abstract partial class SharedCatalogEditorPage : SharedDataEditorPage
{
    protected VBoxContainer Form = null!;
    protected int? Current;
    private ItemList _list = null!;
    private LineEdit _search = null!;
    private Label _title = null!;
    private readonly List<int> _visible = [];
    private bool _building;
    private int _generation;
    protected abstract string Subject { get; }
    protected abstract IEnumerable<(int Id, string Name)> Entries { get; }
    protected abstract bool Changed(int id);
    protected abstract void Restore(int id);
    protected abstract void BuildForm(int id);
    public override bool HasPendingChanges => false;
    public override bool CommitPending() => true;
    public override void RefreshAfterSave() { RefreshList(); ShowCurrent(); }
    public override void Activate() { if (IsNodeReady()) { RefreshList(); ShowCurrent(); } }

    public override void _Ready()
    {
        var split = new HSplitContainer { SplitOffsets = [235] }; AddChild(split);
        split.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var side = new VBoxContainer { CustomMinimumSize = new(215, 0) }; split.AddChild(side);
        _search = new LineEdit { PlaceholderText = "搜索" + Subject, Name = GetType().Name.Replace("EditorPage", "", StringComparison.Ordinal) + "Search" }; side.AddChild(_search);
        _search.TextChanged += _ => RefreshList();
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, Name = GetType().Name.Replace("EditorPage", "", StringComparison.Ordinal) + "List" }; side.AddChild(_list);
        _list.ItemSelected += index => { if (!_building && CanProcess()) { Current = _visible[(int)index]; ShowCurrent(); } };
        var restore = new Button { Text = "恢复打开时的值", CustomMinimumSize = new(0, 34) }; side.AddChild(restore);
        restore.Pressed += () => { if (Current is int id && CanProcess()) { Restore(id); RefreshAfterSave(); Status("已恢复，尚未保存。"); } };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(550, 0) }; split.AddChild(body);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 23); body.AddChild(_title);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
        Form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; Form.AddThemeConstantOverride("separation", 12); scroll.AddChild(Form);
        Current = Entries.Select(e => (int?)e.Id).FirstOrDefault(); RefreshList(); ShowCurrent();
    }
    private void RefreshList()
    {
        _building = true; _list.Clear(); _visible.Clear();
        foreach (var (id, name) in Entries)
        {
            if (_search.Text.Length > 0 && !name.Contains(_search.Text, StringComparison.OrdinalIgnoreCase) && id.ToString() != _search.Text) continue;
            _visible.Add(id); _list.AddItem(name + (Changed(id) ? "  *" : ""));
            if (Current == id) _list.Select(_visible.Count - 1);
        }
        _building = false;
        UpdateHeading();
    }
    private void UpdateHeading()
    {
        _title.Text = Current is int id ? Entries.First(e => e.Id == id).Name + (Changed(id) ? " *" : "") : "没有可编辑的" + Subject;
    }
    private void ShowCurrent()
    {
        _building = true; _generation++;
        foreach (Node child in Form.GetChildren()) { Form.RemoveChild(child); child.QueueFree(); }
        if (Current is int id) BuildForm(id);
        _building = false; UpdateHeading();
    }
    protected Action<T> Guard<T>(Action<T> action)
    {
        int generation = _generation;
        return value => { if (!_building && CanProcess() && generation == _generation) { action(value); RefreshCurrent(); } };
    }
    private void RefreshCurrent()
    {
        if (_search.Text.Length > 0) { RefreshList(); return; }
        if (Current is int id)
        {
            int row = _visible.IndexOf(id);
            if (row >= 0) _list.SetItemText(row, Entries.First(e => e.Id == id).Name + (Changed(id) ? "  *" : ""));
        }
        UpdateHeading();
    }
    protected GridContainer Section(string title)
    {
        var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", CreatedOfficerEditorStyle.Accent); Form.AddChild(label);
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 9); Form.AddChild(grid); return grid;
    }
    protected void TextField(Node parent, string title, string value, Action<string> changed, string name) =>
        SharedEditorText.Add(parent, title, value, Guard(changed), name: name, inline: true);

    protected void TextField(string title, string value, Action<string> changed, bool multiline = false, string name = "") =>
        SharedEditorText.Add(Form, title, value, Guard(changed), multiline, name);

    protected void Number(Node parent, string title, double value, double min, double max, Action<double> changed, string name = "", string tooltip = "")
    {
        parent.AddChild(new Label { Text = title });
        var field = new SpinBox { Name = name, TooltipText = tooltip, MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true, UpdateOnTextChanged = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(110, 34) };
        var apply = Guard(changed); field.ValueChanged += v => apply(v); parent.AddChild(field);
    }
    protected void Toggle(Node parent, string title, bool value, Action<bool> changed, string name = "", string tooltip = "")
    {
        var field = new CheckBox { Name = name, TooltipText = tooltip, Text = title, ButtonPressed = value }; var apply = Guard(changed);
        field.Toggled += v => apply(v); parent.AddChild(field);
    }
    protected void Choice(Node parent, string title, IReadOnlyList<string> labels, int selected, Action<int> changed)
    {
        parent.AddChild(new Label { Text = title }); var field = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (string label in labels) field.AddItem(label); field.Select(selected);
        var apply = Guard(changed); field.ItemSelected += v => apply((int)v); parent.AddChild(field);
    }
    protected void Rebuild() => ShowCurrent();
}
