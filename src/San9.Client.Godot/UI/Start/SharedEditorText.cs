using Godot;

namespace San9.Client.Godot;

internal static class SharedEditorText
{
    internal static void Add(Node parent, string title, string value, Action<string> changed, bool multiline = false, string name = "", bool inline = false)
    {
        parent.AddChild(new Label { Text = title, VerticalAlignment = VerticalAlignment.Center });
        if (multiline)
        {
            var field = new TextEdit { Text = value, CustomMinimumSize = new(0, 120), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                WrapMode = TextEdit.LineWrappingMode.Boundary };
            if (name.Length > 0) field.Name = name;
            field.TextChanged += () => changed(field.Text); parent.AddChild(field);
        }
        else
        {
            var field = new LineEdit { Text = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (name.Length > 0) field.Name = name;
            if (inline) field.CustomMinimumSize = new(110, 34);
            field.TextChanged += v => changed(v); parent.AddChild(field);
        }
    }
}
