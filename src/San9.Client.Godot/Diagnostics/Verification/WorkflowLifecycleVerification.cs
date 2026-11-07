using Godot;
using San9.Application.Presentation;
using San9.Core.Domain;

namespace San9.Client.Godot;

/// <summary>Asset-free engine checks for ownership, tree teardown and map callback replacement.</summary>
public partial class WorkflowLifecycleVerification : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private async void Run()
    {
        try
        {
            if (System.Environment.GetEnvironmentVariable("SAN9_UI_PREFERENCES_CONTENT") is { Length: > 0 } preferencesRoot)
            {
                await CheckUiPreferences(preferencesRoot);
                GetTree().Quit();
                return;
            }
            if (System.Environment.GetEnvironmentVariable("SAN9_OPENING_CONTENT") is { Length: > 0 } openingRoot)
            {
                await CheckOpeningControls(openingRoot);
                GetTree().Quit();
                return;
            }
            CheckWindows();
            CheckMapInputs();
            await CheckModals();
            CheckCompanionDraft();
            if (System.Environment.GetEnvironmentVariable("SAN9_MODAL_CONTENT") is { Length: > 0 } content) await CheckContentModals(content);
            GD.Print("WORKFLOW_LIFECYCLE_OK windows=normal,hidden,owner-first,host-first,failure inputs=replaced,repeated-release,unowned-exit modals=reused,nested,focus,raw-input,dynamic,teardown");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void CheckWindows()
    {
        var host = new StrategyOverlayHost(); AddChild(host);
        var locks = host.Workflows;
        using (var windows = new WorkflowWindows(host))
        {
            int released = 0;
            var first = new Control(); var second = new Control();
            windows.Show(first, () => released++, InteractionBlock.Orders | InteractionBlock.Playback);
            windows.Show(second, () => released++, InteractionBlock.Continuation);
            first.Hide();
            Check(locks.Blocked == (InteractionBlock.Orders | InteractionBlock.Playback | InteractionBlock.Continuation), "Hidden window lost its lock.");
            windows.Close(first); windows.Close(first);
            Check(released == 1 && locks.Blocked == InteractionBlock.Continuation, "Closing one window released another owner or completed twice.");
            windows.Dispose(); windows.Dispose();
            Check(released == 2 && windows.IsEmpty && locks.Blocked == InteractionBlock.None, "Owner-first teardown leaked a window or lock.");
        }
        host.Free();
        host = new(); AddChild(host); locks = host.Workflows;
        using (var windows = new WorkflowWindows(host))
        {
            int released = 0;
            windows.Show(new Control(), () => released++);
            host.Free();
            Check(released == 1 && windows.IsEmpty && locks.Blocked == InteractionBlock.None, "Host-first teardown retained an owner.");
        }
        host = new(); AddChild(host); locks = host.Workflows;
        try
        {
            using var windows = new WorkflowWindows(host);
            windows.Show(new Control());
            throw new InvalidDataException("injected preparation failure");
        }
        catch (InvalidDataException) { }
        Check(locks.Blocked == InteractionBlock.None && host.GetChildCount() == 0, "Failure cleanup leaked input restrictions.");
        host.Free();
    }
    private void CheckMapInputs()
    {
        var world = new MapWorld();
        Action<MapObjectTarget?> firstTarget = _ => { }, secondTarget = _ => { };
        Action<MapCell> firstCell = _ => { }, secondCell = _ => { };
        Func<MapObjectTarget, bool> firstAllowed = target => !target.IsUnit, secondAllowed = target => target.IsUnit;
        using var first = new MapTargetInputLease(world, firstTarget, firstCell, firstCell, firstAllowed);
        using var second = new MapTargetInputLease(world, secondTarget, secondCell, secondCell, secondAllowed);
        first.Dispose();
        Check(world.TargetPicked == secondTarget && world.TargetAllowed == secondAllowed && world.WaypointPicked == secondCell && world.SitePicked == secondCell,
            "Old workflow erased replacement callbacks.");
        var military = new MilitaryCommandFlow { World = world }; AddChild(military); military.Free();
        var targets = new CommandTargetFlow { World = world }; AddChild(targets); targets.Free();
        Check(world.TargetPicked == secondTarget && world.WaypointPicked == secondCell && world.SitePicked == secondCell,
            "Inactive workflow exit erased another workflow's input.");
        CheckObjectPicker(world, secondTarget);
        var oldOwner = world.TargetInputOwner;
        var pending = new MapObjectPickRequest([new(1, true)], _ => true,
            () => ReferenceEquals(world.TargetInputOwner, oldOwner), _ => throw new InvalidOperationException("Stale picker submitted."));
        using var replacement = new MapTargetInputLease(world, secondTarget, secondCell, secondCell, firstAllowed);
        second.Dispose(); second.Dispose();
        Check(world.TargetPicked == secondTarget && world.TargetAllowed == firstAllowed && !pending.IsCurrent,
            "Rebinding the same callback retained an old click or lost its replacement input.");
        replacement.Dispose();
        Check(world.TargetPicked is null && world.TargetAllowed is null && world.WaypointPicked is null && world.SitePicked is null, "Active input lease did not release.");
        world.Free();
    }

    private void CheckObjectPicker(MapWorld world, Action<MapObjectTarget?> targetInput)
    {
        MapObjectTarget? preview = null, picked = null;
        var picker = new MapObjectPicker { Preview = target => preview = target };
        AddChild(picker);
        var choices = Enumerable.Range(1, 8).Select(id =>
            new MapObjectChoice(new(id, true), $"Unit {id}", Colors.Blue)).ToArray();
        MapObjectPickRequest Request() => new(choices.Select(row => row.Target), _ => true,
            () => world.TargetPicked == targetInput, target =>
            {
                Check(!picker.Visible && preview is null, "Selection ran before dismissing the object picker.");
                picked = target;
            });
        var request = Request();
        picker.Open(request, choices, new(100, 100));
        var rows = picker.GetChild<ScrollContainer>(0).GetChild<VBoxContainer>(0);
        var seventh = rows.GetChild<InformationMenuButton>(6);
        seventh.EmitSignal(Control.SignalName.MouseEntered);
        Check(preview == choices[6].Target, "Object row did not preview its map identity.");
        seventh.EmitSignal(BaseButton.SignalName.Pressed);
        Check(picked == choices[6].Target && !request.IsCurrent, "More than five overlapping objects were not selectable.");

        picked = null; request = Request();
        picker.Open(request, choices, new(100, 100));
        using var outside = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = new(-1, -1) };
        picker._GuiInput(outside);
        Check(!picker.Visible && !request.IsCurrent && picked is null && world.TargetPicked == targetInput,
            "Outside dismissal submitted or cancelled the underlying target workflow.");

        request = Request(); picker.Open(request, choices, new(100, 100));
        using var openingRelease = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false };
        picker._Input(openingRelease);
        Check(picker.Visible, "Opening right-button release dismissed the object picker.");
        using var rightPress = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
        picker._Input(rightPress); picker._Input(openingRelease);
        Check(!picker.Visible && picked is null && world.TargetPicked == targetInput, "Right cancellation leaked into target selection.");

        request = Request(); picker.Open(request, choices, new(100, 100));
        using var escape = new InputEventKey { Pressed = true, Keycode = Key.Escape };
        picker._Input(escape);
        Check(!picker.Visible && !request.IsCurrent, "Escape left the object selection active.");
        picker.Free();
    }
}
