using Godot;
using San9.Application.Authoring;

namespace San9.Client.Godot;

public partial class TacticLinkEditorVerification : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private async void Run()
    {
        try
        {
            string root = System.Environment.GetEnvironmentVariable("SAN9_TACTIC_LINK_CONTENT")
                ?? throw new InvalidOperationException("Set SAN9_TACTIC_LINK_CONTENT to the content root.");
            await CheckTacticLinks(root);
            GD.Print("TACTIC_LINK_EDITOR_OK"); GetTree().Quit();
        }
        catch (Exception error) { GD.PrintErr(error); GetTree().Quit(1); }
    }

    private static T Find<T>(Node owner, string name) where T : Node => (T)owner.FindChild(name, true, false);

    private async Task CheckTacticLinks(string root)
    {
        var workspace = await SharedDataEditorWorkspace.OpenAsync(root);
        var before = workspace.TacticLinkPairs.First();
        var editor = new TacticLinkEditor { Workspace = workspace, ReportStatus = (message, _) => throw new InvalidOperationException(message) };
        AddChild(editor);
        var table = editor.GetNode<Tree>("TacticLinkPairs");
        if (table.GetRoot()?.GetChildCount() != workspace.TacticLinkPairs.Count)
            throw new InvalidOperationException("The initial relationship table is incomplete.");
        table.GetRoot().GetFirstChild().Select(0);
        Find<SpinBox>(editor, "TacticLinkBonus").Value = before.Bonus == 100 ? 99 : before.Bonus + 1;
        if (workspace.TacticLinkPairs.First().Bonus == before.Bonus)
            throw new InvalidOperationException("Editing the selected bonus did not update the draft.");
        Find<Button>(editor, "RemoveTacticLinkPair").EmitSignal(BaseButton.SignalName.Pressed);
        if (workspace.TacticLinkPairs.Any(p => p.FirstId == before.FirstId && p.SecondId == before.SecondId))
            throw new InvalidOperationException("Deleting a pair did not update the draft.");
        Find<Button>(editor, "AddTacticLinkPair").EmitSignal(BaseButton.SignalName.Pressed);
        if (!editor.HasPendingChanges) throw new InvalidOperationException("The open add dialog was not tracked.");
        var dialog = editor.GetNode<ConfirmationDialog>("AddTacticLinkDialog");
        async Task Choose(string field, int id)
        {
            var button = Find<Button>(dialog, field);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            var candidates = Find<ItemList>(button, field + "Candidates");
            int index = Enumerable.Range(0, candidates.ItemCount).Single(i => candidates.GetItemText(i).EndsWith($"（{id}）", StringComparison.Ordinal));
            candidates.EmitSignal(ItemList.SignalName.ItemActivated, index);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await Choose("TacticLinkFirst", before.SecondId); await Choose("TacticLinkSecond", before.FirstId);
        Find<SpinBox>(dialog, "NewTacticLinkBonus").Value = 37;
        dialog.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        if (editor.HasPendingChanges || !workspace.TacticLinkPairs.Any(p => p.FirstId == before.FirstId && p.SecondId == before.SecondId && p.Bonus == 37))
            throw new InvalidOperationException("The add dialog did not commit a canonical pair.");
        var search = Find<LineEdit>(editor, "TacticLinkSearch");
        search.Text = workspace.GetOfficer(before.FirstId).Name;
        if (table.GetRoot().GetChildCount() == 0) throw new InvalidOperationException("Filtering hid a matching pair.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        editor.Free();
    }
}
