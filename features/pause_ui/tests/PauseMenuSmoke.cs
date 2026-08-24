using Godot;
using System;

/// <summary>Headless contract/runtime smoke test for the global pause machine.</summary>
public partial class PauseMenuSmoke : Node
{
    private int _checks;
    private int _failures;

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            PauseMenuController pause = GetNodeOrNull<PauseMenuController>("/root/PauseMenu");
            Check(pause != null, "pause scene is installed as an autoload");
            if (pause == null)
            {
                Finish();
                return;
            }

            pause.PersistenceEnabled = false;
            pause.EnterDurationSeconds = 0.02f;
            pause.ExitDurationSeconds = 0.01f;

            Check(!pause.Visible && !pause.IsOpen, "pause UI starts hidden");
            Check(pause.GetNodeOrNull<PauseVolumeSlider>("Root/Machine/AmbientSlider") != null,
                "environment volume slider exists");
            Check(pause.GetNodeOrNull<PauseVolumeSlider>("Root/Machine/MusicSlider") != null,
                "music volume slider exists");
            Check(pause.GetNodeOrNull<PauseVolumeSlider>("Root/Machine/SfxSlider") != null,
                "sound effect volume slider exists");
            Check(pause.GetNodeOrNull<Label>("Root/Machine/AmbientLabel")?.Text == "环境声",
                "environment row is labelled");
            Check(pause.GetNodeOrNull<Label>("Root/Machine/MusicLabel")?.Text == "音乐",
                "music row is labelled");
            Check(pause.GetNodeOrNull<Label>("Root/Machine/SfxLabel")?.Text == "音效",
                "sound effect row is labelled");
            Check(pause.GetNodeOrNull<BaseButton>("Root/Machine/ReturnButton") != null,
                "bottom return-to-main-menu button exists");
            Check(pause.GetNodeOrNull<BaseButton>("Root/Machine/CloseButton") != null,
                "painted close button exists");

            int ambientBus = AudioServer.GetBusIndex("Ambient");
            int musicBus = AudioServer.GetBusIndex("Music");
            int sfxBus = AudioServer.GetBusIndex("SFX");
            Check(ambientBus >= 0 && musicBus >= 0 && sfxBus >= 0,
                "three independent audio buses exist");
            Check(ambientBus != musicBus && ambientBus != sfxBus && musicBus != sfxBus,
                "environment, music and SFX use different buses");

            float oldAmbient = pause.GetVolume(PauseMenuController.AudioChannel.Ambient);
            float oldMusic = pause.GetVolume(PauseMenuController.AudioChannel.Music);
            float oldSfx = pause.GetVolume(PauseMenuController.AudioChannel.Sfx);
            pause.SetVolume(PauseMenuController.AudioChannel.Ambient, 0.31f);
            pause.SetVolume(PauseMenuController.AudioChannel.Music, 0.52f);
            pause.SetVolume(PauseMenuController.AudioChannel.Sfx, 0.73f);
            Check(DbMatchesLinear(ambientBus, 0.31f), "environment slider controls Ambient bus");
            Check(DbMatchesLinear(musicBus, 0.52f), "music slider controls Music bus");
            Check(DbMatchesLinear(sfxBus, 0.73f), "SFX slider controls SFX bus");

            Check(PauseMenuController.IsScenePathExcluded(
                    "res://Scenes/game_scene/main_menu.tscn"),
                "main menu is excluded from global pause");
            Check(PauseMenuController.IsScenePathExcluded(
                    "res://Scenes/game_scene/credits_screen.tscn"),
                "credits screen is excluded from global pause");
            Check(PauseMenuController.IsScenePathExcluded(
                    "res://Scenes/game_scene/end_screen.tscn"),
                "legacy ending is excluded from global pause");
            Check(PauseMenuController.IsScenePathExcluded(
                    "res://features/exploration/f4_sequence/ending/f4_ending_screen.tscn"),
                "Core-00 ending is excluded from global pause");

            pause.AllowInputInHeadlessTests = true;
            pause._UnhandledInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree().CreateTimer(0.05, true), SceneTreeTimer.SignalName.Timeout);
            Check(pause.Visible && pause.IsOpen, "ESC opens pause on a gameplay/test scene");
            Check(GetTree().Paused, "opening pause freezes the scene tree");

            pause._UnhandledInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            await ToSignal(GetTree().CreateTimer(0.04, true), SceneTreeTimer.SignalName.Timeout);
            Check(!pause.Visible && !pause.IsOpen, "second ESC closes pause");
            Check(!GetTree().Paused, "closing pause resumes the scene tree");

            pause.SetVolume(PauseMenuController.AudioChannel.Ambient, oldAmbient);
            pause.SetVolume(PauseMenuController.AudioChannel.Music, oldMusic);
            pause.SetVolume(PauseMenuController.AudioChannel.Sfx, oldSfx);
        }
        catch (Exception exception)
        {
            GD.PushError($"PAUSE_MENU_SMOKE_EXCEPTION: {exception}");
            _failures++;
            if (GetTree().Paused)
                GetTree().Paused = false;
        }

        Finish();
    }

    private static bool DbMatchesLinear(int busIndex, float expected)
    {
        if (busIndex < 0) return false;
        return Mathf.Abs(AudioServer.GetBusVolumeDb(busIndex) - Mathf.LinearToDb(expected)) < 0.05f;
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (condition)
            return;
        _failures++;
        GD.PushError($"PAUSE_MENU_SMOKE_FAIL: {message}");
    }

    private void Finish()
    {
        if (_failures == 0)
            GD.Print($"PAUSE_MENU_SMOKE_PASS checks={_checks}");
        else
            GD.PushError($"PAUSE_MENU_SMOKE_FAILED checks={_checks} failures={_failures}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
