using Godot;

/// <summary>
/// 全局转场（CanvasLayer Autoload）。猫和老鼠式圆形转场（iris wipe）。
/// 遮罩平时隐藏，转场时显示并拦截点击。
///
/// 用法：
///   SceneTransition.Instance.ChangeScene("res://path/to/scene.tscn");
/// </summary>
[GlobalClass]
public partial class SceneTransition : CanvasLayer
{
    public static SceneTransition Instance { get; private set; }

    /// <summary>每个阶段（收缩/放大）的时长</summary>
    [Export] private float _duration = 1.5f;

    private ColorRect _overlay;
    private ShaderMaterial _material;
    private bool _busy;

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("SceneTransition: 重复实例化"); return; }
        Instance = this;
        Layer = 200;

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        var shader = GD.Load<Shader>("res://Shaders/circle_transition.gdshader");
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("radius", 1.5f);

        // 按实际视口宽高比修正，保证黑圈是正圆（4:3 下 1.333）
        var viewSize = GetViewport().GetVisibleRect().Size;
        if (viewSize.Y > 0f)
            _material.SetShaderParameter("aspect", viewSize.X / viewSize.Y);

        _overlay = new ColorRect
        {
            Material = _material,
            Visible = false,                        // 平时隐藏
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);
    }

    /// <summary>带转场切场景。</summary>
    public async void ChangeScene(string scenePath)
    {
        if (_busy) return;
        _busy = true;

        await IrisClose();
        GetTree().ChangeSceneToFile(scenePath);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await IrisOpen();

        _busy = false;
    }

    /// <summary>圈缩到中央，遮住画面并拦截点击。</summary>
    public async System.Threading.Tasks.Task IrisClose()
    {
        _overlay.Visible = true;
        _overlay.MouseFilter = Control.MouseFilterEnum.Stop;  // 拦截点击

        var tween = CreateTween();
        tween.TweenProperty(_material, "shader_parameter/radius", -0.1f, _duration)
            .From(1.1f)                                    // 显式起始值，消除残留状态
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);                  // 开始快、结束慢
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>圈放大露出画面，结束后隐藏遮罩。</summary>
    public async System.Threading.Tasks.Task IrisOpen()
    {
        var tween = CreateTween();
        tween.TweenProperty(_material, "shader_parameter/radius", 1.5f, _duration)
            .From(-0.1f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);                   // 开始慢、结束快
        await ToSignal(tween, Tween.SignalName.Finished);

        _overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        _overlay.Visible = false;
    }
}
