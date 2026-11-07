using San9.Content.Packages;

namespace San9.Importer.San9PkTc101.Packages;

public enum MovieLibraryGroup { Events, Scenarios, Startup, All }

/// <summary>Converts original TC101 clips directly into the optional movie library.</summary>
public static class MovieLibraryImporter
{
    internal static string ScenarioOpeningRole(int scenarioId)
    {
        if (scenarioId is < 0 or >= 20) throw new ArgumentOutOfRangeException(nameof(scenarioId));
        return "scenario-opening-" + scenarioId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task<bool> ImportAsync(string gameDirectory, string output,
        MovieLibraryGroup group = MovieLibraryGroup.All, Action<string>? progress = null, CancellationToken token = default, string? ffmpegPath = null)
    {
        using var operation = ImportOperation.Acquire(output);
        return await ImportCoreAsync(operation, gameDirectory, output, group, progress, token, ffmpegPath);
    }

    internal static async Task<bool> ImportCoreAsync(ImportOperation operation, string gameDirectory, string output,
        MovieLibraryGroup group = MovieLibraryGroup.All, Action<string>? progress = null, CancellationToken token = default, string? ffmpegPath = null)
    {
        operation.RequireOutput(output);
        if (!Enum.IsDefined(group)) throw new ArgumentOutOfRangeException(nameof(group));
        var encoder = ffmpegPath is null ? FfmpegEncoder.Find() : new FfmpegEncoder(Path.GetFullPath(ffmpegPath));
        if (encoder is null) { progress?.Invoke("Skipped movies: ffmpeg was not found on PATH."); return false; }
        await ImportAsync(gameDirectory, output, group, encoder, progress, token);
        return true;
    }

    internal static async Task ImportAsync(string gameDirectory, string output, MovieLibraryGroup group,
        IMediaEncoder encoder, Action<string>? progress = null, CancellationToken token = default)
    {
        if (!Enum.IsDefined(group)) throw new ArgumentOutOfRangeException(nameof(group));
        Dictionary<string, string> sources = [];
        if (group is MovieLibraryGroup.Startup or MovieLibraryGroup.All)
        {
            sources.Add(MovieCatalog.StartupThemeRole, "theme.mpg");
            sources.Add(MovieCatalog.StartupOpeningRole, "op_pk.mpg");
        }
        if (group is MovieLibraryGroup.Events or MovieLibraryGroup.All)
        {
            sources.Add("chained-prelude", "Event00.mpg");
            sources.Add("chained-coup", "Event01.mpg");
            sources.Add("three-visits-first", "Event02.mpg");
            sources.Add("three-visits-recruitment", "Event03.mpg");
            sources.Add("guandu", "Event04.mpg");
            sources.Add("red-cliffs", "Event05.mpg");
            sources.Add("force-activation", "Event06.mpg");
            sources.Add("imperial-ceremony", "Event07.mpg");
            sources.Add("hanzhong", "Event08.mpg");
            sources.Add("emperor-restoration", "Event09.mpg");
            sources.Add("expedition-memorial", "Event10.mpg");
            sources.Add("five-tigers", "Event11.mpg");
            sources.Add("qiao-marriage", "Event12.mpg");
            sources.Add("sun-marriage", "Event13.mpg");
        }
        if (group is MovieLibraryGroup.Scenarios or MovieLibraryGroup.All)
            for (int index = 0; index < 20; index++)
            {
                string source = index < 10 ? $"Sop_h{index:D2}.mpg" : $"Sop_v{index - 10:D2}.mpg";
                sources.Add(ScenarioOpeningRole(index), source);
            }
        var existing = await MovieCatalogFiles.LoadAsync(output, token);
        var movies = new Dictionary<string, string>(existing.Movies);
        var conversion = new MediaConversion(output, encoder);
        foreach (var (role, source) in sources)
        {
            string path = $"shared/movies/{role}.ogv";
            await conversion.ConvertAsync(Path.Combine(gameDirectory, "MOVIE", source), path,
                FfmpegEncoder.MovieOptions, progress, token);
            if (!movies.TryGetValue(role, out string? current) || current != path)
            {
                movies[role] = path;
                await MovieCatalogFiles.WriteAsync(output, new(MovieCatalog.CurrentSchemaVersion, movies), token);
            }
        }
    }
}
