using Godot;
using San9.Content.Authoring;

namespace San9.Client.Godot;

public partial class CreatedOfficerEditor
{
    private void ChooseParent(IReadOnlyList<(string Label, Guid? Created, int? Historical)> references, CreatedOfficer person)
    {
        var dialog = new ConfirmationDialog { Title = "选择亲代", OkButtonText = "确定", CancelButtonText = "取消", Exclusive = true };
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); dialog.AddChild(column);
        var search = new LineEdit { PlaceholderText = "搜索姓名", Name = "ParentSearch" }; column.AddChild(search);
        var list = new ItemList { CustomMinimumSize = new(540, 350), SizeFlagsVertical = SizeFlags.ExpandFill,
            Name = "ParentCandidates" }; column.AddChild(list);
        var visible = new List<int>();
        int selected = -1;
        void Refresh()
        {
            visible.Clear(); list.Clear(); selected = -1;
            string query = search.Text.Trim();
            for (int i = 0; i < references.Count; i++)
            {
                var candidate = references[i];
                if (i != 0 && !candidate.Label.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                visible.Add(i); list.AddItem(candidate.Label);
                if (candidate.Created == person.ParentId && candidate.Historical == person.ScenarioParentId)
                { selected = i; list.Select(visible.Count - 1); }
            }
            dialog.GetOkButton().Disabled = selected < 0;
        }
        void Apply()
        {
            if (selected < 0) return;
            var candidate = references[selected];
            Change(person.Id, p => p with { ParentId = candidate.Created, ScenarioParentId = candidate.Historical }, rebuild: true);
            dialog.QueueFree();
        }
        search.TextChanged += _ => Refresh();
        list.ItemSelected += index => { selected = visible[(int)index]; dialog.GetOkButton().Disabled = false; };
        list.ItemActivated += index => { selected = visible[(int)index]; Apply(); };
        dialog.Confirmed += Apply; dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog); Refresh(); dialog.PopupCentered(new(600, 470)); search.GrabFocus();
    }
}
