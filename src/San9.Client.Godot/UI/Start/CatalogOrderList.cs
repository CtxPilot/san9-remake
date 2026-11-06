using Godot;

namespace San9.Client.Godot;

/// <summary>Reorders stable catalog identities using native drag/drop and insertion feedback.</summary>
public partial class CatalogOrderList : ItemList
{
    public Func<bool> CanReorder { get; init; } = () => false;
    public Action<int, int> Reorder { get; init; } = null!;
    private int _slot = -1;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!CanReorder() || !CanProcess()) return default;
        int row = GetItemAtPosition(atPosition, true);
        if (row < 0) return default;
        SetDragPreview(new Label { Text = GetItemText(row), MouseFilter = MouseFilterEnum.Ignore });
        return new global::Godot.Collections.Dictionary { ["source"] = this, ["id"] = GetItemMetadata(row) };
    }

    private int SourceRow(Variant data)
    {
        if (!CanReorder() || !CanProcess() || data.VariantType != Variant.Type.Dictionary) return -1;
        var payload = data.AsGodotDictionary();
        if (!payload.TryGetValue("source", out var source) || source.VariantType != Variant.Type.Object || source.AsGodotObject() != this ||
            !payload.TryGetValue("id", out var id) || id.VariantType != Variant.Type.Int) return -1;
        for (int i = 0; i < ItemCount; i++) if (GetItemMetadata(i).AsInt32() == id.AsInt32()) return i;
        return -1;
    }

    private int SlotAt(Vector2 position)
    {
        int row = GetItemAtPosition(position);
        if (row < 0) return 0;
        var rect = GetItemRect(row);
        return row + (position.Y + GetVScrollBar().Value >= rect.GetCenter().Y ? 1 : 0);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        int source = SourceRow(data);
        _slot = source < 0 ? -1 : SlotAt(atPosition); QueueRedraw();
        return source >= 0;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int source = SourceRow(data);
        _slot = -1; QueueRedraw();
        if (source < 0) return;
        int slot = SlotAt(atPosition);
        int target = slot > source ? slot - 1 : slot;
        Reorder(GetItemMetadata(source).AsInt32(), target);
    }

    public override void _Process(double delta)
    {
        var viewport = GetViewport();
        if (!viewport.GuiIsDragging() || SourceRow(viewport.GuiGetDragData()) < 0 || !new Rect2(Vector2.Zero, Size).HasPoint(GetLocalMousePosition()))
        {
            if (_slot >= 0) { _slot = -1; QueueRedraw(); }
            return;
        }
        var mouse = GetLocalMousePosition();
        var scroll = GetVScrollBar();
        if (mouse.Y < 28) scroll.Value -= 280 * delta;
        else if (mouse.Y > Size.Y - 28) scroll.Value += 280 * delta;
        _slot = SlotAt(mouse); QueueRedraw();
    }

    public override void _Draw()
    {
        if (_slot < 0 || ItemCount == 0) return;
        var rect = GetItemRect(Math.Min(_slot, ItemCount - 1));
        float y = (float)((_slot == ItemCount ? rect.End.Y : rect.Position.Y) - GetVScrollBar().Value);
        DrawLine(new(4, y), new(Size.X - GetVScrollBar().Size.X - 4, y), new Color("e5c589"), 2);
    }
}
