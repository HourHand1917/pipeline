using Godot;
using System;

public partial class MainMenu : Control
{
	private bool waitingForInput = true;
	[Export] private AnimationPlayer animationPlayer;
	[Export] private AudioStream backgroundMusic; // 添加背景音乐导出变量
	[Export] private float musicFadeDuration = 2.0f; // 淡入持续时间

	private AudioManager audioManager;

	public override void _Ready()
	{
		// 获取 AudioManager 实例
		audioManager = GetNode<AudioManager>("/root/AudioManager");
		
		// 如果有背景音乐，渐入播放
		if (backgroundMusic != null && audioManager != null)
		{
			audioManager.PlayMusicWithFade(backgroundMusic, musicFadeDuration);
		}
	}

	private async void HideAndShow(string first, string second)
	{
		animationPlayer.Play("hide_" + first);
		await ToSignal(animationPlayer, "animation_finished");
		animationPlayer.Play("show_" + second);
	}

	public override void _Input(InputEvent @event)
	{
		if (!waitingForInput) return;

		if ((@event is InputEventKey key && key.Pressed) ||
			(@event is InputEventMouseButton mouse && mouse.Pressed))
		{
			waitingForInput = false;
			HideAndShow("wait_lable", "menu");
		}
	}

	private void OnStartPressed()
	{
		// 可选：在切换场景前淡出音乐
		if (audioManager != null)
		{
			audioManager.FadeOutMusic(1.0f);
		}
		SceneTransition.Instance.ChangeScene("res://features/exploration/scenes/f1/f1_0.tscn");
	}

	private void OnLoadPressed()
	{
		GD.Print("Load Pressed");
	}

	private void OnCreditsPressed()
	{
		GD.Print("Credits Pressed");
	}

	private void OnExitPressed()
	{
		GetTree().Quit();
	}
}