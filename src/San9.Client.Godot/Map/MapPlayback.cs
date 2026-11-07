using Godot;
using San9.Application.Presentation;
using San9.Content.Images;
using San9.Content.Maps;
using San9.Core.Domain;
using San9.Core.Simulation;
using San9.Rendering;

namespace San9.Client.Godot;

/// <summary>Owns a scene segment's motion, history, finite effects and sampling caches.
/// Continuation is explicit and pending segments do not advance the presented segment.</summary>
internal sealed partial class MapPlayback
{
    internal sealed record Inputs(MapPackage Map, StrategySnapshot State, MapSceneProjection Projection,
        IReadOnlyDictionary<int, UnitState> RuntimeUnits, IReadOnlyDictionary<string, MapAnimationClip> Clips,
        IReadOnlyDictionary<string, ImageAsset> Assets, BattleMapFrame? Frame);
    private readonly Inputs _inputs;
    private readonly IReadOnlyDictionary<int, SettlementView> _settlements;
    private readonly Func<double> _positionTime;
    private MapPackage Map => _inputs.Map;
    private StrategySnapshot State => _inputs.State;
    public TurnDayPresentation? Day { get; }
    private BattleMapFrame? MapFrame { get; set; }
    internal void UpdateMapFrame(BattleMapFrame frame) => MapFrame = frame;
    private Dictionary<int, UnitVisualState> _unitStates => _inputs.Projection.Units;
    private IReadOnlyDictionary<int, UnitState> _runtimeUnits => _inputs.RuntimeUnits;
    private IReadOnlyDictionary<string, MapAnimationClip> Clips => _inputs.Clips;
    private IReadOnlyDictionary<string, ImageAsset> _assets => _inputs.Assets;

    public MapPlayback(Inputs inputs, MapPlayback? previous, Func<double> positionTime, TurnDayPresentation? day)
    {
        _inputs = inputs; _positionTime = positionTime; Day = day; MapFrame = inputs.Frame;
        _settlements = inputs.State.Settlements.ToDictionary(s => s.Id);
        _motions = Day?.Plan?.Motions ?? [];
        _attacks = Day?.Plan?.Attacks.ToArray() ?? [];
        PrepareHistory(previous);
        PrepareRetirements(previous);
        PrepareUnitPlayback(previous is not null && ReferenceEquals(previous.Map, Map) ? previous : null);
    }
}
