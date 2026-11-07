using Godot;

namespace San9.Client.Godot;

internal static class SkinPainter
{
    public static void MapObjectPicker(CanvasItem canvas, TextureScope textures, Rect2 bounds)
    {
        var p = bounds.Position;
        var size = bounds.Size;
        // Disjoint regions reproduce indexed overwrites without blending translucent edges twice.
        void Part(string part, Vector2 offset, Vector2 extent, Vector2 phase) =>
            TilePhased(canvas, textures.Get("map-object-picker-" + part, canvas), new(p + offset, extent), phase);
        Part("center", new(12, 12), size - new Vector2(24, 24), new(12, 12));
        Part("top", new(12, 0), new(size.X - 24, 12), new(12, 0));
        Part("bottom", new(12, size.Y - 12), new(size.X - 24, 12), new(12, 0));
        Part("left", new(0, 12), new(12, size.Y - 24), new(0, 12));
        Part("right", new(size.X - 12, 12), new(12, size.Y - 24), new(0, 12));
        Part("top-left", Vector2.Zero, new(12, 12), Vector2.Zero);
        Part("top-right", new(size.X - 12, 0), new(12, 12), Vector2.Zero);
        Part("bottom-left", new(0, size.Y - 12), new(12, 12), Vector2.Zero);
        Part("bottom-right", size - new Vector2(12, 12), new(12, 12), Vector2.Zero);
    }

    private static void TilePhased(CanvasItem canvas, Texture2D texture, Rect2 rect, Vector2 phase)
    {
        for (float y = 0; y < rect.Size.Y;)
        {
            float sy = (phase.Y + y) % texture.GetHeight();
            float height = Math.Min(texture.GetHeight() - sy, rect.Size.Y - y);
            for (float x = 0; x < rect.Size.X;)
            {
                float sx = (phase.X + x) % texture.GetWidth();
                float width = Math.Min(texture.GetWidth() - sx, rect.Size.X - x);
                canvas.DrawTextureRectRegion(texture, new(rect.Position + new Vector2(x, y), new(width, height)),
                    new(new(sx, sy), new(width, height)));
                x += width;
            }
            y += height;
        }
    }

    public static void InformationMenu(CanvasItem canvas, TextureScope textures, Rect2 bounds)
    {
        var p = bounds.Position;
        var s = bounds.Size;
        Tile(canvas, textures.Get("information-menu-center", canvas), bounds);
        Tile(canvas, textures.Get("information-menu-top", canvas), new(p, new(s.X, 16)));
        Tile(canvas, textures.Get("information-menu-bottom", canvas), new(p + new Vector2(0, s.Y - 16), new(s.X, 16)));
        Tile(canvas, textures.Get("information-menu-left", canvas), new(p, new(16, s.Y)));
        Tile(canvas, textures.Get("information-menu-right", canvas), new(p + new Vector2(s.X - 16, 0), new(16, s.Y)));
        canvas.DrawTexture(textures.Get("information-menu-top-left", canvas), p);
        canvas.DrawTexture(textures.Get("information-menu-top-right", canvas), p + new Vector2(s.X - 16, 0));
        canvas.DrawTexture(textures.Get("information-menu-bottom-left", canvas), p + new Vector2(0, s.Y - 16));
        canvas.DrawTexture(textures.Get("information-menu-bottom-right", canvas), p + new Vector2(s.X - 32, s.Y - 32));
    }

    public static void Tile(CanvasItem canvas, Texture2D texture, Rect2 rect)
    {
        for (float y = 0; y < rect.Size.Y; y += texture.GetHeight())
        for (float x = 0; x < rect.Size.X; x += texture.GetWidth())
        {
            var size = new Vector2(Math.Min(texture.GetWidth(), rect.Size.X - x), Math.Min(texture.GetHeight(), rect.Size.Y - y));
            canvas.DrawTextureRectRegion(texture, new(rect.Position + new Vector2(x, y), size), new(Vector2.Zero, size));
        }
    }
    public static void Strip(CanvasItem canvas, Texture2D left, Texture2D middle, Texture2D right, Rect2 rect)
    {
        Tile(canvas, middle, new(rect.Position + new Vector2(left.GetWidth(), 0),
            new(rect.Size.X - left.GetWidth() - right.GetWidth(), rect.Size.Y)));
        canvas.DrawTextureRect(left, new(rect.Position, new(left.GetWidth(), rect.Size.Y)), false);
        canvas.DrawTextureRect(right, new(new(rect.End.X - right.GetWidth(), rect.Position.Y), new(right.GetWidth(), rect.Size.Y)), false);
    }
}
