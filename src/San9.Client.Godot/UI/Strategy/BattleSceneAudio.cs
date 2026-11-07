using Godot;
using San9.Application.Presentation;
using San9.Rendering;
using San9.Core.Simulation;

namespace San9.Client.Godot;

internal partial class BattleSceneAudio : Node
{
    public string ContentRoot { get; init; } = "";
    public IReadOnlyDictionary<string, string> Sounds { get; init; } = null!;
    public Action<string>? PlaySound { get; init; }
    public Action<string, Vector2>? PlayMapSound { get; init; }
    public Func<bool> IsPaused { get; init; } = () => false;
    public Func<bool> SoundEnabled { get; init; } = () => true;
    public Func<BattleObjectContext, Vector2> Project { get; init; } = null!;
    private BattleVisualCue? _zoom;
    private DuelSceneCue? _duel;
    private bool IsSceneActive;
    private float SceneAudioGain;
    private AudioStreams _streams = null!;
    private AudioStreamPlayer _ambience = null!;
    private AudioStreamPlayer _numberRoll = null!;
    private readonly HashSet<(string Cue, int Index)> _sounds = [];
    private string? _ambienceCue;
    private readonly HashSet<string> _startedSounds = [];
    private readonly HashSet<string> _reinforcementSounds = [];
    private string? _actionSoundCue;
    private readonly List<AudioStreamPlayer?> _actionSounds = [];

    public override void _Ready()
    {
        _streams = new(ContentRoot, Sounds);
        _ambience = new(); AddChild(_ambience);
        _numberRoll = new(); AddChild(_numberRoll);
    }

    public void Reset()
    {
        StopActionSounds();
        _sounds.Clear(); _startedSounds.Clear(); _reinforcementSounds.Clear();
        _ambienceCue = null;
        if (_ambience is not null) { _ambience.Stop(); _ambience.Stream = null; }
        if (_numberRoll is not null) { _numberRoll.Stop(); _numberRoll.Stream = null; }
    }

    public void Advance(TurnDayPresentation day, BattleVisualCue? zoom, DuelSceneCue? duel,
        bool sceneActive, float sceneAudioGain)
    {
        _zoom = zoom; _duel = duel; IsSceneActive = sceneActive; SceneAudioGain = sceneAudioGain;
        var activeVisuals = day.Battle!.Visuals.Where(c => day.IsVisualReady(c) && (c.IsMapEffect
            ? day.IsMapEffectPresented(c.Id) && day.MapEffectElapsed(c.Id) < c.End - c.Start
            : day.Elapsed >= c.Start && day.Elapsed < c.End));
        var mapEffects = day.Battle.MapEffects.Where(c => day.IsFactReady(c.FactIndex) && day.IsDialogueComplete(c.AfterDialogueId));
        double time = day.Elapsed;
        if (_duel?.Sample(time) is { } stage)
        {
            int index = _duel.Stages.IndexOf(stage);
            // A seek plays only the active stage, never sounds belonging to skipped strikes.
            if (_sounds.Add((_duel.Id, index)))
            {
                if (stage.Sound is { } sound) PlaySound?.Invoke(sound);
                if (stage.Effects.FirstOrDefault()?.Sound is { } effectSound && effectSound != stage.Sound) PlaySound?.Invoke(effectSound);
            }
        }
        foreach (var cue in activeVisuals)
        {
            if (_startedSounds.Add(cue.Id))
            {
                if (cue.Start > day.SkippedSceneTime && cue.Effect?.Sound is { } sound) PlaySound?.Invoke(sound);
            }
            if (cue.Doubled && cue.Zoom is { } doubledZoom &&
                BattleReinforcementPlayback.Sample(doubledZoom.DoubleEffect, time - cue.Start) is not null &&
                _reinforcementSounds.Add(cue.Id) && cue.Start + BattleReinforcementPlayback.DelayMilliseconds / 1000.0 > day.SkippedSceneTime &&
                doubledZoom.DoubleEffect[0].Sound is { } doubledSound)
                PlaySound?.Invoke(doubledSound);
            if (cue.Playback is { } playback)
                for (int i = 0; i < playback.Sounds.Length; i++)
                    if (time >= cue.Start + playback.Sounds[i].Time && _sounds.Add((cue.Id, i)) &&
                        cue.Start + playback.Sounds[i].Time > day.SkippedSceneTime)
                        PlaySound?.Invoke(playback.Sounds[i].Role);
        }
        UpdateActionSounds(time);
        if (!IsSceneActive)
            foreach (var cue in mapEffects.Where(c => day.IsMapEffectPresented(c.Id) && day.MapEffectElapsed(c.Id) < c.Sprite.Columns * c.Sprite.FrameMilliseconds / 1000.0))
                if (_startedSounds.Add(cue.Id) && cue.Sprite.Sound is { } role) PlayMapSound?.Invoke(role, Project(cue.Target));
        string? ambienceCue = _zoom?.Id ?? _duel?.Id;
        if (_ambienceCue != ambienceCue)
        { _ambience.Stop(); _ambience.Stream = null; _ambienceCue = ambienceCue; }
        string? ambience = _zoom?.Zoom?.AmbienceSound ?? _duel?.Definition.Sounds["ambience"];
        if (ambience is not null && SoundEnabled())
        {
            _ambience.Stream ??= _streams.Get(ambience, loop: true);
            _ambience.VolumeDb = Mathf.LinearToDb(SceneAudioGain);
            if (!_ambience.Playing) _ambience.Play();
        }
        else _ambience.Stop();
        if (_zoom?.Playback?.Numbers is { } rolling && rolling.IsRolling(time - _zoom.Start) && SoundEnabled() && !IsPaused())
        {
            var stream = _streams.Get(rolling.Definition.RollSound, loop: true);
            if (_numberRoll.Stream != stream) { _numberRoll.Stop(); _numberRoll.Stream = stream; }
            _numberRoll.VolumeDb = Mathf.LinearToDb(SceneAudioGain);
            if (!_numberRoll.Playing) _numberRoll.Play();
        }
        else _numberRoll.Stop();
    }

    private void UpdateActionSounds(double time)
    {
        if (_zoom is not { Playback: { } playback } cue || !SoundEnabled())
        {
            StopActionSounds();
            return;
        }
        if (_actionSoundCue != cue.Id)
        {
            StopActionSounds();
            _actionSoundCue = cue.Id;
            for (int i = 0; i < playback.LoopSounds.Length; i++) _actionSounds.Add(null);
        }
        double elapsed = time - cue.Start;
        for (int i = 0; i < playback.LoopSounds.Length; i++)
        {
            var sound = playback.LoopSounds[i];
            var player = _actionSounds[i];
            if (elapsed < sound.Start || elapsed >= sound.End)
            {
                if (player is not null) ReleaseActionSound(player);
                _actionSounds[i] = null;
                continue;
            }
            bool starting = player is null;
            if (player is null)
            {
                player = new() { Stream = _streams.Get(sound.Role, loop: true) };
                AddChild(player); _actionSounds[i] = player;
            }
            player.VolumeDb = Mathf.LinearToDb(SceneAudioGain * sound.Gain(elapsed));
            if (starting) player.Play();
            player.StreamPaused = IsPaused();
        }
    }

    public override void _ExitTree() { Reset(); _streams.Dispose(); }

    private static void ReleaseActionSound(AudioStreamPlayer player)
    { player.Stop(); player.Stream = null; player.QueueFree(); }

    private void StopActionSounds()
    {
        foreach (var player in _actionSounds)
            if (player is not null) ReleaseActionSound(player);
        _actionSounds.Clear();
        _actionSoundCue = null;
    }
}
