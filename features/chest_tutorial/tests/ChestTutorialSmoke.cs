using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Exercises the three tutorial steps against the production f1_1 chest and
/// RewardPage. The first step uses the real viewport mouse route; the second
/// clicks a real loot slot and watches DataManager; the third uses the real
/// close button.
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

            // The online scene may move its initial player spawn independently
            // of this tutorial. Put the real player inside the real detection
            // range so this remains an input-chain test, not a layout test.
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.GlobalPosition = chest.GlobalPosition;
                await WaitFrames(4);
            }

            // Move the mouse into the real click shape before sending the
            // click so BlinkComponent/InteractableBase take their normal path.
            await PushViewportClick(tutorial.CurrentInputRect.GetCenter());
            // Online RewardPage enters through a 0.5 second animation. Wait
            // until it settles before validating step 2.
            await WaitFrames(36);
            Check(chest.IsOpened, "viewport click opens the real chest");
            Check(tutorial.CurrentTutorialStep == ChestTutorial.Step.CollectLoot,
                "opening the chest advances to the collect step");
            Check(tutorial.CurrentTargetNode == reward,
                "second spotlight targets the whole RewardPage list");
            Check(tutorial.CurrentInputRect.HasArea(),
                "reward page stays clickable through the spotlight");

            // Step 2: claim the first loot slot through the real viewport
            // route; the tutorial watches DataManager for the backpack gain.
            await WaitFrames(32);
            BaseButton loot = FindFirstLootButton(reward);
            Check(loot != null, "reward page exposes a loot slot");
            Check(!loot.Disabled, "loot slot is clickable during the collect step");
            Check(tutorial.CurrentInputRect.Encloses(loot.GetGlobalRect()),
                "loot slot lies inside the tightened step-2 spotlight");
            await PushViewportClick(loot.GetGlobalRect().GetCenter());
            await WaitFrames(6);
            Check(tutorial.CurrentTutorialStep == ChestTutorial.Step.CloseChest,
                "claiming loot advances to the close step");
            Check(tutorial.CurrentTargetNode is BaseButton,
                "third spotlight targets the real RewardPage close button");
            Check(tutorial.CurrentInputRect.HasArea(),
                "close button remains clickable through the spotlight");

            // Step 3: the tutorial follows the moving button every frame, but
            // users click it after it reaches its resting position.
            await WaitFrames(32);
            await PushViewportClick(tutorial.CurrentInputRect.GetCenter());
            await WaitFrames(12);
            Check(tutorial.CurrentTutorialStep == ChestTutorial.Step.Complete,
                "closing the RewardPage completes the tutorial");
            Check(!tutorial.IsTutorialActive && !tutorial.Visible,
                "completion removes the tutorial overlay");
            Check(PlayerController.Instance == null || PlayerController.Instance.MovementEnabled,
                "completion releases player movement");

            GD.Print($"CHEST_TUTORIAL_SMOKE_PASS checks={_checks} steps=3");
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

    /// <summary>战利品槽都在 RewardPage/LootList 里，取第一个可点击的槽位按钮。</summary>
    private static BaseButton FindFirstLootButton(RewardPage reward)
    {
        Control list = reward.GetNodeOrNull<Control>("LootList");
        return list == null ? null : FindDescendant<BaseButton>(list);
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
