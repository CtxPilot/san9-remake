using Godot;

namespace San9.Client.Godot;

/// <summary>Asset-free check that saving keeps visible controls and defers hidden-page work.</summary>
public partial class SharedEditorRefreshVerification : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private void Run()
    {
        try
        {
            var tabs = new TabContainer(); AddChild(tabs);
            var visible = new SharedEditorRefreshProbe(); tabs.AddChild(visible);
            var hidden = new SharedEditorRefreshProbe(); tabs.AddChild(hidden);
            tabs.CurrentTab = 0;
            var field = visible.Field;
            field.Text = "当前输入";
            visible.Dirty = hidden.Dirty = false;
            visible.Saved(); hidden.Saved();
            if (visible.Field != field || visible.Field.Text != "当前输入" || visible.BuildCount != 1 || hidden.BuildCount != 1)
                throw new InvalidOperationException("Saving rebuilt an editor form.");
            tabs.CurrentTab = 1; hidden.Activate();
            if (hidden.BuildCount != 2 || visible.Field != field)
                throw new InvalidOperationException("Hidden editor did not refresh on activation.");
            GD.Print("SHARED_EDITOR_REFRESH_OK"); GetTree().Quit();
        }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }
}
