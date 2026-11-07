using Godot;
using San9.Content.Interface;
using San9.Content.Text;
using San9.Application.Presentation;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Retained event messages and current pending commands, projected without evaluating messages on repaint.</summary>
public partial class ExecutionPanels : Control
{
    public TextureScope Textures { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public InterfaceSkin Skin { get; init; } = null!;
    private const int Travel = 214;
    private static readonly Rect2 Records = new(13, 564, 424, 208);
    private static readonly Rect2 Commands = new(738, 610, 272, 140);
    private readonly GameTextPresenter _text = new();
    private Control _recordsMotion = null!, _commandsMotion = null!;
    private Control _headers = null!;
    private readonly ExecutionCommandRowView[] _commandRows = new ExecutionCommandRowView[5];
    public MapMessageLog RecordsLog { get; init; } = null!;
    public event Action<MapCell>? LocationRequested;
    private long _revision = -1;
    private bool _expanded;
    private bool _covered;
    private int _offset = Travel;
    private double _elapsed;
    public bool Expanded => _expanded;
    public bool IsExpanded => _expanded && _offset == 0;
    public bool IsCollapsed => !_expanded && _offset == Travel;
    private bool ReportsVisible => RecordsLog.ReportMode != Settings.ProgressReportMode.Off;
    public void SetReportMode(Settings.ProgressReportMode mode)
    {
        RecordsLog.ReportMode = mode;
        _recordsDirty = true; QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var layout = GD.Load<PackedScene>("res://UI/Strategy/ExecutionPanelsLayout.tscn").Instantiate<Control>();
        AddChild(layout);
        _recordsMotion = layout.GetNode<Control>("%RecordsMotion");
        _commandsMotion = layout.GetNode<Control>("%CommandsMotion");
        _recordRows = new() { MouseFilter = MouseFilterEnum.Ignore };
        _recordsMotion.AddChild(_recordRows);
        for (int i = 0; i < 10; i++)
        {
            var row = new MapRecordRow { Textures = Textures, Skin = Skin, Visible = false,
                LocationRequested = cell => LocationRequested?.Invoke(cell) };
            _recordRows.AddChild(row); _availableRecords.Enqueue(row);
        }
        layout.Resized += QueueRedraw;
        _headers = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _commandsMotion.AddChild(_headers);
        for (int i = 0; i < _commandRows.Length; i++)
        {
            var row = new ExecutionCommandRowView { Textures = Textures, Skin = Skin, Visible = false };
            _commandsMotion.AddChild(row); _commandRows[i] = row;
        }
        int[] x = [28, 88, 124, 184];
        for (int i = 0; i < ExecutionPanelSkin.Columns.Count; i++)
        {
            var label = new Label { Text = Texts.Resolve(ExecutionPanelSkin.HeaderKey(ExecutionPanelSkin.Columns[i])),
                Position = Commands.Position + new Vector2(x[i], 18), Size = new(i == 3 ? 48 : 24, 12),
                MouseFilter = MouseFilterEnum.Ignore };
            _headers.AddChild(label);
            _text.Apply(label, GameTextStyles.CatalogRow with { FontSize = 12 });
        }
        ApplyOffset();
    }

    public bool Append(MapMessageRecord entry)
    {
        if (!RecordsLog.Append(entry)) return false;
        if (!RecordsLog.Allows(entry)) return false;
        _recordsDirty = true;
        return true;
    }

    public void ExpandImmediately()
    {
        _expanded = true; _offset = 0; _elapsed = 0;
        ApplyOffset();
    }

    private Control _recordRows = null!;
    private readonly Dictionary<string, MapRecordRow> _recordBindings = [];
    private readonly Queue<MapRecordRow> _availableRecords = new();
    private readonly List<string> _releasedRecords = [];
    private bool _recordsDirty = true;
    public override void _Process(double delta)
    {
        if (_recordsDirty) RefreshRecords();
    }
    private void RefreshRecords()
    {
        _recordsDirty = false; _recordRows.Visible = ReportsVisible;
        var visible = RecordsLog.Visible.ToArray();
        foreach (var pair in _recordBindings)
            if (!visible.Any(entry => entry.Id == pair.Key)) _releasedRecords.Add(pair.Key);
        foreach (string id in _releasedRecords)
        {
            var row = _recordBindings[id]; row.Release();
            _recordBindings.Remove(id); _availableRecords.Enqueue(row);
        }
        _releasedRecords.Clear();
        for (int i = 0; i < visible.Length; i++)
        {
            var entry = visible[i];
            if (!_recordBindings.TryGetValue(entry.Id, out var row))
            {
                row = _availableRecords.Dequeue(); _recordBindings.Add(entry.Id, row);
            }
            row.Bind(entry); row.Position = Records.Position + new Vector2(21, 21 + 16 * (10 - visible.Length + i)); row.Show();
        }
    }

    public void Bind(StrategySnapshot view)
    {
        if (_revision==view.World.Revision || !IsNodeReady()) return;
        _revision=view.World.Revision;
        var rows = ExecutionCommands.Read(view, Texts, _commandRows.Length);
        for (int i = 0; i < _commandRows.Length; i++)
        {
            var row = _commandRows[i];
            row.Visible = i < rows.Count;
            if (!row.Visible) continue;
            var value = rows[i];
            var person = view.World.Officers.Single(p => p.Id == value.OfficerId);
            var force = view.World.Forces.Single(f => f.Id == person.OwnerId);
            row.Bind(value, force.ColorCode);
            row.Position = Commands.Position + new Vector2(14, 35 + 16 * (5 - rows.Count + i));
        }
    }

    public void Advance(bool expanded, double delta, bool covered = false)
    {
        _covered = covered;
        Visible = _offset < Travel && !_covered;
        if (_expanded != expanded)
        {
            _expanded = expanded;
            _elapsed = 0;
            ApplyOffset();
            return;
        }
        int target = expanded ? 0 : Travel;
        if (_offset == target) return;
        _elapsed += delta;
        if (_elapsed < 0.1) return;
        int distance = (int)Math.Floor(_elapsed * Travel / 0.5);
        _elapsed = 0;
        _offset = expanded ? Math.Max(target, _offset - distance) : Math.Min(target, _offset + distance);
        ApplyOffset();
    }

    private void ApplyOffset()
    {
        Visible = _offset < Travel && !_covered;
        _recordsMotion.Position = _commandsMotion.Position = new(0, _offset);
        QueueRedraw();
    }

    private Vector2 RecordsOffset => GetGlobalTransform().AffineInverse() * _recordsMotion.GlobalPosition;
    private Vector2 CommandsOffset => GetGlobalTransform().AffineInverse() * _commandsMotion.GlobalPosition;

    public IEnumerable<Rect2> OccupiedAreas()
    {
        if (!IsVisibleInTree()) yield break;
        if (ReportsVisible) yield return new(Records.Position + RecordsOffset, Records.Size);
        yield return new(Commands.Position + CommandsOffset, Commands.Size);
    }

    public bool Covers(Vector2 point) => OccupiedAreas().Any(rect => rect.HasPoint(point));

    public override void _Draw()
    {
        if (ReportsVisible) DrawTexture(Textures.Get("turn-record-panel", this), Records.Position + RecordsOffset);
        DrawTexture(Textures.Get("turn-command-panel", this), Commands.Position + CommandsOffset);
    }

    public override void _ExitTree() => _text.Dispose();
}
