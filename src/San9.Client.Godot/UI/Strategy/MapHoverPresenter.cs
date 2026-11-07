using Godot;
using San9.Application.Presentation;
using San9.Content.Text;

namespace San9.Client.Godot;

/// <summary>Owns map highlights, information cards and region feedback as one presentation.</summary>
public sealed class MapHoverPresenter(MapWorld world, TextCatalog texts, MapHoverCard card,
    IReadOnlyList<Label> regionLabels, Label regionName, Label cityName)
{
    private MapObjectTarget? _information;
    private StrategySnapshot? _view;
    private MapInformationContext _context;
    public bool RegionVisible { get; private set; }

    public void Clear()
    {
        world.HoverObject(null);
        card.ShowInfo(null, Vector2.Zero);
        _information = null;
        _view = null;
        SetRegionVisible(false);
    }

    public void Apply(MapHoverFeedback feedback, StrategySnapshot view, MapInformationContext context, Vector2 anchor)
    {
        world.HoverObject(feedback.Highlight);
        if (_information != feedback.Information || !ReferenceEquals(_view, view) || _context != context)
        {
            if (_information != feedback.Information) card.ShowInfo(null, anchor);
            _information = feedback.Information;
            _view = view;
            _context = context;
            card.ShowInfo(feedback.Information is { } target
                ? target.IsUnit ? UnitMapInteraction.Hover(view, texts, target.Id, context)
                    : MapHoverInfo.Resolve(view, texts, target.Id, context)
                : null, anchor);
        }
        if (feedback.Region is { } cell && world.Package is { } package)
        {
            var info = RegionHoverInfo.Resolve(package, cell, view, texts);
            regionName.Text = info.Region;
            cityName.Text = info.City;
            SetRegionVisible(true);
        }
        else SetRegionVisible(false);
    }

    private void SetRegionVisible(bool visible)
    {
        RegionVisible = visible;
        foreach (var label in regionLabels) label.Visible = visible;
    }
}
