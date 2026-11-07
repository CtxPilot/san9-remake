using San9.Content.Packages;
using San9.Importer.San9PkTc101.Packages;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class MovieLibraryImporterTests
{
    private sealed class CopyEncoder : IMediaEncoder
    {
        public Task EncodeAsync(string source, string output, IReadOnlyList<string> options, CancellationToken token)
        {
            File.Copy(source, output);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GroupedConversionMapsOriginalMoviesAndRetainsExistingAndAuthoredBindings()
    {
        string root = Path.Combine(Path.GetTempPath(), "san9-movies-" + Guid.NewGuid().ToString("N"));
        string prepared = Path.Combine(root, "MOVIE"), output = Path.Combine(root, "content");
        Directory.CreateDirectory(prepared); Directory.CreateDirectory(output);
        try
        {
            byte[] header = new byte[36];
            "OggS"u8.CopyTo(header); header[26] = 1; header[27] = 7; header[28] = 0x80;
            "theora"u8.CopyTo(header.AsSpan(29));
            foreach (string family in new[] { "h", "v" })
                for (int index = 0; index < 10; index++)
                {
                    header[35]++;
                    await File.WriteAllBytesAsync(Path.Combine(prepared, $"Sop_{family}{index:D2}.mpg"), header);
                }
            await File.WriteAllBytesAsync(Path.Combine(output, "retained.ogv"), header);
            string authored = "my-authored-opening";
            await MovieCatalogFiles.WriteAsync(output, new(1, new()
            { ["three-visits-first"] = "retained.ogv", [authored] = "retained.ogv" }));
            await MovieLibraryImporter.ImportAsync(root, output, MovieLibraryGroup.Scenarios, new CopyEncoder());
            var catalog = await MovieCatalogFiles.LoadAsync(output);
            Assert.Equal(22, catalog.Movies.Count);
            Assert.Equal("retained.ogv", catalog.Movies["three-visits-first"]);
            Assert.Equal("retained.ogv", catalog.Movies[authored]);
            foreach (var (id, source) in new[] { ("0", "Sop_h00"), ("4", "Sop_h04"), ("9", "Sop_h09"), ("10", "Sop_v00"), ("19", "Sop_v09") })
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(prepared, source + ".mpg")),
                    await File.ReadAllBytesAsync(PackageFiles.Resolve(output, catalog.Movies[MovieLibraryImporter.ScenarioOpeningRole(int.Parse(id, System.Globalization.CultureInfo.InvariantCulture))])));

            for (int index = 0; index < 14; index++)
            {
                header[35]++;
                await File.WriteAllBytesAsync(Path.Combine(prepared, $"Event{index:D2}.mpg"), header);
            }
            await MovieLibraryImporter.ImportAsync(root, output, MovieLibraryGroup.Events, new CopyEncoder());
            var published = await MovieCatalogFiles.LoadAsync(output);
            Assert.Equal(35, published.Movies.Count);
            foreach (var entry in catalog.Movies.Where(p => p.Key != "three-visits-first"))
                Assert.Equal(entry.Value, published.Movies[entry.Key]);
            foreach (var (role, source) in new[]
            {
                ("chained-prelude", "Event00"), ("chained-coup", "Event01"),
                ("three-visits-first", "Event02"), ("three-visits-recruitment", "Event03"),
                ("guandu", "Event04"), ("red-cliffs", "Event05"), ("force-activation", "Event06"),
                ("imperial-ceremony", "Event07"), ("hanzhong", "Event08"), ("emperor-restoration", "Event09"),
                ("expedition-memorial", "Event10"), ("five-tigers", "Event11"), ("qiao-marriage", "Event12"),
                ("sun-marriage", "Event13")
            })
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(prepared, source + ".mpg")),
                    await File.ReadAllBytesAsync(PackageFiles.Resolve(output, published.Movies[role])));

            foreach (string name in new[] { "theme", "op_pk" })
            {
                header[35]++;
                await File.WriteAllBytesAsync(Path.Combine(prepared, name + ".mpg"), header);
            }
            await MovieLibraryImporter.ImportAsync(root, output, MovieLibraryGroup.Startup, new CopyEncoder());
            var complete = await MovieCatalogFiles.LoadAsync(output);
            Assert.Equal(37, complete.Movies.Count);
            foreach (var entry in published.Movies) Assert.Equal(entry.Value, complete.Movies[entry.Key]);
            foreach (var (role, source) in new[] { (MovieCatalog.StartupThemeRole, "theme"), (MovieCatalog.StartupOpeningRole, "op_pk") })
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(prepared, source + ".mpg")),
                    await File.ReadAllBytesAsync(PackageFiles.Resolve(output, complete.Movies[role])));
            Assert.DoesNotContain(complete.Movies.Keys, role => role.Contains("logo", StringComparison.OrdinalIgnoreCase));
        }
        finally { Directory.Delete(root, true); }
    }
}
