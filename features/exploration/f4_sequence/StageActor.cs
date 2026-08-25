using Godot;

/// <summary>
/// 舞台角色（挂在 AnimatedSprite2D 上）。
/// 供 AnimationPlayer 方法轨道调用：先播一段进入动画（一次），
/// 播完自动循环播放指定的空闲动画。
/// 依赖：进入动画在 SpriteFrames 里设为「不循环」，空闲动画设为「循环」。
/// </summary>
[GlobalClass]
public partial class StageActor : AnimatedSprite2D
{
    private string _pendingIdleAnimation = "";

    public override void _Ready()
    {
        AnimationFinished += OnAnimationFinished;
    }

    public override void _ExitTree()
    {
        AnimationFinished -= OnAnimationFinished;
    }

    /// <summary>先播 enterAnimation 一次，播完后循环 idleAnimation。显隐由 AnimationPlayer 控制。</summary>
    public void PlayEnterThenIdle(string enterAnimation, string idleAnimation)
    {
        _pendingIdleAnimation = idleAnimation;
        Play(enterAnimation);
    }

    private void OnAnimationFinished()
    {
        if (string.IsNullOrEmpty(_pendingIdleAnimation))
            return;

        string idle = _pendingIdleAnimation;
        _pendingIdleAnimation = "";
        Play(idle);
    }
}
