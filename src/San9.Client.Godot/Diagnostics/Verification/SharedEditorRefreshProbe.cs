using Godot;

namespace San9.Client.Godot;

internal partial class SharedEditorRefreshProbe : SharedCatalogEditorPage
{
    public int BuildCount { get; private set; }
    public bool Dirty { get; set; } = true;
    public LineEdit Field { get; private set; } = null!;
    protected override string Subject => "验证";
    protected override IEnumerable<(int Id, string Name)> Entries => [(1, "测试项目")];
    protected override bool Changed(int id) => Dirty;
    protected override void Restore(int id) { }
    protected override void BuildForm(int id)
    {
        BuildCount++;
        Field = new LineEdit { Text = "已保存值" }; Form.AddChild(Field);
    }
}
