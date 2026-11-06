using Godot;
using San9.Content.Authoring;
using San9.Rendering;

namespace San9.Client.Godot;

public partial class CreatedOfficerEditor
{
    private CenterContainer? _flagImage;
    private int? _shownFlagGlyph;
    private void FinishSurnameEdit(Guid? id = null)
    {
        if (_workspace is null) return;
        bool changed = id is Guid person ? Workspace.FinishSurnameEdit(person, Preferences.Current.AutoAssignOfficerFlag)
            : Workspace.FinishSurnameEdits(Preferences.Current.AutoAssignOfficerFlag);
        if (changed) Changed();
    }

    private string FlagName(int glyph) => glyph < 0 ? "无字" :
        Workspace.FlagCatalog?.Surnames.FirstOrDefault(p => p.Value == glyph).Key ?? (glyph == 0 ? "帥" : "自定旗字");

    private void RefreshFlagValue()
    {
        if (_flagImage is null || !IsInstanceValid(_flagImage) || Current is not { } person ||
            _shownFlagGlyph == person.FlagGlyph) return;
        _shownFlagGlyph = person.FlagGlyph;
        _flagImage.TooltipText = FlagName(person.FlagGlyph);
        foreach (var child in _flagImage.GetChildren()) { _flagImage.RemoveChild(child); child.QueueFree(); }
        if (_portraitService is { } service && FlagVisuals.Layers(5, 0, person.FlagGlyph).All(service.AnimationClips.ContainsKey))
            _flagImage.AddChild(new FlagPreview { Service = service, Glyph = person.FlagGlyph, PreviewScale = 2 });
        else
            _flagImage.AddChild(new Label { Text = _portraitService is null ? "旗帜资源未就绪" : "旗字资源不可用" });
    }

    private void BuildFlags(Node parent, CreatedOfficer person)
    {
        var section = Section(parent, "旗号");
        _flagImage = new CenterContainer { CustomMinimumSize = new(0, 112), Name = "OfficerFlagGlyph" };
        section.AddChild(_flagImage); _shownFlagGlyph = null; RefreshFlagValue();
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 6); section.AddChild(actions);
        var choose = AddButton(actions, "选择", ChooseFlag, "ChooseFlagGlyph");
        choose.Disabled = Workspace.FlagCatalog is null || _portraitService is null;
        choose.TooltipText = "选择旗字";
        choose.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var reset = AddButton(actions, "自动", () =>
        { FinishSurnameEdit(); Workspace.AssignFlagFromSurname(person.Id); Changed(); }, "ResetFlagGlyph");
        reset.Disabled = Workspace.FlagCatalog is null;
        reset.TooltipText = "按当前姓氏重新关联旗字，不改变编辑选项。";
        reset.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    private void ChooseFlag()
    {
        FinishSurnameEdit();
        if (Workspace.FlagCatalog is not { } flags || _portraitService is not { } service || Current is not { } person) return;
        int[] glyphs = [-1, ..flags.Glyphs.Order()];
        const int pageSize = 24;
        int page = Math.Max(0, Array.IndexOf(glyphs, person.FlagGlyph)) / pageSize;
        var dialog = new AcceptDialog { Title = "选择旗字", OkButtonText = "取消", Exclusive = true };
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); dialog.AddChild(column);
        var grid = new GridContainer { Columns = 8, CustomMinimumSize = new(704, 222) };
        grid.AddThemeConstantOverride("h_separation", 6); grid.AddThemeConstantOverride("v_separation", 6); column.AddChild(grid);
        var navigation = new HBoxContainer(); column.AddChild(navigation);
        var previous = new Button { Text = "上一页", CustomMinimumSize = new(0, 34) }; navigation.AddChild(previous);
        var counter = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center }; navigation.AddChild(counter);
        var next = new Button { Text = "下一页", CustomMinimumSize = new(0, 34) }; navigation.AddChild(next);
        previous.Pressed += () => { page--; Refresh(); };
        next.Pressed += () => { page++; Refresh(); };
        void Refresh()
        {
            foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            int pages = Math.Max(1, (glyphs.Length + pageSize - 1) / pageSize);
            page = Math.Clamp(page, 0, pages - 1);
            foreach (int glyph in glyphs.Skip(page * pageSize).Take(pageSize))
            {
                var button = new Button { CustomMinimumSize = new(82, 68), TooltipText = FlagName(glyph) };
                if (glyph == person.FlagGlyph) button.AddThemeStyleboxOverride("normal", ContentEditorStyle.Surface("33495c", "b3a36c", 4));
                button.AddChild(new FlagPreview { Service = service, Glyph = glyph, Position = new(7, 8) });
                button.Pressed += () =>
                {
                    Change(person.Id, p => p with { FlagGlyph = glyph }, rebuild: true);
                    dialog.QueueFree();
                };
                grid.AddChild(button);
            }
            previous.Disabled = page == 0; next.Disabled = page + 1 >= pages; counter.Text = $"{page + 1} / {pages}";
        }
        dialog.Confirmed += () => dialog.QueueFree(); dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); Refresh(); dialog.PopupCentered(new(750, 310));
    }
}
