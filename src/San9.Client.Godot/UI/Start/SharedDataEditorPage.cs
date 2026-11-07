using Godot;
using San9.Application.Authoring;

namespace San9.Client.Godot;

/// <summary>Pages edit one parent-owned draft and expose pending control input to the global save.</summary>
public abstract partial class SharedDataEditorPage : Control
{
    public SharedDataEditorWorkspace Workspace { get; init; } = null!;
    public Action<string, bool> ReportStatus { get; init; } = null!;
    public abstract bool HasPendingChanges { get; }
    public abstract bool CommitPending();
    public abstract void RefreshAfterSave();
    public virtual void Saved() { if (IsVisibleInTree()) RefreshAfterSave(); }
    public virtual void Activate() { }
    protected void Status(string message, bool error = false) => ReportStatus(message, error);
}
