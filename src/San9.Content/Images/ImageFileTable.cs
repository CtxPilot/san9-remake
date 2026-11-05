using System.Text.Json;
using System.Text.Json.Serialization;
using San9.Content.Packages;

namespace San9.Content.Images;

/// <summary>A document-local file table. Runtime images hold resolved file objects, never table indices.</summary>
internal sealed class ImageFileTable
{
    private readonly List<ImageFile> _files = [];
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    public ImageFile[] Files => _files.ToArray();

    public ImageFileTable(IEnumerable<ImageFile> files)
    {
        foreach (var file in files)
        {
            if (_indices.ContainsKey(file.Path)) throw new InvalidDataException($"Duplicate image file: {file.Path}");
            Add(file);
        }
    }

    public static ImageFileTable FromAssets(IEnumerable<ImageAsset> assets)
    {
        var table = new ImageFileTable([]);
        foreach (var asset in assets.OrderBy(a => a.File.Path, StringComparer.Ordinal)) table.Add(asset.File);
        return table;
    }

    public static ImageFileTable Read(JsonElement document, JsonSerializerOptions options)
    {
        if (!document.TryGetProperty("files", out var value)) throw new JsonException("Missing image file table.");
        var files = value.Deserialize<ImageFile[]>(options)
            ?? throw new JsonException("Missing image file table.");
        ContentStructure.Validate(files);
        return new(files);
    }

    private int Add(ImageFile file)
    {
        if (_indices.TryGetValue(file.Path, out int index))
        {
            if (_files[index] != file) throw new InvalidDataException($"Conflicting physical image metadata: {file.Path}");
            return index;
        }
        _ = PackageFiles.Resolve(".", file.Path);
        PngImage.ValidateDimensions(file.Width, file.Height, file.Channels);
        index = _files.Count;
        _indices.Add(file.Path, index); _files.Add(file);
        return index;
    }

    public JsonSerializerOptions Options(JsonSerializerOptions options) => new(options)
    { Converters = { new ImageReferenceConverter(this) } };

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record ImageReference(string Id, int File, int X, int Y, int Width, int Height);

    private sealed class ImageReferenceConverter(ImageFileTable table) : JsonConverter<ImageAsset>
    {
        public override ImageAsset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var reference = JsonSerializer.Deserialize<ImageReference>(ref reader, options)
                ?? throw new JsonException("Missing image reference.");
            if ((uint)reference.File >= (uint)table._files.Count)
                throw new InvalidDataException($"Unknown image file index: {reference.File}");
            var image = new ImageAsset(reference.Id, table._files[reference.File], reference.X, reference.Y, reference.Width, reference.Height);
            image.Validate();
            return image;
        }

        public override void Write(Utf8JsonWriter writer, ImageAsset image, JsonSerializerOptions options)
        {
            image.Validate();
            JsonSerializer.Serialize(writer, new ImageReference(image.Id, table.Add(image.File), image.X, image.Y, image.Width, image.Height), options);
        }
    }
}
