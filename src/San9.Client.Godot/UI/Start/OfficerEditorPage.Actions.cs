using Godot;

namespace San9.Client.Godot;

public partial class OfficerEditorPage
{
    private SharedCatalogActions _actions = null!;
    public override bool HasPendingChanges => _actions.Busy;
    public override bool CommitPending() => !_actions.Busy;

    protected override void BuildSidebarActions(VBoxContainer sidebar)
    {
        _actions = new()
        {
            Page = this, Subject = "武将", Identity = "CatalogOfficer", Current = () => Current,
            CanDelete = Workspace.IsCreatedOfficer, EntryName = id => Workspace.GetOfficer(id).Name,
            Create = async (id, token) => (await Workspace.CreateOfficerAsync(id, token)).Id,
            Delete = Workspace.DeleteOfficerAsync, Select = SelectEntry,
            SuccessMessage = "已更新草稿，尚未保存；新增武将不会自动加入剧本。"
        };
        sidebar.AddChild(_actions);
    }

    protected override void RefreshActions() => _actions.Refresh();
}
