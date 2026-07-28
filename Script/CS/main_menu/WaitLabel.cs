using Godot;
using System;

public partial class WaitLabel : Label
{
	private Tween blinkTween;

	public override void _Ready()
	{
		StartBlinking();
	}

	/// <summary>
	/// 游戏打开时自动开始闪烁（Tween 循环）
	/// </summary>
	private void StartBlinking()
	{
		blinkTween = CreateTween();
		blinkTween.SetLoops(0); // 无限循环

		// 透明度在 1.0 → 0.3 之间往复
		blinkTween.TweenProperty(this, "modulate:a", 0.0f, 0.8f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		blinkTween.TweenProperty(this, "modulate:a", 1.0f, 0.8f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
	}

	/// <summary>
	/// 点击后由 AnimationPlayer 调用：淡出并隐藏
	/// </summary>
	public void FadeOutAndHide()
	{
		// 停止闪烁动画
		if (blinkTween != null && blinkTween.IsValid())
		{
			blinkTween.Kill();
		}

		Tween t = CreateTween();
		t.TweenProperty(this, "modulate:a", 0.0f, 0.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		t.TweenCallback(Callable.From(() => Hide()));
	}
}
