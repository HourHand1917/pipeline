using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Exercises the two tutorial steps against the production f1_1 chest and
/// RewardPage. The first step uses the real viewport mouse route; the second
/// uses the real close button.
/// </summary>
public partial class ChestTutorialSmoke : Node
{
    private int _checks;
    private bool _mouseNotified;

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1440, 1080);
        CallDeferred(MethodName.Run);
    }

    private async void Run()
    {
        try
        {
            PackedScene packed = ResourceLoader.Load<PackedScene>(
                "res://features/exploration/scenes/f1/f1_1.tscn");
            Check(packed != null, "production f1_1 scene loads");
            Node level = packed.Instantiate();
            AddChild(level);
            await WaitFrames(12);

            ChestTutorial tutorial = FindDescendant<ChestTutorial>(level);
            ChestInteractable chest = FindDescendant<ChestInteractable>(level);
            RewardPage reward = FindDescendant<RewardPage>(level);
            Check(tutorial != null && chest != null && reward != null,
                "production scene exposes tutorial, chest and RewardPage");

            tutorial.ShowOnlyOnce = false;
            tutorial.ForceStartForTesting();
            await WaitFrames(3);
            Check(tutorial.IsTutorialActive, "tutorial starts");
            Check(tutorial.CurrentTutorialStep == ChestTutorial.Step.ClickChest,
                "first step teaches clicking the chest");
            Check(tutorial.CurrentTargetNode is CollisionShape2D,
                "first spotlight targets the real chest click shape");
            Check(tutorial.CurrentInputRect.HasArea(),
                "first spotlight exposes a non-empty input hole");

            // f1_1 starts the player inside the chest detection radius. Move
            // the mouse into the real click shape before sending the click so
            // BlinkComponent/InteractableBase take their normal path.
            await PushViewportClick(tutorial.CurrentInputRect.GetCenter());
            await WaitFrames(12);
            Check(chest.IsOpened, "viewport click opens the real chest");
            Check(tutorial.CurrentTutorialStep == ChestTutorial.Step.CloseChest,
                "opening the chest advances to the close step");
            Check(tutorial.CurrentTargetNode is Button,
                "second spotlight targets the real RewardPage close button");
            Check(tutorial.CurrentInputRect.HasArea(),
                "close button remains clickable through the spotlight");

            // Let the 0.5 second RewardPage show animation settle before the
            // viewport hit test; the tutorial follows the moving button every
            // frame, but users click it after it reaches its resting position.
            await WaitFrames(32);
            await PushViewportClick(tutorial.CurrentInputRect.GetCenter());
            await WaitFrames(12);
            Check(tutorial.CurrentTutorialStep == ChestTutorial.Step.Complete,
                "closing the RewardPage completes the tutorial");
            Check(!tutorial.IsTutorialActive && !tutorial.Visible,
                "completion removes the tutorial overlay");
            Check(PlayerController.Instance == null || PlayerController.Instance.MovementEnabled,
                "completion releases player movement");

            GD.Print($"CHEST_TUTORIAL_SMOKE_PASS checks={_checks} steps=2");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHEST_TUTORIAL_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task PushViewportClick(Vector2 position)
    {
        Viewport viewport = GetViewport();
        if (!_mouseNotified)
        {
            viewport.NotifyMouseEntered();
            _mouseNotified = true;
        }
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = position,
            GlobalPosition = position,
        }, true);
        viewport.UpdateMouseCursorState();
        await WaitFrames(2);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        }, true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        }, true);
    }

    private async Task WaitFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
