using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// Drives the six highlighted production controls through the real viewport
/// input route.  The test therefore catches both state-machine regressions and
/// tutorial overlays that accidentally swallow the highlighted click.
/// </summary>
public partial class BoomBattleTutorialSmoke : Node
{
    private int _checks;
    private bool _mouseNotified;

    public override void _Ready()
    {
        // Headless windows default to 64x64 even though the project viewport is
        // 1440x1080.  Give the spotlight the same 4:3 canvas as the game.
        GetWindow().Size = new Vector2I(1440, 1080);
        CallDeferred(MethodName.Run);
    }

    private async void Run()
    {
        try
        {
            BoomBattleTutorial tutorial = null;
            for (int frame = 0; frame < 180 && tutorial == null; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                tutorial = FindDescendant<BoomBattleTutorial>(GetTree().CurrentScene);
                if (tutorial != null && !tutorial.IsTutorialActive)
                    tutorial = null;
            }

            Check(tutorial != null, "Boom tutorial must activate in the demo encounter");
            if (tutorial == null) return;
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.LightCells,
                "tutorial starts at lighting cells");
            Check(tutorial.CurrentInputRect.IsEqualApprox(tutorial.CurrentTarget.GetGlobalRect()),
                "visual padding never expands the real input hole into neighbouring controls");

            await AdvanceStepByClicking(tutorial, BoomBattleTutorial.TutorialStep.LightCells, 8);
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.UseLitCards,
                "lighting every cell advances to card use");

            await AdvanceStepByClicking(tutorial, BoomBattleTutorial.TutorialStep.UseLitCards, 4);
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.MoveOnTrack,
                "using the fully lit card advances to movement");

            await AdvanceStepByClicking(tutorial, BoomBattleTutorial.TutorialStep.MoveOnTrack, 4);
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.SwitchEnemyPanel,
                "clicking the highlighted empty track cell moves the player");

            await AdvanceStepByClicking(tutorial, BoomBattleTutorial.TutorialStep.SwitchEnemyPanel, 4, 45);
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.InspectEnemy,
                "panel button reveals the enemy panel");

            await AdvanceStepByClicking(tutorial, BoomBattleTutorial.TutorialStep.InspectEnemy, 4);
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.EndTurn,
                "clicking Boom's track slot advances to end turn");

            await AdvanceStepByClicking(tutorial, BoomBattleTutorial.TutorialStep.EndTurn, 2, 6);
            Check(tutorial.CurrentTutorialStep == BoomBattleTutorial.TutorialStep.Complete,
                "the real end-turn button completes the tutorial after phase change");
            Check(!tutorial.IsTutorialActive && !tutorial.Visible,
                "completed tutorial releases the original battle UI");

            GD.Print($"BOOM_BATTLE_TUTORIAL_SMOKE_PASS checks={_checks} steps=6");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BOOM_BATTLE_TUTORIAL_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task AdvanceStepByClicking(
        BoomBattleTutorial tutorial,
        BoomBattleTutorial.TutorialStep expected,
        int attempts,
        int settleFrames = 8)
    {
        for (int attempt = 0;
             attempt < attempts && tutorial.CurrentTutorialStep == expected;
             attempt++)
        {
            await WaitFrames(3);
            Control target = tutorial.CurrentTarget;
            Check(target != null && GodotObject.IsInstanceValid(target),
                $"step {expected} exposes a real Control target");
            if (target == null || !GodotObject.IsInstanceValid(target)) return;
            await PushViewportClick(target);
            await WaitFrames(settleFrames);
        }
    }

    private async Task PushViewportClick(Control target)
    {
        Vector2 center = target.GetGlobalRect().GetCenter();
        Viewport viewport = GetViewport();
        if (!_mouseNotified)
        {
            viewport.NotifyMouseEntered();
            _mouseNotified = true;
        }
        viewport.PushInput(new InputEventMouseMotion
        {
            Position = center,
            GlobalPosition = center,
        }, true);
        viewport.UpdateMouseCursorState();
        await WaitFrames(2);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = center,
            GlobalPosition = center,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        }, true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = center,
            GlobalPosition = center,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        }, true);
    }

    private async Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T result = FindDescendant<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
