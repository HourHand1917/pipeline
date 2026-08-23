using Godot;
using System;

public partial class F4EndingScreenTest : Node
{
    private int _checks;

    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            PackedScene packed = ResourceLoader.Load<PackedScene>(
                "res://features/exploration/f4_sequence/ending/f4_ending_screen.tscn");
            Check(packed != null, "ending scene loads");
            F4EndingScreen ending = packed.Instantiate<F4EndingScreen>();
            Check(Mathf.IsEqualApprox(ending.AfterCreditsDelaySeconds, 3.0f),
                "poster waits three seconds after the default credits finish");
            TextureRect poster = ending.GetNode<TextureRect>("PosterLayer/Poster");
            Check(poster.Texture != null && poster.Texture.GetSize().X > 1000,
                "the supplied Pipeline Record poster is embedded without stretching its source");
            ending.CreditsText = "测试字幕";
            ending.ScrollPixelsPerSecond = 100000.0f;
            ending.AfterCreditsDelaySeconds = 0.0f;
            ending.PosterFadeSeconds = 0.0f;
            ending.ExitApplicationOnInput = false;
            bool exitRequested = false;
            ending.ExitRequested += () => exitRequested = true;
            AddChild(ending);

            for (int i = 0; i < 12 && !ending.PosterVisible; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Check(ending.CreditsFinished, "rolling credits complete before poster");
            Check(ending.PosterVisible, "poster appears after credits and configured delay");
            Check(ending.PosterText.Text.Contains("黑匣子实验室"), "poster contains the team title");
            Check(ending.PosterText.Text.Contains("Popkey49") && ending.PosterText.Text.Contains("Josie"),
                "poster contains the configured credits");
            Check(ending.ExitHint.Text == "点击任意键退出", "poster shows the exit hint");

            ending._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Check(exitRequested, "any key emits exit only after the poster is visible");
            GD.Print($"F4_ENDING_SCREEN_TEST_PASS checks={_checks} configurable=1 poster=1 any_key=1");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"F4_ENDING_SCREEN_TEST_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
