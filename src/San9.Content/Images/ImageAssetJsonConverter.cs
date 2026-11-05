using System.Text.Json;
using System.Text.Json.Serialization;

namespace San9.Content.Images;

/// <summary>Standalone image definitions carry their file metadata; catalog documents supply a file-table converter.</summary>
public sealed class ImageAssetJsonConverter : JsonConverter<ImageAsset>
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record Definition(string Id, ImageFile File, int X, int Y, int Width, int Height);

    public override ImageAsset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var definition = JsonSerializer.Deserialize<Definition>(ref reader, options)
            ?? throw new JsonException("Missing image definition.");
        return new(definition.Id, definition.File, definition.X, definition.Y, definition.Width, definition.Height);
    }

    public override void Write(Utf8JsonWriter writer, ImageAsset image, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new Definition(image.Id, image.File, image.X, image.Y, image.Width, image.Height), options);
}
