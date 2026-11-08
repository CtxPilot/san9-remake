using San9.Content.Interface;

namespace San9.Importer.San9PkTc101.Interface;

/// <summary>Source-to-asset identity is independent of the number of playback roles.</summary>
internal static class AudioAssetCompiler
{
    private static readonly Dictionary<string, string> SemanticPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bgm/bgm_00"] = "shared/music/unification-victory",
        ["bgm/bgm_01"] = "shared/music/title",
        ["bgm/bgm_02"] = "shared/music/game-setup",
        ["bgm/bgm_03"] = "shared/music/scenario-opening",
        ["bgm/bgm_04"] = "shared/music/map-spring",
        ["bgm/bgm_05"] = "shared/music/map-summer",
        ["bgm/bgm_06"] = "shared/music/map-autumn",
        ["bgm/bgm_07"] = "shared/music/map-winter",
        ["bgm/bgm_08"] = "shared/music/map-spring-high-rank",
        ["bgm/bgm_09"] = "shared/music/map-summer-high-rank",
        ["bgm/bgm_10"] = "shared/music/map-autumn-high-rank",
        ["bgm/bgm_11"] = "shared/music/map-winter-high-rank",
        ["bgm/bgm_12"] = "shared/music/battle-normal",
        ["bgm/bgm_13"] = "shared/music/battle-high-rank",
        ["bgm/bgm_14"] = "shared/music/march-normal",
        ["bgm/bgm_15"] = "shared/music/march-high-rank",
        ["bgm/bgm_16"] = "shared/music/domestic-affairs",
        ["bgm/bgm_17"] = "shared/music/strategy",
        ["bgm/bgm_18"] = "shared/music/event",
        ["bgm/bgm_19"] = "shared/music/friendship",
        ["bgm/bgm_20"] = "shared/music/rank-promotion",
        ["bgm/bgm_21"] = "shared/music/diplomacy",
        ["bgm/bgm_22"] = "shared/music/ending-main",
        ["bgm/bgm_23"] = "shared/music/ending-finale",
        ["bgm/bgm_24"] = "shared/music/capture-victory",
        ["bgm/bgm_25"] = "shared/music/settlement-lost",
        ["bgm/bgm_26"] = "shared/music/force-eliminated",
        ["se/se_a000"] = "shared/sound-effects/infantry-attack-1",
        ["se/se_a001"] = "shared/sound-effects/infantry-attack-2",
        ["se/se_a002"] = "shared/sound-effects/infantry-attack-3",
        ["se/se_a003"] = "shared/sound-effects/elephant-attack-1",
        ["se/se_a004"] = "shared/sound-effects/elephant-attack-2",
        ["se/se_a005"] = "shared/sound-effects/elephant-attack-3",
        ["se/se_a006"] = "shared/sound-effects/elephant-battle-1",
        ["se/se_a007"] = "shared/sound-effects/elephant-battle-2",
        ["se/se_a008"] = "shared/sound-effects/arrow-launch-1",
        ["se/se_a009"] = "shared/sound-effects/arrow-launch-2",
        ["se/se_a010"] = "shared/sound-effects/arrow-launch-3",
        ["se/se_a011"] = "shared/sound-effects/ram-unit-attack-1",
        ["se/se_a012"] = "shared/sound-effects/ram-unit-attack-2",
        ["se/se_a013"] = "shared/sound-effects/ram-unit-attack-3",
        ["se/se_a014"] = "shared/sound-effects/catapult-launch",
        ["se/se_a015"] = "shared/sound-effects/stone-impact",
        ["se/se_a017"] = "shared/sound-effects/ram-building-impact",
        ["se/se_a018"] = "shared/sound-effects/light-ship-attack",
        ["se/se_a019"] = "shared/sound-effects/warship-attack",
        ["se/se_a020"] = "shared/sound-effects/rockfall-impact",
        ["se/se_a025"] = "shared/sound-effects/cinematic-infantry-attack",
        ["se/se_a026"] = "shared/sound-effects/cinematic-arrow-attack",
        ["se/se_a028"] = "shared/sound-effects/cinematic-light-ship-attack",
        ["se/se_a029"] = "shared/sound-effects/cinematic-warship-attack",
        ["se/se_a030"] = "shared/sound-effects/cinematic-assault-attack",
        ["se/se_a032"] = "shared/sound-effects/cinematic-crossbow-attack",
        ["se/se_a033"] = "shared/sound-effects/cinematic-light-ship-assault",
        ["se/se_a034"] = "shared/sound-effects/cinematic-warship-assault",
        ["se/se_a035"] = "shared/sound-effects/cinematic-mounted-shot",
        ["se/se_b000"] = "shared/sound-effects/battle-ambience-1",
        ["se/se_b001"] = "shared/sound-effects/battle-ambience-2",
        ["se/se_b002"] = "shared/sound-effects/battle-ambience-3",
        ["se/se_b003"] = "shared/sound-effects/cinematic-battle-close",
        ["se/se_b004"] = "shared/sound-effects/cinematic-battle-distant",
        ["se/se_b006"] = "shared/sound-effects/duel-ambience",
        ["se/se_c000"] = "shared/sound-effects/cultivation-complete",
        ["se/se_c001"] = "shared/sound-effects/trade-start",
        ["se/se_c002"] = "shared/sound-effects/recruit-result",
        ["se/se_c003"] = "shared/sound-effects/strategy-departure",
        ["se/se_c004"] = "shared/sound-effects/mission-departure-and-study",
        ["se/se_c006"] = "shared/sound-effects/patrol-complete",
        ["se/se_c007"] = "shared/sound-effects/order-start",
        ["se/se_c008"] = "shared/sound-effects/commercial-development-complete",
        ["se/se_d000"] = "shared/sound-effects/infantry-defeat-1",
        ["se/se_d001"] = "shared/sound-effects/cavalry-defeat",
        ["se/se_d002"] = "shared/sound-effects/light-ship-defeat",
        ["se/se_d003"] = "shared/sound-effects/warship-defeat",
        ["se/se_d004"] = "shared/sound-effects/siege-engine-defeat",
        ["se/se_d005"] = "shared/sound-effects/unit-hit-or-removal",
        ["se/se_d007"] = "shared/sound-effects/elephant-defeat",
        ["se/se_d008"] = "shared/sound-effects/cinematic-infantry-or-building-hit",
        ["se/se_d009"] = "shared/sound-effects/cinematic-light-ship-hit",
        ["se/se_d010"] = "shared/sound-effects/cinematic-warship-hit",
        ["se/se_d000a"] = "shared/sound-effects/infantry-defeat-2",
        ["se/se_d000b"] = "shared/sound-effects/infantry-defeat-3",
        ["se/se_e000"] = "shared/sound-effects/fire-trap",
        ["se/se_e001"] = "shared/sound-effects/water-trap",
        ["se/se_e002"] = "shared/sound-effects/pit-trap",
        ["se/se_e003"] = "shared/sound-effects/rock-trap",
        ["se/se_e004"] = "shared/sound-effects/illusion",
        ["se/se_e005"] = "shared/sound-effects/sorcery",
        ["se/se_e006"] = "shared/sound-effects/healing",
        ["se/se_e007"] = "shared/sound-effects/false-report-state",
        ["se/se_e008"] = "shared/sound-effects/unformed-state",
        ["se/se_e009"] = "shared/sound-effects/confusion",
        ["se/se_e010"] = "shared/sound-effects/routed-state",
        ["se/se_e011"] = "shared/sound-effects/insult",
        ["se/se_e013"] = "shared/sound-effects/psychological-attack",
        ["se/se_e014"] = "shared/sound-effects/rescue-or-reinforcement-arrival",
        ["se/se_e015"] = "shared/sound-effects/encouragement",
        ["se/se_e016"] = "shared/sound-effects/high-morale-state",
        ["se/se_f000"] = "shared/sound-effects/tactic-result-unblocked",
        ["se/se_f001"] = "shared/sound-effects/tactic-result-blocked",
        ["se/se_f002"] = "shared/sound-effects/cinematic-contact-attack-1",
        ["se/se_f003"] = "shared/sound-effects/cinematic-contact-attack-2",
        ["se/se_f004"] = "shared/sound-effects/cinematic-contact-attack-3",
        ["se/se_f005"] = "shared/sound-effects/battle-result-digit-stop",
        ["se/se_f006"] = "shared/sound-effects/tactic-result-final-digit",
        ["se/se_f007"] = "shared/sound-effects/battle-result-digit-scroll",
        ["se/se_g000"] = "shared/sound-effects/strategy-order-prompt",
        ["se/se_g001"] = "shared/sound-effects/announcement",
        ["se/se_g002"] = "shared/sound-effects/punishment-report",
        ["se/se_g003"] = "shared/sound-effects/execution-result",
        ["se/se_g005"] = "shared/sound-effects/positive-result",
        ["se/se_g006"] = "shared/sound-effects/negative-result",
        ["se/se_g007"] = "shared/sound-effects/diplomatic-declaration",
        ["se/se_g008"] = "shared/sound-effects/success",
        ["se/se_g009"] = "shared/sound-effects/failure",
        ["se/se_g010"] = "shared/sound-effects/strategy-title",
        ["se/se_g011"] = "shared/sound-effects/execution-title",
        ["se/se_g012"] = "shared/sound-effects/territory-acquired",
        ["se/se_g100"] = "shared/sound-effects/force-merger",
        ["se/se_g101"] = "shared/sound-effects/challenge-opening",
        ["se/se_g104"] = "shared/sound-effects/challenge-failure",
        ["se/se_g105"] = "shared/sound-effects/trainee-departure",
        ["se/se_g106"] = "shared/sound-effects/officer-death-report",
        ["se/se_g999"] = "shared/sound-effects/system-silence",
        ["se/se_h000"] = "shared/sound-effects/tactic-preparation",
        ["se/se_h001"] = "shared/sound-effects/tactic-fierce-attack",
        ["se/se_h002"] = "shared/sound-effects/tactic-struggle",
        ["se/se_h003"] = "shared/sound-effects/tactic-breakthrough",
        ["se/se_h004"] = "shared/sound-effects/tactic-charge",
        ["se/se_h005"] = "shared/sound-effects/tactic-mounted-shot",
        ["se/se_h006"] = "shared/sound-effects/tactic-flying-shot",
        ["se/se_h007"] = "shared/sound-effects/tactic-volley",
        ["se/se_h008"] = "shared/sound-effects/tactic-repeating-shot",
        ["se/se_h009"] = "shared/sound-effects/tactic-sniper",
        ["se/se_h012"] = "shared/sound-effects/tactic-arrow-rain",
        ["se/se_h013"] = "shared/sound-effects/tactic-crossbow-volley",
        ["se/se_h014"] = "shared/sound-effects/tactic-ramming",
        ["se/se_h015"] = "shared/sound-effects/tactic-ship-assault",
        ["se/se_h022"] = "shared/sound-effects/illusion-study",
        ["se/se_h023"] = "shared/sound-effects/sorcery-study",
        ["se/se_h024"] = "shared/sound-effects/duel-finisher-effect",
        ["se/se_h025"] = "shared/sound-effects/duel-resolution-effect",
        ["se/se_h026"] = "shared/sound-effects/tactic-relay-charge",
        ["se/se_h027"] = "shared/sound-effects/tactic-surprise-attack",
        ["se/se_h028"] = "shared/sound-effects/tactic-cavalry-shot",
        ["se/se_h029"] = "shared/sound-effects/tactic-repeating-crossbow",
        ["se/se_i000"] = "shared/sound-effects/duel-return-to-position",
        ["se/se_i001"] = "shared/sound-effects/duel-clash-preparation",
        ["se/se_i002"] = "shared/sound-effects/duel-stamina-depleted",
        ["se/se_i003"] = "shared/sound-effects/duel-defeat-1",
        ["se/se_i004"] = "shared/sound-effects/duel-defeat-2",
        ["se/se_i005"] = "shared/sound-effects/duel-dodge-or-entrance",
        ["se/se_i006"] = "shared/sound-effects/duel-power-strike-windup",
        ["se/se_i007"] = "shared/sound-effects/duel-normal-hit-1",
        ["se/se_i008"] = "shared/sound-effects/duel-normal-hit-2",
        ["se/se_i009"] = "shared/sound-effects/duel-power-strike-hit",
        ["se/se_i010"] = "shared/sound-effects/duel-advantage-or-entrance",
        ["se/se_i011"] = "shared/sound-effects/duel-disadvantage",
        ["se/se_i012"] = "shared/sound-effects/duel-entrance-preparation",
        ["se/se_i013"] = "shared/sound-effects/duel-finisher-windup",
        ["se/se_m000"] = "shared/sound-effects/construction",
        ["se/se_m001"] = "shared/sound-effects/fortification-construction",
        ["se/se_m002"] = "shared/sound-effects/facility-complete-or-repair",
        ["se/se_m003"] = "shared/sound-effects/settlement-damage",
        ["se/se_m004"] = "shared/sound-effects/obstacle-damage",
        ["se/se_m005"] = "shared/sound-effects/demolition-result",
        ["se/se_m006"] = "shared/sound-effects/settlement-capture",
        ["se/se_m007"] = "shared/sound-effects/fire-smoke",
        ["se/se_m008"] = "shared/sound-effects/capture-smoke",
        ["se/se_s000"] = "shared/sound-effects/title-entry",
        ["se/se_s001"] = "shared/sound-effects/game-start-or-force-reselection",
        ["se/se_v000"] = "shared/sound-effects/infantry-march-1",
        ["se/se_v000a"] = "shared/sound-effects/infantry-march-2",
        ["se/se_v000b"] = "shared/sound-effects/infantry-march-3",
        ["se/se_v001"] = "shared/sound-effects/cavalry-march-1",
        ["se/se_v001a"] = "shared/sound-effects/cavalry-march-2",
        ["se/se_v001b"] = "shared/sound-effects/cavalry-march-3",
        ["se/se_v002"] = "shared/sound-effects/siege-engine-march-1",
        ["se/se_v002a"] = "shared/sound-effects/siege-engine-march-2",
        ["se/se_v002b"] = "shared/sound-effects/siege-engine-march-3",
        ["se/se_v003"] = "shared/sound-effects/elephant-march-1",
        ["se/se_v003a"] = "shared/sound-effects/elephant-march-2",
        ["se/se_v003b"] = "shared/sound-effects/elephant-march-3",
        ["se/se_v004"] = "shared/sound-effects/light-ship-sailing-1",
        ["se/se_v004a"] = "shared/sound-effects/light-ship-sailing-2",
        ["se/se_v004b"] = "shared/sound-effects/light-ship-sailing-3",
        ["se/se_v005"] = "shared/sound-effects/warship-sailing-1",
        ["se/se_v005a"] = "shared/sound-effects/warship-sailing-2",
        ["se/se_v005b"] = "shared/sound-effects/warship-sailing-3",
        ["se/se_v006"] = "shared/sound-effects/cinematic-infantry-advance",
        ["se/se_v007"] = "shared/sound-effects/cinematic-cavalry-advance",
        ["se/se_v008"] = "shared/sound-effects/cinematic-light-ship-advance",
        ["se/se_v009"] = "shared/sound-effects/cinematic-warship-advance",
        ["se/se_v010"] = "shared/sound-effects/cinematic-assault-advance",
        ["se/se_v011"] = "shared/sound-effects/cinematic-charge-advance",
        ["se/se_v012"] = "shared/sound-effects/cinematic-light-ship-assault-advance",
        ["se/se_v013"] = "shared/sound-effects/cinematic-warship-assault-advance",
    };

    internal static string Path(string source)
    {
        string normalized = source.Replace((char)92, '/').ToLowerInvariant();
        string extension = System.IO.Path.GetExtension(normalized);
        if (extension == ".wma") extension = ".ogg";
        if (SemanticPaths.TryGetValue(System.IO.Path.ChangeExtension(normalized, null), out string? semantic)) return semantic + extension;
        string directory = normalized.StartsWith("bgm/", StringComparison.Ordinal) ? "music" : "sound-effects";
        return $"shared/{directory}/{System.IO.Path.GetFileNameWithoutExtension(normalized)}{extension}";
    }

    internal static Dictionary<string, string> Discover(string root, params string[] directories)
    {
        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in directories.Length == 0 ? ["BGM", "SE"] : directories)
        {
            string folder = System.IO.Path.Combine(root, directory);
            string[] paths = Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
            // Original BGM is authoritative; prepared siblings are not dependencies.
            var originals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (directory == "BGM")
                foreach (string path in paths.Where(p => System.IO.Path.GetExtension(p).Equals(".wma", StringComparison.OrdinalIgnoreCase)))
                {
                    string source = directory + "/" + System.IO.Path.GetFileName(path);
                    string key = System.IO.Path.ChangeExtension(source, null);
                    if (!files.TryAdd(key, source))
                        throw new InvalidDataException("Duplicate original BGM: " + source);
                    originals.Add(key);
                }
            foreach (string path in paths.Order(StringComparer.Ordinal))
            {
                if (AudioFileFormats.Identify(path) is null) continue;
                string source = $"{directory}/{System.IO.Path.GetFileName(path)}";
                string key = System.IO.Path.ChangeExtension(source, null);
                if (originals.Contains(key)) continue;
                if (!files.TryAdd(key, source))
                    throw new InvalidDataException("Multiple prepared audio formats for source: " + source);
            }
        }
        return files;
    }

    internal static string Resolve(IReadOnlyDictionary<string, string> files, string source) =>
        files.TryGetValue(System.IO.Path.ChangeExtension(source, null), out string? file) ? file :
        System.IO.Path.HasExtension(source) ? source : source + (source.StartsWith("BGM/", StringComparison.OrdinalIgnoreCase) ? ".wma" : ".wav");
}
