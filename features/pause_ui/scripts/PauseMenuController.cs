using Godot;
using System.Collections.Generic;

/// <summary>
/// Global, reusable pause machine. The scene is installed as an autoload so
/// gameplay scenes do not need to be modified individually.
/// </summary>
[GlobalClass]
public partial class PauseMenuController : CanvasLayer
{
    public enum AudioChannel
    {
        Ambient,
        Music,
        Sfx,
    }

    private const string MainMenuPath = "res://Scenes/game_scene/main_menu.tscn";
    private const string SettingsPath = "user://pause_audio_settings.cfg";
    private const string SettingsSection = "audio";
    private const float MachineWidth = 1428.0f;
    private const float MachineHeight = 1101.0f;

    private static readonly HashSet<string> ExcludedScenePaths = new()
    {
        MainMenuPath,
        "res://Scenes/game_scene/credits_screen.tscn",
        "res://Scenes/game_scene/end_screen.tscn",
        "res://features/exploration/f4_sequence/ending/f4_ending_screen.tscn",
    };

    [ExportGroup("Scene references")]
    [Export] private Control _backdrop;
    [Export] private Control _machine;
    [Export] private PauseVolumeSlider _ambientSlider;
    [Export] private PauseVolumeSlider _musicSlider;
    [Export] private PauseVolumeSlider _sfxSlider;
    [Export] private BaseButton _closeButton;
    [Export] private BaseButton _returnButton;

    [ExportGroup("Behaviour")]
    [Export] public bool PersistenceEnabled { get; set; } = true;
    [Export] public bool AllowInputInHeadlessTests { get; set; }
    [Export(PropertyHint.Range, "0.1,1.5,0.01")]
    public float EnterDurationSeconds { get; set; } = 0.62f;
    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float ExitDurationSeconds { get; set; } = 0.24f;

    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }

    private AudioManager _audioManager;
    private Tween _motionTween;
    private float _machineScale = 1.0f;
    private Vector2 _machineRestPosition;
    private bool _returningToMainMenu;

    public override void _Ready()
    {
        Layer = 900;
        ProcessMode = ProcessModeEnum.Always;
        _audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");

        _ambientSlider ??= GetNodeOrNull<PauseVolumeSlider>("Root/Machine/AmbientSlider");
        _musicSlider ??= GetNodeOrNull<PauseVolumeSlider>("Root/Machine/MusicSlider");
        _sfxSlider ??= GetNodeOrNull<PauseVolumeSlider>("Root/Machine/SfxSlider");
        _closeButton ??= GetNodeOrNull<BaseButton>("Root/Machine/CloseButton");
        _returnButton ??= GetNodeOrNull<BaseButton>("Root/Machine/ReturnButton");
        _backdrop ??= GetNodeOrNull<Control>("Root/Backdrop");
        _machine ??= GetNodeOrNull<Control>("Root/Machine");

        if (_ambientSlider == null || _musicSlider == null || _sfxSlider == null
            || _closeButton == null || _returnButton == null || _backdrop == null || _machine == null)
        {
            GD.PushError("PauseMenuController: scene references are incomplete.");
            SetProcessUnhandledInput(false);
            return;
        }

        _ambientSlider.ValueChanged += value => ApplyVolume(AudioChannel.Ambient, value);
        _musicSlider.ValueChanged += value => ApplyVolume(AudioChannel.Music, value);
        _sfxSlider.ValueChanged += value => ApplyVolume(AudioChannel.Sfx, value);
        _closeButton.Pressed += Close;
        _returnButton.Pressed += ReturnToMainMenu;
        _audioManager?.AttachUiSounds(_closeButton);
        _audioManager?.AttachUiSounds(_returnButton);

        if (PersistenceEnabled)
            LoadSettings();
        else
            ApplyAllVolumes();

        GetViewport().SizeChanged += LayoutForViewport;
        LayoutForViewport();
        Visible = false;
        _backdrop.Modulate = new Color(1, 1, 1, 0);
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null)
            GetViewport().SizeChanged -= LayoutForViewport;
        if (IsOpen && GetTree() != null)
            GetTree().Paused = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode == Key.F11)
        {
            ToggleFullscreen();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode != Key.Escape && !@event.IsActionPressed("ui_cancel"))
            return;
        if (DisplayServer.GetName() == "headless" && !AllowInputInHeadlessTests)
            return;

        if (IsOpen)
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (CanOpenForCurrentScene())
        {
            Open();
            GetViewport().SetInputAsHandled();
        }
    }

    public bool CanOpenForCurrentScene()
    {
        if (IsOpen || IsAnimating || _returningToMainMenu || GetTree().Paused)
            return false;

        Node currentScene = GetTree().CurrentScene;
        if (currentScene == null)
            return false;

        string scenePath = currentScene.SceneFilePath;
        if (ExcludedScenePaths.Contains(scenePath))
            return false;

        // Keep the dialogue canvas' own ESC-to-close behaviour. Without this
        // guard the same key could both end a conversation and open pause.
        Node dialogic = GetNodeOrNull<Node>("/root/Dialogic");
        if (dialogic != null && dialogic.Get("current_timeline").VariantType != Variant.Type.Nil)
            return false;

        return true;
    }

    private void ToggleFullscreen()
    {
        DisplayServer.WindowMode mode = DisplayServer.WindowGetMode();
        if (mode == DisplayServer.WindowMode.Fullscreen || mode == DisplayServer.WindowMode.ExclusiveFullscreen)
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        else
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
    }

    public static bool IsScenePathExcluded(string scenePath) =>
        ExcludedScenePaths.Contains(scenePath ?? string.Empty);

    public void Open()
    {
        if (IsOpen || IsAnimating || _returningToMainMenu)
            return;

        LayoutForViewport();
        IsOpen = true;
        IsAnimating = true;
        Visible = true;
        GetTree().Paused = true;

        _motionTween?.Kill();
        _machine.PivotOffset = new Vector2(MachineWidth * 0.5f, MachineHeight * 0.5f);
        _machine.Position = _machineRestPosition
            + new Vector2(-GetViewport().GetVisibleRect().Size.X * 0.72f,
                GetViewport().GetVisibleRect().Size.Y * 0.48f);
        _machine.Scale = Vector2.One * (_machineScale * 0.78f);
        _machine.Rotation = Mathf.DegToRad(-11.0f);
        _backdrop.Modulate = new Color(1, 1, 1, 0);

        float first = EnterDurationSeconds * 0.68f;
        float settle = EnterDurationSeconds * 0.32f;
        Vector2 overshoot = _machineRestPosition + new Vector2(18.0f, -10.0f);

        _motionTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        _motionTween.SetParallel(true);
        _motionTween.TweenProperty(_backdrop, "modulate:a", 1.0f, first);
        _motionTween.TweenProperty(_machine, "position", overshoot, first)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _motionTween.TweenProperty(_machine, "scale", Vector2.One * (_machineScale * 1.025f), first)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _motionTween.TweenProperty(_machine, "rotation", Mathf.DegToRad(2.0f), first)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _motionTween.Chain().SetParallel(true);
        _motionTween.TweenProperty(_machine, "position", _machineRestPosition, settle)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _motionTween.TweenProperty(_machine, "scale", Vector2.One * _machineScale, settle)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _motionTween.TweenProperty(_machine, "rotation", 0.0f, settle)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _motionTween.Finished += () =>
        {
            IsAnimating = false;
            _ambientSlider.GrabFocus();
        };
    }

    public void Close()
    {
        if (!IsOpen || _returningToMainMenu)
            return;

        IsAnimating = true;
        if (PersistenceEnabled)
            SaveSettings();

        _motionTween?.Kill();
        Vector2 target = _machineRestPosition
            + new Vector2(-GetViewport().GetVisibleRect().Size.X * 0.6f,
                GetViewport().GetVisibleRect().Size.Y * 0.42f);
        _motionTween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel(true);
        _motionTween.TweenProperty(_machine, "position", target, ExitDurationSeconds)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _motionTween.TweenProperty(_machine, "rotation", Mathf.DegToRad(-8.0f), ExitDurationSeconds);
        _motionTween.TweenProperty(_backdrop, "modulate:a", 0.0f, ExitDurationSeconds);
        _motionTween.Finished += FinishClose;
    }

    public float GetVolume(AudioChannel channel) => channel switch
    {
        AudioChannel.Ambient => _ambientSlider?.Value ?? 0.0f,
        AudioChannel.Music => _musicSlider?.Value ?? 0.0f,
        AudioChannel.Sfx => _sfxSlider?.Value ?? 0.0f,
        _ => 0.0f,
    };

    public void SetVolume(AudioChannel channel, float value)
    {
        PauseVolumeSlider slider = channel switch
        {
            AudioChannel.Ambient => _ambientSlider,
            AudioChannel.Music => _musicSlider,
            AudioChannel.Sfx => _sfxSlider,
            _ => null,
        };
        slider?.SetValue(value);
    }

    private void FinishClose()
    {
        Visible = false;
        IsOpen = false;
        IsAnimating = false;
        GetTree().Paused = false;
    }

    private void ReturnToMainMenu()
    {
        if (_returningToMainMenu)
            return;

        _returningToMainMenu = true;
        if (PersistenceEnabled)
            SaveSettings();
        _motionTween?.Kill();
        Visible = false;
        IsOpen = false;
        IsAnimating = false;
        GetTree().Paused = false;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.ChangeScene(MainMenuPath);
        else
            GetTree().ChangeSceneToFile(MainMenuPath);

        // The autoload survives the scene change; permit future sessions.
        GetTree().CreateTimer(0.1).Timeout += () => _returningToMainMenu = false;
    }

    private void LayoutForViewport()
    {
        if (_machine == null)
            return;

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0.0f || viewport.Y <= 0.0f)
            return;

        _machineScale = Mathf.Min(viewport.X / MachineWidth, viewport.Y / MachineHeight) * 0.94f;
        Vector2 scaledSize = new(MachineWidth * _machineScale, MachineHeight * _machineScale);
        _machineRestPosition = (viewport - scaledSize) * 0.5f;
        if (!IsOpen || !IsAnimating)
        {
            _machine.Position = _machineRestPosition;
            _machine.Scale = Vector2.One * _machineScale;
        }
    }

    private void LoadSettings()
    {
        ConfigFile config = new();
        if (config.Load(SettingsPath) != Error.Ok)
        {
            ApplyAllVolumes();
            return;
        }

        _ambientSlider.SetValue((float)config.GetValue(SettingsSection, "ambient", 0.8f), false);
        _musicSlider.SetValue((float)config.GetValue(SettingsSection, "music", 0.8f), false);
        _sfxSlider.SetValue((float)config.GetValue(SettingsSection, "sfx", 0.8f), false);
        ApplyAllVolumes();
    }

    private void SaveSettings()
    {
        ConfigFile config = new();
        config.SetValue(SettingsSection, "ambient", _ambientSlider.Value);
        config.SetValue(SettingsSection, "music", _musicSlider.Value);
        config.SetValue(SettingsSection, "sfx", _sfxSlider.Value);
        Error error = config.Save(SettingsPath);
        if (error != Error.Ok)
            GD.PushWarning($"PauseMenuController: could not save audio settings ({error}).");
    }

    private void ApplyAllVolumes()
    {
        ApplyVolume(AudioChannel.Ambient, _ambientSlider.Value);
        ApplyVolume(AudioChannel.Music, _musicSlider.Value);
        ApplyVolume(AudioChannel.Sfx, _sfxSlider.Value);
    }

    private void ApplyVolume(AudioChannel channel, float value)
    {
        AudioManager.Bus bus = channel switch
        {
            AudioChannel.Ambient => AudioManager.Bus.AMBIENT,
            AudioChannel.Music => AudioManager.Bus.MUSIC,
            AudioChannel.Sfx => AudioManager.Bus.SFX,
            _ => AudioManager.Bus.MASTER,
        };

        if (_audioManager != null)
        {
            _audioManager.SetBusVolume(bus, value);
            return;
        }

        string busName = channel switch
        {
            AudioChannel.Ambient => "Ambient",
            AudioChannel.Music => "Music",
            _ => "SFX",
        };
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex >= 0)
            AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(Mathf.Clamp(value, 0.0f, 1.0f)));
    }
}
