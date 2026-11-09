using System.Collections.Immutable;
using Godot;
using San9.Application.Presentation;
using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Edits member equipment and soldier counts within the deployment draft.</summary>
public partial class DeploymentDetachmentsView : Control
{
    public DeploymentDraft Draft { get; init; } = null!;
    public TextCatalog Texts { get; init; } = null!;
    public Action Closed { get; init; } = null!;
    private readonly Dictionary<int, (OptionButton Types, SpinBox Soldiers, List<(int Id, int Level)> Choices)> _rows = [];
    private Label _message = null!;
    private OptionButton? _navalTraining;
    private OptionButton _siege = null!;
    private readonly List<int?> _siegeChoices = [null];

    public override void _Ready()
    {
        Position = Vector2.Zero;
        Size = new(1024, 768);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = CompanionPanelStyle.Create(GetThemeFont("font"));
        var shade = new ColorRect { Color = new Color(0, 0, 0, .75f), Size = Size, MouseFilter = MouseFilterEnum.Stop };
        AddChild(shade);
        var panel = new PanelContainer { Position = new(220, 140), Size = new(584, 480) };
        AddChild(panel);
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 12);
        panel.AddChild(margin);
        var layout = new VBoxContainer();
        margin.AddChild(layout);
        layout.AddChild(new Label { Text = "分队兵种", HorizontalAlignment = HorizontalAlignment.Center });
        layout.AddChild(new Label { Text = $"分队兵力合计须为 {Draft.Soldiers}" });
        if (Draft.NavalFormationId is not null)
        {
            _navalTraining = new OptionButton();
            foreach (var training in Draft.Rules.NavalTraining.Values.OrderBy(t => t.Id)
                .Where(t => t.ResearchProjectId is null || Draft.Force.CompletedResearch.Contains(t.ResearchProjectId)))
                _navalTraining.AddItem($"{training.Name}（费用 {training.DeploymentCost}）", training.Id);
            for (int i = 0; i < _navalTraining.ItemCount; i++)
                if (_navalTraining.GetItemId(i) == Draft.NavalTrainingId) _navalTraining.Select(i);
            layout.AddChild(_navalTraining);
        }
        _siege = new OptionButton();
        _siege.AddItem("无攻城器械");
        foreach (var equipment in Draft.Rules.SiegeEquipment.Values.OrderBy(item => item.Id)
            .Where(item => Draft.Force.CompletedResearch.Contains(item.ResearchProjectId)))
        {
            _siegeChoices.Add(equipment.Id);
            _siege.AddItem($"{equipment.Name}（费用 {equipment.DeploymentCost}）");
        }
        _siege.Select(_siegeChoices.IndexOf(Draft.SiegeEquipmentId));
        layout.AddChild(_siege);
        foreach (int officerId in Draft.Members)
        {
            var row = new HBoxContainer();
            layout.AddChild(row);
            string name = Texts.Resolve(Draft.View.Officers.Single(person => person.Id == officerId).NameKey);
            row.AddChild(new Label { Text = name, CustomMinimumSize = new(110, 36) });
            var types = new OptionButton { CustomMinimumSize = new(220, 36) };
            row.AddChild(types);
            var choices = new List<(int Id, int Level)>();
            foreach (var type in Draft.Rules.TroopTypes.Values.OrderBy(type => type.Id))
                for (int level = 1; level <= type.MaximumLevel; level++)
                    if (type.LevelResearchProjects.All(project => project.Key > level || Draft.Force.CompletedResearch.Contains(project.Value)))
                    {
                        choices.Add((type.Id, level));
                        types.AddItem($"{type.Name}·{level}级");
                    }
            var current = Draft.Detachments[officerId];
            types.Select(choices.FindIndex(choice => choice.Id == current.TroopTypeId && choice.Level == current.Level));
            var count = new SpinBox { MinValue = 1, MaxValue = Draft.Soldiers, Step = 1, Value = current.Soldiers,
                CustomMinimumSize = new(150, 36), AllowGreater = false, AllowLesser = false };
            row.AddChild(count);
            _rows.Add(officerId, (types, count, choices));
        }
        _message = new Label { Text = "", CustomMinimumSize = new(0, 36) };
        layout.AddChild(_message);
        var buttons = new HBoxContainer();
        layout.AddChild(buttons);
        var accept = new Button { Text = "确定" };
        accept.Pressed += Accept;
        buttons.AddChild(accept);
        var cancel = new Button { Text = "取消" };
        cancel.Pressed += () => Closed();
        buttons.AddChild(cancel);
    }

    private void Accept()
    {
        if (_siege.Selected < 0 || !Draft.CanUseSiegeEquipment(_siegeChoices[_siege.Selected]))
        {
            _message.Text = "攻城器械需要已解锁且有相应兵法的武将。";
            return;
        }
        if (_navalTraining is not null && (_navalTraining.Selected < 0 ||
            !Draft.Rules.NavalTraining.TryGetValue(_navalTraining.GetSelectedId(), out var training) ||
            training.ResearchProjectId is string project && !Draft.Force.CompletedResearch.Contains(project)))
        {
            _message.Text = "请选择已解锁的水军训练。";
            return;
        }
        var result = ImmutableDictionary.CreateBuilder<int, DetachmentState>();
        foreach (var (officer, row) in _rows)
        {
            if (row.Types.Selected < 0) { _message.Text = "请选择兵种。"; return; }
            var choice = row.Choices[row.Types.Selected];
            result.Add(officer, new(choice.Id, choice.Level, (uint)row.Soldiers.Value));
        }
        if (!Draft.SetDetachments(result.ToImmutable()))
        {
            _message.Text = $"请检查兵种和兵力，合计须为 {Draft.Soldiers}。";
            return;
        }
        if (_navalTraining is not null && !Draft.SetNavalTraining(_navalTraining.GetSelectedId()))
        {
            _message.Text = "请选择已解锁的水军训练。";
            return;
        }
        if (_siege.Selected < 0 || !Draft.SetSiegeEquipment(_siegeChoices[_siege.Selected]))
        {
            _message.Text = "攻城器械需要已解锁且有相应兵法的武将。";
            return;
        }
        Closed();
    }
}
