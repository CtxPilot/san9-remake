using San9.Importer.San9PkTc101.Interface;

namespace San9.Importer.San9PkTc101.Tests;

public sealed class AudioAssetCompilerTests
{
    // RE-0182: command constructors 25..36, then prepare_command_participants; event kinds are a separate domain.
    [Theory]
    [InlineData("false-report", "SE/SE_C003.WAV")]
    [InlineData("disorder", "SE/SE_C003.WAV")]
    [InlineData("inspire", "SE/SE_C004.WAV")]
    [InlineData("rescue", "SE/SE_C007.WAV")]
    [InlineData("alienate", "SE/SE_C003.WAV")]
    [InlineData("burn", "SE/SE_C003.WAV")]
    [InlineData("seize", "SE/SE_C003.WAV")]
    [InlineData("rumor", "SE/SE_C003.WAV")]
    [InlineData("gift", "SE/SE_C004.WAV")]
    [InlineData("request", "SE/SE_C004.WAV")]
    [InlineData("return", "SE/SE_C004.WAV")]
    [InlineData("surrender", "SE/SE_C004.WAV")]
    public void MissionDepartureSoundsFollowCommandConstructorsRatherThanEventKinds(string kind, string source)
    {
        var message = MissionPresentationCompiler.Compile()[kind + "-start"];
        var sound = Assert.Single(CommandPresentationCompiler.SoundSources, s => s.Role == message.Sound);
        Assert.Equal(source, sound.File);
    }

    // RE-0095/0096/0097: independently checked against the TC101 command calls and SE table.
    [Theory]
    [InlineData("commerce-result", "SE/SE_C008.WAV")]
    [InlineData("cultivation-result", "SE/SE_C000.WAV")]
    [InlineData("domestic-result", "SE/SE_C006.WAV")]
    [InlineData("facility-result", "SE/SE_G005.WAV")]
    [InlineData("recruit-result", "SE/SE_C002.WAV")]
    [InlineData("trade-start", "SE/SE_C001.WAV")]
    [InlineData("repair-complete", "SE/se_m002.WAV")]
    [InlineData("patrol-citizen", "SE/SE_G008.WAV")]
    public void FacilitySoundsMatchOriginalCommandCues(string role, string source)
    {
        var sound = Assert.Single(CommandPresentationCompiler.SoundSources, s => s.Role == role);
        Assert.Equal(source, sound.File);
    }

    [Fact]
    public void ConstructionSoundsSeparateFortsFromBothOrientationsOfObstacles()
    {
        var sounds = MapAudioCompiler.ConstructionSounds("SE21", "SE22");
        foreach (int facility in new[] { 6, 7, 8, 9 })
            Assert.Equal("SE21", sounds[facility]);
        foreach (int facility in new[] { 10, 11, 12, 13, 14, 15, 16 })
            Assert.Equal("SE22", sounds[facility]);
    }

    [Fact]
    public void SharedPlaybackRolesReferenceOnePhysicalEffect()
    {
        var sources = CommandPresentationCompiler.SoundSources;
        var study = sources.Single(s => s.Role == "decision-study");
        var departure = sources.Single(s => s.Role == "mission-departure");
        Assert.Equal(AudioAssetCompiler.Path(study.File), AudioAssetCompiler.Path(departure.File));
        Assert.StartsWith("shared/sound-effects/", AudioAssetCompiler.Path(study.File));
    }

    [Fact]
    public void MusicIdentityDoesNotBindEncodingAndUnknownMeaningPreservesSourceName()
    {
        Assert.All(MapAudioCompiler.MusicSources, source =>
        {
            Assert.Empty(System.IO.Path.GetExtension(source.File));
            Assert.StartsWith("shared/music/", AudioAssetCompiler.Path(source.File));
        });
        Assert.Equal("shared/music/bgm_custom.ogg", AudioAssetCompiler.Path("BGM/BGM_CUSTOM.WMA"));
        Assert.Equal("shared/music/title.ogg", AudioAssetCompiler.Path("BGM/BGM_01.ogg"));
        Assert.Equal("shared/sound-effects/se_custom.wav", AudioAssetCompiler.Path("SE/SE_CUSTOM.WAV"));
        Assert.Equal(AudioAssetCompiler.Path("SE/SE_G011.WAV"), AudioAssetCompiler.Path("se/se_g011.wav"));
    }

    [Fact]
    public void OriginalMusicBindsConvertedOggAndPreparedOnlyMusicRetainsItsFormat()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "san9-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(System.IO.Path.Combine(root, "BGM"));
        Directory.CreateDirectory(System.IO.Path.Combine(root, "SE"));
        try
        {
            string ogg = System.IO.Path.Combine(root, "BGM", "BGM_04.ogg");
            File.WriteAllBytes(ogg, []);
            File.WriteAllBytes(System.IO.Path.Combine(root, "BGM", "BGM_04.WMA"), []);
            Assert.Equal("shared/music/map-spring.ogg", AudioAssetCompiler.Path(
                AudioAssetCompiler.Resolve(AudioAssetCompiler.Discover(root), "BGM/BGM_04")));
            File.Delete(ogg);
            Assert.Equal("shared/music/map-spring.ogg", AudioAssetCompiler.Path(
                AudioAssetCompiler.Resolve(AudioAssetCompiler.Discover(root), "BGM/BGM_04")));
            File.Delete(System.IO.Path.Combine(root, "BGM", "BGM_04.WMA"));
            File.WriteAllBytes(System.IO.Path.Combine(root, "BGM", "BGM_04.MP3"), []);
            Assert.Equal("shared/music/map-spring.mp3", AudioAssetCompiler.Path(
                AudioAssetCompiler.Resolve(AudioAssetCompiler.Discover(root), "BGM/BGM_04")));
            File.WriteAllBytes(ogg, []);
            Assert.Throws<InvalidDataException>(() => AudioAssetCompiler.Discover(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void MissingMediaStillCompilesReferencesWithoutInstallingFiles()
    {
        var files = AudioAssetCompiler.Discover(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Empty(files);
        Assert.Equal("shared/music/map-spring.ogg", AudioAssetCompiler.Path(AudioAssetCompiler.Resolve(files, "BGM/BGM_04")));
        Assert.Equal("shared/sound-effects/construction.wav", AudioAssetCompiler.Path(AudioAssetCompiler.Resolve(files, "SE/SE_M000.WAV")));
    }

    [Fact]
    public void ActionCandidatesIncludeThirdVariantsWithoutMixingFixedShipAttackFamilies()
    {
        foreach (int first in new[] { 30, 33, 36, 39, 42, 45, 56, 59, 64, 67 })
            Assert.Equal(new[] { first, first + 1, first + 2 }, MapAudioCompiler.ActionVariants(first));
        Assert.Equal(new[] { 73 }, MapAudioCompiler.ActionVariants(73));
        Assert.Equal(new[] { 74 }, MapAudioCompiler.ActionVariants(74));
        Assert.Equal(new[] { 70 }, MapAudioCompiler.ActionVariants(70));
        Assert.Empty(MapAudioCompiler.ActionVariants(-1));
    }
}
