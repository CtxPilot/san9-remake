using Godot;
using San9.Application.Authoring;
using San9.Content.Images;
using San9.Content.Packages;

namespace San9.Client.Godot;

public partial class PortraitEditor
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop;
        Theme = ContentEditorStyle.Create(GetThemeFont("font"));
        _previousAutoQuit = GetTree().AutoAcceptQuit; GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += RequestClose;
        var panel = new PanelContainer(); panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(panel);
        panel.AddThemeStyleboxOverride("panel", ContentEditorStyle.Surface("1b222c", "394758", 12));
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); panel.AddChild(column);
        var header = new HBoxContainer(); column.AddChild(header);
        header.AddChild(new Label { Text = "头像编辑器", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _import = Button(header, "导入图片", Import);
        _save = Button(header, "保存", () => Save()); _save.TooltipText = "Ctrl+S";
        CompanionPanelStyle.Primary(_save);
        Button(header, "关闭", RequestClose);
        var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [244] }; column.AddChild(split);
        var sidebar = ContentEditorControls.Sidebar(split); sidebar.CustomMinimumSize = new(220, 0);
        _search = new LineEdit { PlaceholderText = "搜索图片名或使用者" }; sidebar.AddChild(_search);
        _modified = new CheckBox { Text = "仅看未保存修改" }; sidebar.AddChild(_modified);
        _list = new Tree { SizeFlagsVertical = SizeFlags.ExpandFill, Columns = 2,
            HideRoot = true, ColumnTitlesVisible = true, SelectMode = Tree.SelectModeEnum.Row };
        _list.SetColumnTitle(0, "图片名"); _list.SetColumnTitle(1, "使用者");
        _list.SetColumnTitleAlignment(0, HorizontalAlignment.Left); _list.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _list.SetColumnCustomMinimumWidth(0, 80); _list.SetColumnCustomMinimumWidth(1, 100);
        sidebar.AddChild(_list);
        _list.ItemSelected += () =>
        {
            if (!_refreshing && _list.GetSelected() is { } item) Select(item.GetMetadata(0).AsString());
        };
        _search.TextChanged += _ => RefreshList(); _modified.Toggled += _ => RefreshList();
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; split.AddChild(body);
        var identityRow = new HBoxContainer(); identityRow.AddThemeConstantOverride("separation", 10); body.AddChild(identityRow);
        _name = new LineEdit { PlaceholderText = "头像名称", Editable = false, Name = "PortraitName",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new(140, 34), TooltipText = "直接编辑自建头像名称" };
        identityRow.AddChild(_name);
        _name.TextChanged += _ => { if (!_refreshing) { _nameDirty = true; CancelAnalysis(); } };
        _name.TextSubmitted += _ => FinishName(); _name.FocusExited += FinishName;
        _identity = new Label { Text = "正在读取…", MouseFilter = MouseFilterEnum.Ignore };
        _identity.AddThemeColorOverride("font_color", ContentEditorStyle.Muted); identityRow.AddChild(_identity);
        _undo = Button(identityRow, "撤销", () => { FinishName(); _workspace?.Undo(); RefreshSelection(); });
        _redo = Button(identityRow, "重做", () => { FinishName(); _workspace?.Redo(); RefreshSelection(); });
        _more = new MenuButton { Text = "更多", CustomMinimumSize = new(0, 34) }; identityRow.AddChild(_more);
        var menu = _more.GetPopup();
        menu.AddItem("恢复打开时", 0); menu.AddItem("恢复基础取景", 1); menu.AddItem("由面部推算半身", 2);
        menu.AddSeparator(); menu.AddItem("替换图片", 4); menu.AddItem("恢复原版", 5); menu.AddItem("删除头像", 6);
        menu.AddSeparator(); menu.AddItem("操作说明", 3);
        menu.IdPressed += id =>
        {
            if (id == 0) Restore(false); else if (id == 1) Restore(true);
            else if (id == 2) EstimateBust(); else if (id == 3) ShowHelp();
            else if (id == 4) ReplaceImage(); else if (id == 5) RestoreOriginal(); else if (id == 6) DeletePortrait();
        };
        var toolbar = new HBoxContainer(); toolbar.AddThemeConstantOverride("separation", 8); body.AddChild(toolbar);
        _automatic = Button(toolbar, "自动面部取景", () => _ = DetectFace()); _automatic.Disabled = true;
        _automatic.TooltipText = "自动定位面部，并按公式推算半身";
        _faceView = Button(toolbar, "面部", () => SelectView(PortraitView.Face));
        _bustView = Button(toolbar, "半身", () => SelectView(PortraitView.Bust));
        foreach (var button in new[] { _faceView, _bustView })
        {
            button.ToggleMode = true; CompanionPanelStyle.Toggle(button);
        }
        _faceView.TooltipText = "调整蓝色面部框；两项同时选中可一起移动"; _bustView.TooltipText = "调整金色半身框；两项同时选中可一起移动";
        SyncView(PortraitView.Face);
        _framingHint = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, MaxLinesVisible = 1,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Pass };
        _framingHint.AddThemeColorOverride("font_color", ContentEditorStyle.Muted); toolbar.AddChild(_framingHint);
        Button(toolbar, "适应画布", () => _canvas.ResetView());
        var editing = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        editing.AddThemeConstantOverride("separation", 12); body.AddChild(editing);
        _canvas = new PortraitEditorCanvas { CustomMinimumSize = new(300, 260), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, Name = "PortraitCropCanvas" }; editing.AddChild(_canvas);
        _canvas.AnchorChanged = AnchorChanged;
        _canvas.ManualEditStarted = CancelAnalysis;
        _canvas.ViewChanged = view => { if (!_canvas.MoveBoth) SyncView(view); ShowCurrent(); };
        _canvas.FaceSelected = index =>
        {
            if (_candidateKey == _current && index >= 0 && index < _canvas.Candidates.Length)
                ApplyFace(_canvas.Candidates[index]);
        };
        var previewScroll = new ScrollContainer { CustomMinimumSize = new(176, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        editing.AddChild(previewScroll);
        _previews = new PortraitEditorPreviews { CustomMinimumSize = new(160, 380), MouseFilter = MouseFilterEnum.Ignore }; previewScroll.AddChild(_previews);
        var coordinates = new HBoxContainer(); coordinates.AddThemeConstantOverride("separation", 8); body.AddChild(coordinates);
        coordinates.AddChild(new Label { Text = "X" }); _x = Coordinate(coordinates);
        coordinates.AddChild(new Label { Text = "Y" }); _y = Coordinate(coordinates);
        coordinates.AddChild(new Label { Text = "px", MouseFilter = MouseFilterEnum.Ignore });
        _x.ValueChanged += _ => CoordinateChanged(); _y.ValueChanged += _ => CoordinateChanged();
        _x.GetLineEdit().TextChanged += _ => { if (!_refreshing) { _xDirty = true; CancelAnalysis(); } };
        _y.GetLineEdit().TextChanged += _ => { if (!_refreshing) { _yDirty = true; CancelAnalysis(); } };
        _references = new Label { Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        _references.AddThemeColorOverride("font_color", ContentEditorStyle.Muted); coordinates.AddChild(_references);
        _errorDialog = new AcceptDialog { Title = "操作失败", OkButtonText = "关闭" }; AddChild(_errorDialog);
        _saveFeedbackTimer = new global::Godot.Timer { OneShot = true, WaitTime = 2 }; AddChild(_saveFeedbackTimer);
        _saveFeedbackTimer.Timeout += () => _save.Text = "保存";
        _save.Disabled = _import.Disabled = true; _undo.Disabled = _redo.Disabled = true;
        Load();
    }

    private static Button Button(Node parent, string text, Action action) => ContentEditorControls.ActionButton(parent, text, action);
    private static SpinBox Coordinate(Node parent)
    {
        var field = new SpinBox { MinValue = 0, MaxValue = 0, Step = 1, Rounded = true,
            CustomMinimumSize = new(100, 34), UpdateOnTextChanged = false };
        parent.AddChild(field); return field;
    }
}
