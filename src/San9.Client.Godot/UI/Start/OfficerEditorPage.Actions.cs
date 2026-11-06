using Godot;

namespace San9.Client.Godot;

public partial class OfficerEditorPage
{
    private readonly CancellationTokenSource _lifetime = new();
    private Button _copy = null!, _delete = null!;
    private bool _busy;
    public override bool HasPendingChanges => _busy;
    public override bool CommitPending() => !_busy;

    protected override void BuildSidebarActions(VBoxContainer sidebar)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); sidebar.AddChild(row);
        var create = ContentEditorControls.ActionButton(row, "新建", () => _ = RunAction(async token =>
            (await Workspace.CreateOfficerAsync(token: token)).Id), "NewCatalogOfficer");
        _copy = ContentEditorControls.ActionButton(row, "复制", () =>
        {
            if (Current is int id) _ = RunAction(async token => (await Workspace.CreateOfficerAsync(id, token)).Id);
        }, "CopyCatalogOfficer");
        _delete = ContentEditorControls.ActionButton(row, "删除", ConfirmDelete, "DeleteCatalogOfficer");
        foreach (var button in new[] { create, _copy, _delete }) button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    protected override void RefreshActions()
    {
        _copy.Disabled = _busy || Current is null;
        _delete.Disabled = _busy || Current is not int id || !Workspace.IsCreatedOfficer(id);
        _delete.TooltipText = _delete.Disabled ? "仅能删除自建武将；原有武将可编辑、可复制。" : "检查引用后删除，保存全部修改后生效。";
    }

    private void ConfirmDelete()
    {
        if (Current is not int id || !Workspace.IsCreatedOfficer(id) || _busy) return;
        var person = Workspace.GetOfficer(id);
        var dialog = new ConfirmationDialog { Title = "删除自建武将", DialogText = $"删除“{person.Name}”？保存全部修改后生效。",
            OkButtonText = "删除", CancelButtonText = "取消", Exclusive = true };
        dialog.Confirmed += () =>
        {
            dialog.QueueFree();
            _ = RunAction(async token => { await Workspace.DeleteOfficerAsync(id, token); return null; });
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); dialog.PopupCentered(new(560, 180));
    }

    private async Task RunAction(Func<CancellationToken, Task<int?>> action)
    {
        if (_busy || !CanProcess()) return;
        var token = _lifetime.Token;
        _busy = true; RefreshActions(); ProcessMode = ProcessModeEnum.Disabled;
        Status("正在检查武将身份与引用…");
        string? error = null;
        int? selection = null;
        try
        {
            selection = await action(token);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception failure) when (failure is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
            or System.Text.Json.JsonException or OverflowException) { error = failure.Message; }
        finally
        {
            if (!token.IsCancellationRequested) { _busy = false; ProcessMode = ProcessModeEnum.Inherit; RefreshActions(); }
        }
        if (token.IsCancellationRequested) return;
        if (error is not null) { ShowActionError(error); return; }
        SelectEntry(selection); Status("已更新草稿，尚未保存；新增武将不会自动加入剧本。");
    }

    private void ShowActionError(string error)
    {
        Status("操作未完成，当前草稿已保留。", true);
        var dialog = new AcceptDialog { Title = "操作未完成", OkButtonText = "关闭", Exclusive = true };
        var scroll = new ScrollContainer { CustomMinimumSize = new(620, 200), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        dialog.AddChild(scroll);
        scroll.AddChild(new Label { Text = error, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill });
        dialog.Confirmed += () => dialog.QueueFree(); dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); dialog.PopupCentered(new(680, 300));
    }

    public override void _ExitTree() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
