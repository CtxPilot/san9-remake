using Godot;
using San9.Application.Presentation;

namespace San9.Client.Godot;

public partial class StrategyHud
{
    private MapObjectPicker _objectPicker = null!;

    private bool IsCommandSource(MapObjectTarget target) =>
        (target.IsUnit ? MapCommandMenu.Unit(Scenario, target.Id) : MapCommandMenu.Settlement(Scenario, target.Id)).Count > 0;

    private bool HasObjectContext(MapObjectTarget target) =>
        (target.IsUnit ? UnitMapInteraction.Menu(Scenario, target.Id) : SettlementContextMenu.Read(Scenario, target.Id)).Count > 0;

    private void BuildObjectPicker()
    {
        _objectPicker = new() { Textures = _textures, Preview = World.HoverObject };
        OverlayHost.AddChild(_objectPicker);
        World.ObjectChoiceRequested += ShowObjectChoices;
        World.CommandSourceAllowed = IsCommandSource;
        World.ContextAllowed = HasObjectContext;
    }

    private void ShowObjectChoices(MapObjectPickRequest request, Vector2 point)
    {
        var view = Scenario;
        List<MapObjectChoice> choices = [];
        foreach (var target in request.Candidates)
        {
            string name;
            int? owner;
            if (target.IsUnit)
            {
                var unit = view.World.Units.Single(u => u.Id == target.Id);
                name = view.Officers.FirstOrDefault(o => o.Id == unit.CommanderId) is { } commander
                    ? Texts.Resolve(commander.NameKey) + "队" : "部队";
                owner = unit.OwnerId;
            }
            else
            {
                var place = view.Settlements.Single(s => s.Id == target.Id);
                name = place.ResolveName(Texts); owner = place.OwnerId;
            }
            int color = view.Forces.FirstOrDefault(f => f.Id == owner)?.ColorCode ?? 50;
            choices.Add(new(target, name, new Color(Skin.Colors[$"overview-force-{color}"])));
        }
        _hoverCard.ShowInfo(null, point); _hoverTarget = null;
        _objectPicker.Open(request, choices, point);
    }
}
