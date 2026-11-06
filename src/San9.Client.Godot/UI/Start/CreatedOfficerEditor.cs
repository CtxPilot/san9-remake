using Godot;
using San9.Application.Authoring;
using San9.Client.Godot.Persistence;
using San9.Content.Authoring;
using San9.Content.Packages;
using San9.Content.Scenarios;
using Timer = global::Godot.Timer;

namespace San9.Client.Godot;

/// <summary>The workspace owns drafts; portraits use the game's shared texture service.</summary>
public partial class CreatedOfficerEditor : ModalView
{
    public CreatedOfficerStore Store { get; init; } = null!;
    public string ContentRoot { get; init; } = "";
    public TextureService? Service { get; init; }
    public Settings.UiPreferencesSession Preferences { get; init; } = new();
    public Action Closed { get; init; } = null!;
    private CreatedOfficerWorkspace? _workspace;
    private CreatedOfficerWorkspace Workspace => _workspace ?? throw new InvalidOperationException("人物库尚未载入。");
    private Guid? _current;
    private ItemList _list = null!;
    private LineEdit _search = null!;
    private TabContainer _tabs = null!;
    private Label _status = null!, _count = null!, _title = null!;
    private Timer _statusTimer = null!;
    private readonly List<Guid> _visible = [];
    private ScenarioDefinitions? _definitions;
    private OfficerDefaults[] _historicalOfficers = [];
    private readonly CancellationTokenSource _lifetime = new();
    private bool _refreshing;
    private TextureService? _portraitService;
    private bool _previousAutoAcceptQuit;
    public CreatedOfficerLibrary Library => Workspace.Library;
    public bool HasChanges => _workspace?.HasChanges ?? false;
    private CreatedOfficer? Current => _current is Guid id ? Library.Officers.SingleOrDefault(p => p.Id == id) : null;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = ContentEditorStyle.Create(GetThemeFont("font"));
        _portraitService = Service;
        _previousAutoAcceptQuit = GetTree().AutoAcceptQuit;
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestClose;
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("1b222c") });
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(panel);
        var margin = new MarginContainer();
        foreach (string edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + edge, 16);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); margin.AddChild(column);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); column.AddChild(header);
        _title = new Label { Text = "新武将编辑器", Name = "EditorTitle", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _title.AddThemeFontSizeOverride("font_size", 20); header.AddChild(_title);
        var options = new MenuButton { Text = "编辑选项", Name = "EditorOptions" };
        var optionsPopup = options.GetPopup();
        optionsPopup.AddCheckItem("自动旗字", 0);
        optionsPopup.SetItemChecked(0, Preferences.Current.AutoAssignOfficerFlag);
        optionsPopup.IdPressed += id =>
        {
            if (id != 0) return;
            FinishSurnameEdit();
            Preferences.Update(p => p with { AutoAssignOfficerFlag = !p.AutoAssignOfficerFlag });
            optionsPopup.SetItemChecked(0, Preferences.Current.AutoAssignOfficerFlag);
            if (_workspace is not null) ShowCurrent();
        };
        header.AddChild(options);
        var save = AddButton(header, "保存人物库", () => Save(), "SaveOfficers");
        save.TooltipText = "Ctrl+S"; CompanionPanelStyle.Primary(save);
        AddButton(header, "关闭", RequestClose, "CloseEditor");
        var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [230] }; column.AddChild(split);
        var sidebar = ContentEditorControls.Sidebar(split);
        var listHeading = new HBoxContainer(); sidebar.AddChild(listHeading);
        listHeading.AddChild(new Label { Text = "人物库" });
        _count = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right };
        _count.AddThemeColorOverride("font_color", ContentEditorStyle.Muted); listHeading.AddChild(_count);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 6); sidebar.AddChild(actions);
        AddButton(actions, "新建", () => { FinishSurnameEdit(); _current = Workspace.Add().Id; Changed(true); }, "NewOfficer").SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddButton(actions, "复制", () => { FinishSurnameEdit(); if (_current is Guid id) { _current = Workspace.Duplicate(id).Id; Changed(true); } }, "CopyOfficer").SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddButton(actions, "删除", DeleteCurrent, "DeleteOfficer").SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _search = new LineEdit { PlaceholderText = "搜索姓名、字", Name = "OfficerSearch" }; sidebar.AddChild(_search);
        _search.TextChanged += _ => RefreshList();
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, Name = "CreatedOfficerList" }; sidebar.AddChild(_list);
        _list.ItemSelected += index => { if (!_refreshing) { _current = _visible[(int)index]; ShowCurrent(); } };
        var files = new HBoxContainer(); files.AddThemeConstantOverride("separation", 8); sidebar.AddChild(files);
        AddButton(files, "导入", Import, "ImportOfficers").SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddButton(files, "导出", Export, "ExportOfficers").SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(600, 0), Name = "OfficerPages" }; split.AddChild(_tabs);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
            TooltipText = Store.Path, Name = "EditorStatus", Visible = false }; column.AddChild(_status);
        _statusTimer = new Timer { OneShot = true, WaitTime = 4 }; AddChild(_statusTimer);
        _statusTimer.Timeout += () => _status.Hide();
        try { _workspace = new(Store.Load()); }
        catch (Exception error) when (IsFileError(error))
        {
            save.Disabled = true; split.Hide();
            Status("人物库读取失败，未覆盖原文件：" + error.Message, true);
            return;
        }
        _current = Library.Officers.FirstOrDefault()?.Id;
        RefreshList(); ShowCurrent();
        LoadCatalog();
    }

    private static bool IsFileError(Exception error) => error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
        or FormatException or InvalidDataException or ArgumentException or OverflowException;

    private async void LoadCatalog()
    {
        var token = _lifetime.Token;
        try
        {
            var catalog = await ContentCatalogFiles.LoadAsync(ContentRoot, token);
            try
            {
                if (_portraitService is null)
                {
                    string textureRoot = PackageFiles.Resolve(ContentRoot, catalog.Textures);
                    var textures = await Task.Run(() => TexturePackageFiles.LoadAsync(textureRoot, token), token);
                    token.ThrowIfCancellationRequested();
                    var service = new TextureService(); service.Initialize(textures, textureRoot); AddChild(service);
                    _portraitService = service; ShowCurrent();
                }
            }
            catch (Exception error) when (IsFileError(error))
            { Status("头像资源读取失败，仍可编辑人物资料：" + error.Message, true); }
            try
            {
                var flags = await ContentCatalogFiles.LoadOfficerFlagsAsync(ContentRoot, catalog, token);
                token.ThrowIfCancellationRequested();
                if (_portraitService is not null) flags.ValidateReferences(_portraitService.AnimationClips.Keys.ToHashSet());
                Workspace.UseFlagCatalog(flags); ShowCurrent();
            }
            catch (Exception error) when (IsFileError(error))
            { Status("旗字目录读取失败，选旗和自动关联不可用：" + error.Message, true); }
            var authoring = await ContentCatalogFiles.LoadAuthoringAsync(ContentRoot, catalog, token);
            token.ThrowIfCancellationRequested();
            _definitions = authoring.Definitions;
            _historicalOfficers = authoring.Officers.Officers;
            ShowCurrent();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) when (IsFileError(error))
        { Status("共享内容读取失败，仍可编辑人物资料：" + error.Message, true); }
    }
    private void RefreshList()
    {
        _refreshing = true; _visible.Clear(); _list.Clear();
        foreach (var person in Library.Officers.Where(p => string.IsNullOrWhiteSpace(_search.Text) ||
            p.FullName.Contains(_search.Text, StringComparison.OrdinalIgnoreCase) || p.CourtesyName.Contains(_search.Text, StringComparison.OrdinalIgnoreCase)))
        {
            _visible.Add(person.Id); _list.AddItem(string.IsNullOrWhiteSpace(person.FullName) ? "（未命名）" : person.FullName);
            if (person.Id == _current) _list.Select(_visible.Count - 1);
        }
        _count.Text = string.IsNullOrWhiteSpace(_search.Text) ? $"{Library.Officers.Length} 位" : $"{_visible.Count} / {Library.Officers.Length} 位";
        _title.Text = "新武将编辑器" + (HasChanges ? " · 未保存" : "");
        _refreshing = false;
    }
    private void Changed(bool rebuild = false) { RefreshList(); if (rebuild) ShowCurrent(); else RefreshFlagValue(); }
    private void Change(Guid officerId, Func<CreatedOfficer, CreatedOfficer> edit, bool rebuild = false)
    {
        var person = Library.Officers.SingleOrDefault(p => p.Id == officerId);
        if (person is null) return;
        Workspace.Set(edit(person)); Changed(rebuild);
    }
    private void ShowCurrent()
    {
        FinishSurnameEdit();
        int page = _tabs.CurrentTab;
        foreach (var child in _tabs.GetChildren()) { _tabs.RemoveChild(child); child.QueueFree(); }
        if (Current is not { } person)
        { var empty = new VBoxContainer { Name = "人物资料" }; _tabs.AddChild(empty);
            empty.AddChild(new Label { Text = "点击“新建”开始创作，或导入已有的人物库。" }); return; }
        BuildPages(person);
        _tabs.CurrentTab = Math.Clamp(page, 0, _tabs.GetTabCount() - 1);
    }
    private bool Save()
    {
        if (_workspace is null) return false;
        try
        {
            FinishSurnameEdit();
            Store.Save(Library); _workspace.Saved(); RefreshList(); Status("人物库已保存。"); return true;
        }
        catch (Exception error) when (IsFileError(error)) { Status("未保存：" + error.Message, true); return false; }
    }
    private void DeleteCurrent()
    {
        if (Current is not { } person) return;
        var dialog = new ConfirmationDialog { Title = "删除人物", DialogText = $"删除“{person.FullName}”？保存人物库后生效。", OkButtonText = "删除", CancelButtonText = "取消" };
        dialog.Confirmed += () =>
        {
            try { Workspace.Delete(person.Id); _current = Library.Officers.FirstOrDefault()?.Id; Changed(true); }
            catch (InvalidDataException error) { Status(error.Message, true); }
        };
        Popup(dialog);
    }
    private void Import() => ChooseLibraryFile(false, path =>
    {
        var incoming = CreatedOfficerStore.Read(path);
        int collisions = incoming.Officers.Count(p => Library.Officers.Any(o => o.Id == p.Id));
        var dialog = new ConfirmationDialog { Title = "导入人物库", DialogText = $"导入 {incoming.Officers.Length} 位人物。\n其中 {collisions} 位身份已存在。更新会保留身份并替换其资料；另建副本会生成新身份。",
            OkButtonText = "更新相同身份", CancelButtonText = "取消" };
        dialog.AddButton("全部另建副本", false, "copies");
        void Merge(bool copies)
        {
            try { Workspace.Import(incoming, copies); _current ??= Library.Officers.FirstOrDefault()?.Id; Changed(true); Status("已导入草稿，请检查并保存。"); }
            catch (Exception error) when (IsFileError(error)) { Status("导入失败：" + error.Message, true); }
        }
        dialog.Confirmed += () => Merge(false);
        dialog.CustomAction += action => { if (action == "copies") { Merge(true); dialog.QueueFree(); } };
        Popup(dialog);
    });
    private void Export() => ChooseLibraryFile(true, path =>
    { FinishSurnameEdit(); CreatedOfficerStore.Export(path, Library); Status("已导出人物资料与头像引用。导出不替代保存本地人物库。"); });
    private void ChooseLibraryFile(bool save, Action<string> selected)
    {
        var dialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem,
            FileMode = save ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile, Filters = ["*.json ; 人物库"],
            Title = save ? "导出人物库" : "导入人物库", CurrentFile = save ? "created-officers.json" : "" };
        dialog.FileSelected += path =>
        {
            try { selected(path); }
            catch (Exception error) when (IsFileError(error)) { Status("操作失败：" + error.Message, true); }
        };
        dialog.Canceled += () => dialog.QueueFree(); dialog.FileSelected += _ => dialog.QueueFree();
        AddChild(dialog); dialog.PopupCentered(new(850, 570));
    }
    private void Popup(ConfirmationDialog dialog)
    {
        dialog.Confirmed += () => dialog.QueueFree(); dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); dialog.PopupCentered(new(590, 190));
    }
    private void RequestClose()
    {
        if (!HasChanges) { Closed(); return; }
        var dialog = new ConfirmationDialog { Title = "未保存的修改", DialogText = "人物库有未保存修改。", OkButtonText = "保存并关闭", CancelButtonText = "继续编辑" };
        dialog.AddButton("放弃修改并关闭", false, "discard");
        dialog.Confirmed += () => { if (Save()) Closed(); };
        dialog.CustomAction += action => { if (action == "discard") { dialog.QueueFree(); Closed(); } };
        Popup(dialog);
    }
    private void Status(string message, bool error = false)
    {
        _statusTimer.Stop(); _status.Text = message; _status.Visible = !string.IsNullOrWhiteSpace(message);
        _status.Modulate = error ? new Color("ffb4a8") : ContentEditorStyle.Muted;
        if (_status.Visible && !error) _statusTimer.Start();
    }
    private static Button AddButton(Node parent, string text, Action pressed, string name = "") =>
        ContentEditorControls.ActionButton(parent, text, pressed, name);
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.S && key.CtrlPressed) { GetViewport().SetInputAsHandled(); Save(); }
        else if (key.Keycode == Key.Escape) { GetViewport().SetInputAsHandled(); RequestClose(); }
    }
    public override void _ExitTree()
    {
        GetWindow().CloseRequested -= RequestClose;
        GetTree().AutoAcceptQuit = _previousAutoAcceptQuit;
        _lifetime.Cancel(); _lifetime.Dispose();
    }
}
