using Godot;

[GlobalClass]
public partial class EnemyAnimator : Node
{
    [Export] private AnimationPlayer _animPlayer;

    public override void _Ready()
    {
        PlayIdle();
    }

    /// <summary>待机（循环）</summary>
    public void PlayIdle()
    {
        if (_animPlayer == null || !_animPlayer.HasAnimation("idle")) return;
        _animPlayer.Play("idle");
    }

    /// <summary>受伤（单次，播完回 idle）</summary>
    public void PlayHurt()
    {
        if (_animPlayer == null || !_animPlayer.HasAnimation("hurt")) return;
        _animPlayer.Play("hurt");
        _animPlayer.AnimationFinished += OnHurtFinished;
    }

    /// <summary>死亡（单次，播完停住）</summary>
    public void PlayDeath()
    {
        if (_animPlayer == null || !_animPlayer.HasAnimation("death")) return;
        _animPlayer.Play("death");
    }

    /// <summary>主动动作（单次，播完回 idle）</summary>
    public void PlayAction(string animName)
    {
        if (_animPlayer == null || string.IsNullOrEmpty(animName)) return;

        if (!_animPlayer.HasAnimation(animName))
        {
            PlayIdle();
            return;
        }

        _animPlayer.Play(animName);
        _animPlayer.AnimationFinished += OnActionFinished;
    }

    private void OnHurtFinished(StringName animName)
    {
        if (animName != "hurt") return;
        _animPlayer.AnimationFinished -= OnHurtFinished;
        PlayIdle();
    }

    private void OnActionFinished(StringName animName)
    {
        _animPlayer.AnimationFinished -= OnActionFinished;
        PlayIdle();
    }
}