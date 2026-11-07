using San9.Client.Godot.Settings;

namespace San9.Client.Godot;

public enum DayPlaybackBlock { None, Interaction, HudTextures, FeedbackTextures, BattleResources, MapBinding, MapResources, Panels }

/// <summary>Orders the execution page's preparation, playback, publication and display updates.</summary>
internal sealed class ExecutionPresentationCoordinator
{
    private readonly TurnPresentation _turn;
    private readonly MapWorld _world;
    private readonly BattlePlaybackFlow _battle;
    private readonly MapFeedbackFlow _feedback;
    private readonly ExecutionPanels _panels;
    private readonly SessionPresentationFlow _presentation;
    private readonly TextureScope _textures;
    private readonly Func<StrategyInteraction> _interaction;
    private readonly Action _updateSeason;

    public ExecutionPresentationCoordinator(TurnPresentation turn, MapWorld world, BattlePlaybackFlow battle,
        MapFeedbackFlow feedback, ExecutionPanels panels, SessionPresentationFlow presentation,
        TextureScope textures, Func<StrategyInteraction> interaction, Action updateSeason)
    {
        _turn = turn; _world = world; _battle = battle; _feedback = feedback;
        _panels = panels; _presentation = presentation; _textures = textures;
        _interaction = interaction; _updateSeason = updateSeason;
        turn.PrepareDay = PrepareDay;
        turn.CanPlayDay = () => ReadDayBlock() == DayPlaybackBlock.None;
        turn.CanContinue = publication => presentation.Publication == publication && interaction().CanContinue;
        turn.CanPresentClosing = () => panels.IsCollapsed;
        turn.PresentDay = PresentDay;
    }

    private void PrepareDay(TurnDayPresentation day)
    {
        if (_interaction().CanPlay && _textures.IsReady && _feedback.ResourcesReady) _battle.Prepare(day);
    }

    public DayPlaybackBlock ReadDayBlock()
    {
        if (!_interaction().CanPlay) return DayPlaybackBlock.Interaction;
        if (!_textures.IsReady) return DayPlaybackBlock.HudTextures;
        if (!_feedback.ResourcesReady) return DayPlaybackBlock.FeedbackTextures;
        if (!_battle.IsReadyFor(_turn.Day)) return DayPlaybackBlock.BattleResources;
        if (!ReferenceEquals(_world.Day, _turn.Day)) return DayPlaybackBlock.MapBinding;
        if (!_world.IsDayReady) return DayPlaybackBlock.MapResources;
        return _panels.IsExpanded ? DayPlaybackBlock.None : DayPlaybackBlock.Panels;
    }

    private void PresentDay(TurnDayPresentation day)
    {
        // Capture finite map feedback before publishing the day can release its inputs.
        _world.RefreshRuntimeObjects();
        _battle.PresentDay(day);
    }

    public void Advance(double delta, PlaybackSpeed speed)
    {
        var previous = _turn.Screen;
        _turn.PlaybackSpeed = speed;
        _turn.Update(delta);
        if (previous == TurnScreen.Returning && _turn.Screen == TurnScreen.Strategy) _feedback.ResetRecords();
        _presentation.Update();
        _world.Presentation = _turn.Display;
        _world.Day = _turn.Day;
        _updateSeason();
        _battle.Update();
        _feedback.Advance(delta);
        bool show = _turn.Screen is TurnScreen.Preparing or TurnScreen.Playing or TurnScreen.AwaitingDecision or TurnScreen.Presenting ||
            _turn.Screen == TurnScreen.Failed && _panels.Expanded;
        bool allowed = _turn.Phase == San9.Core.Simulation.ResolutionPhase.Daily || _turn.IsStrategy;
        _panels.Advance(allowed && (show || _feedback.RecordsRequested), delta, _presentation.HasDecision);
        _panels.Bind(_turn.Display ?? _world.Session.View);
    }
}
