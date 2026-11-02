using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using San9.Application.Persistence;
using San9.Application.Presentation;

namespace San9.Client.Godot.Settings;

public sealed class UiPreferencesStore
{
    private const int FormatVersion = 1;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<RosterPage>(allowIntegerValues: false),
            new JsonStringEnumConverter<EdgeScrollMode>(allowIntegerValues: false),
            new JsonStringEnumConverter<SelectionViewMode>(allowIntegerValues: false) }
    };
    private readonly string _path;

    public UiPreferencesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("An absolute filesystem path is required.", nameof(path));
        _path = Path.GetFullPath(path);
    }

    public UiPreferences Load()
    {
        string json;
        try { json = File.ReadAllText(_path, Utf8); }
        catch (FileNotFoundException) { return UiPreferences.Default; }
        catch (DirectoryNotFoundException) { return UiPreferences.Default; }
        var document = JsonSerializer.Deserialize<PreferencesDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("UI preferences document is null.");
        if (document.Version != FormatVersion) throw new InvalidDataException($"Unsupported UI preferences version: {document.Version}.");
        var preferences = document.Preferences ?? throw new InvalidDataException("UI preferences are missing.");
        preferences.Validate();
        return preferences;
    }

    public void Save(UiPreferences preferences)
    {
        preferences.Validate();
        var document = new PreferencesDocument(FormatVersion, preferences);
        AtomicFile.Write(_path, Utf8.GetBytes(JsonSerializer.Serialize(document, JsonOptions) + "\n"));
    }

    private sealed record PreferencesDocument(int Version, UiPreferences? Preferences);
}
