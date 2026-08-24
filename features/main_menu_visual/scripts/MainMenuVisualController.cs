using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 主菜单纯表现层。运行时从三个独立序列目录创建 SpriteFrames，
/// 不接管按钮、存档或场景跳转逻辑。
/// </summary>
[GlobalClass]
public partial class MainMenuVisualController : Control
{
    [ExportGroup("图层")]
    [Export] public AnimatedSprite2D BaseLayer { get; set; }
    [Export] public AnimatedSprite2D RubberLayer { get; set; }
    [Export] public AnimatedSprite2D DesktopLayer { get; set; }

    [ExportGroup("序列目录")]
    [Export(PropertyHint.Dir)] public string BaseFramesDirectory { get; set; } =
        "res://features/main_menu_visual/art/base";
    [Export(PropertyHint.Dir)] public string RubberFramesDirectory { get; set; } =
        "res://features/main_menu_visual/art/rubber";
    [Export(PropertyHint.Dir)] public string DesktopFramesDirectory { get; set; } =
        "res://features/main_menu_visual/art/desktop";

    [ExportGroup("播放")]
    [Export(PropertyHint.Range, "1,60,1")] public float BaseFps { get; set; } = 24.0f;
    [Export(PropertyHint.Range, "1,60,1")] public float RubberFps { get; set; } = 24.0f;
    [Export(PropertyHint.Range, "1,60,1")] public float DesktopFps { get; set; } = 30.0f;
    [Export(PropertyHint.Range, "0,4,0.05")] public float IntroHoldSeconds { get; set; } = 0.45f;
    [Export(PropertyHint.Range, "0.05,3,0.05")] public float CrossFadeSeconds { get; set; } = 0.8f;

    public int BaseFrameCount { get; private set; }
    public int RubberFrameCount { get; private set; }
    public int DesktopFrameCount { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        BaseFrameCount = ConfigureLayer(BaseLayer, BaseFramesDirectory, BaseFps);
        RubberFrameCount = ConfigureLayer(RubberLayer, RubberFramesDirectory, RubberFps);
        DesktopFrameCount = ConfigureLayer(DesktopLayer, DesktopFramesDirectory, DesktopFps);

        if (BaseLayer != null)
        {
            BaseLayer.Modulate = Colors.White;
            BaseLayer.Play("loop");
        }
        if (RubberLayer != null)
        {
            RubberLayer.Modulate = new Color(1, 1, 1, BaseFrameCount > 0 ? 0 : 1);
            RubberLayer.Play("loop");
        }
        DesktopLayer?.Play("loop");

        if (BaseFrameCount > 0 && RubberFrameCount > 0)
            Callable.From(PlayIntroTransition).CallDeferred();
    }

    private async void PlayIntroTransition()
    {
        if (IntroHoldSeconds > 0)
            await ToSignal(GetTree().CreateTimer(IntroHoldSeconds), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(BaseLayer) || !IsInstanceValid(RubberLayer))
            return;

        Tween tween = CreateTween().SetParallel();
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(BaseLayer, "modulate:a", 0.0f, CrossFadeSeconds);
        tween.TweenProperty(RubberLayer, "modulate:a", 1.0f, CrossFadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
        if (IsInstanceValid(BaseLayer))
            BaseLayer.Visible = false;
    }

    private static int ConfigureLayer(
        AnimatedSprite2D layer,
        string directory,
        float fps)
    {
        if (layer == null || string.IsNullOrWhiteSpace(directory) || !DirAccess.DirExistsAbsolute(directory))
            return 0;

        var files = new List<string>();
        foreach (string file in DirAccess.GetFilesAt(directory))
        {
            if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                files.Add(file);
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        if (files.Count == 0)
            return 0;

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation("loop");
        frames.SetAnimationLoop("loop", true);
        frames.SetAnimationSpeed("loop", fps);
        foreach (string file in files)
        {
            Texture2D texture = GD.Load<Texture2D>($"{directory}/{file}");
            if (texture != null)
                frames.AddFrame("loop", texture);
        }

        layer.SpriteFrames = frames;
        layer.Animation = "loop";
        return frames.GetFrameCount("loop");
    }
}
