using Godot;
using San9.Content.Images;

namespace San9.Client.Godot;

public partial class CreatedOfficerEditor
{
    private Control PortraitPreview(string? key, Vector2 size, PortraitView view = PortraitView.Full)
    {
        if (key is not null && _portraitService is { } service && service.Portraits.ContainsKey(key))
            return new AsyncPortrait { Service = service, Role = key, View = view,
                CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Ignore };
        var blank = new ColorRect { Color = new("303d50"), CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Ignore };
        var label = new Label { Text = key is null ? "未选择头像" : "头像不可用",
            TooltipText = key ?? "", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); blank.AddChild(label); return blank;
    }

    private void ChoosePortrait()
    {
        if (_portraitService is null || Current is not { } person) return;
        var portraits = _portraitService.Portraits.Values.OrderBy(p => p.Id).ToArray();
        if (portraits.Length == 0) return;
        const int pageSize = 24;
        int page = Math.Max(0, Array.FindIndex(portraits, p => p.Image.Id == person.PortraitKey)) / pageSize;
        var dialog = new AcceptDialog { Title = "选择游戏内头像", OkButtonText = "取消", Exclusive = true };
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); dialog.AddChild(column);
        var filter = new LineEdit { PlaceholderText = "搜索头像编号", Name = "PortraitSearch" }; column.AddChild(filter);
        var scroll = new ScrollContainer { CustomMinimumSize = new(790, 430), SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; column.AddChild(scroll);
        var grid = new GridContainer { Columns = 8 }; grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10); scroll.AddChild(grid);
        var navigation = new HBoxContainer(); column.AddChild(navigation);
        var previous = new Button { Text = "上一页", CustomMinimumSize = new(0, 34) }; navigation.AddChild(previous);
        var pageControls = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        pageControls.AddThemeConstantOverride("separation", 8); navigation.AddChild(pageControls);
        var pageNumber = new SpinBox { MinValue = 1, MaxValue = 1, Step = 1, Rounded = true,
            CustomMinimumSize = new(88, 34), Name = "PortraitPage", TooltipText = "输入页码后按回车跳转，也可用上下箭头调整。" };
        pageControls.AddChild(pageNumber);
        var counter = new Label(); pageControls.AddChild(counter);
        var next = new Button { Text = "下一页", CustomMinimumSize = new(0, 34) }; navigation.AddChild(next);
        bool updatingPage = false;
        previous.Pressed += () => { page--; Refresh(); };
        next.Pressed += () => { page++; Refresh(); };
        pageNumber.ValueChanged += value => { if (!updatingPage) { page = (int)value - 1; Refresh(); } };
        void Refresh()
        {
            foreach (var child in grid.GetChildren()) { grid.RemoveChild(child); child.QueueFree(); }
            var matching = portraits.Where(p => p.Id.ToString().Contains(filter.Text.Trim(), StringComparison.Ordinal)).ToArray();
            int pages = Math.Max(1, (matching.Length + pageSize - 1) / pageSize);
            page = Math.Clamp(page, 0, pages - 1);
            updatingPage = true;
            pageNumber.MaxValue = pages; pageNumber.Value = page + 1; pageNumber.Editable = pages > 1;
            updatingPage = false;
            foreach (var portrait in matching.Skip(page * pageSize).Take(pageSize))
            {
                var button = new Button { CustomMinimumSize = new(88, 136), ToggleMode = true,
                    ButtonPressed = portrait.Image.Id == person.PortraitKey, TooltipText = $"头像 {portrait.Id}" };
                CompanionPanelStyle.Toggle(button);
                grid.AddChild(button);
                var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
                content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                content.OffsetLeft = content.OffsetTop = 4; content.OffsetRight = content.OffsetBottom = -4;
                button.AddChild(content);
                content.AddChild(PortraitPreview(portrait.Image.Id, new(80, 100), PortraitView.Face));
                content.AddChild(new Label { Text = portrait.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = MouseFilterEnum.Ignore });
                button.Pressed += () =>
                {
                    Change(person.Id, p => p with { PortraitKey = portrait.Image.Id }, rebuild: true); dialog.QueueFree();
                };
            }
            counter.Text = $"/ {pages} 页 · {matching.Length} 个头像";
            previous.Disabled = page == 0; next.Disabled = page + 1 >= pages;
            scroll.ScrollVertical = 0;
        }
        filter.TextChanged += _ => { page = 0; Refresh(); };
        dialog.Confirmed += () => dialog.QueueFree(); dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); Refresh(); dialog.PopupCentered(new(850, 620));
    }
}
