using System.Text.Json;
using System.Text.Json.Serialization;
using San9.Content.Images;
using San9.Content.Interface;

namespace San9.Content.Packages;

public sealed class OpeningContentJsonConverter : JsonConverter<OpeningContent>
{
    private sealed record Manifest(int SchemaVersion, ImageFile[] Files, StartMenuPresentation Menu,
        ScenarioSelectionPresentation Selection, OpeningAudioDefinition Audio);

    public override OpeningContent Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty("schemaVersion", out var version) ||
            !version.TryGetInt32(out int number) || number != OpeningContent.CurrentSchemaVersion)
            throw new InvalidDataException("Unsupported opening content version; rebuild opening content.");
        var table = ImageFileTable.Read(document.RootElement, options);
        var manifest = document.RootElement.Deserialize<Manifest>(table.Options(options))
            ?? throw new JsonException("Empty opening content.");
        ContentStructure.Validate(manifest);
        return new(manifest.Menu, manifest.Selection) { Audio = manifest.Audio };
    }

    public override void Write(Utf8JsonWriter writer, OpeningContent opening, JsonSerializerOptions options)
    {
        var table = ImageFileTable.FromAssets(opening.Selection.Forces.MapSkin.Values);
        JsonSerializer.Serialize(writer, new Manifest(OpeningContent.CurrentSchemaVersion, table.Files,
            opening.Menu, opening.Selection, opening.Audio), table.Options(options));
    }
}
