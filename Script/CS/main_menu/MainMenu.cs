using Godot;
using System;

public partial class MainMenu : Control
{
	private bool waitingForInput = true;
	
	[Export] private AnimationPlayer animationPlayer;
	
	[ExportGroup("音乐")]
	[Export] private AudioStream backgroundMusic;
	[Export] private float initialMusicVolume = 0.4f; // 等待界面时的音乐音量
	[Export] private float fullMusicVolume = 1.0f;    // 菜单界面时的音乐音量
	[Export] private float musicFadeDuration = 1.5f;  // 音乐切换过渡时间
	
	private AudioManager _audioManager;

	public override void _Ready()
	{
		_audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
		
		// 播放背景音乐，初始音量较小
		if (backgroundMusic != null && _audioManager != null)
		{
			_audioManager.PlayMusic(backgroundMusic);
			_audioManager.SetBusVolume(AudioManager.Bus.MUSIC, initialMusicVolume);
		}
		
		// 为所有按钮添加 UI 音效
		AttachButtonSounds();
	}

	/// <summary>
	/// 递归查找所有按钮并添加音效
	/// </summary>
	private void AttachButtonSounds()
	{
		if (_audioManager == null) return;
		AttachSoundsRecursive(this);
	}

	private void AttachSoundsRecursive(Node node)
	{
		if (node is BaseButton button)
		{
			_audioManager.AttachUiSounds(button);
		}
		
		foreach (Node child in node.GetChildren())
		{
			AttachSoundsRecursive(child);
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
			
			// 点击后调大音乐音量
			FadeMusicVolume(initialMusicVolume, fullMusicVolume);
			
			// 播放 UI 点击音效
			_audioManager?.PlayUiClick();
			
			HideAndShow("wait_lable", "menu");
		}
	}

	/// <summary>
	/// 平滑过渡音乐音量
	/// </summary>
	private async void FadeMusicVolume(float from, float to)
	{
		if (_audioManager == null) return;
		
		float elapsed = 0f;
		while (elapsed < musicFadeDuration)
		{
			elapsed += (float)GetProcessDeltaTime();
			float t = Mathf.Clamp(elapsed / musicFadeDuration, 0f, 1f);
			float volume = Mathf.Lerp(from, to, t);
			_audioManager.SetBusVolume(AudioManager.Bus.MUSIC, volume);
			await ToSignal(GetTree(), "process_frame");
		}
		
		// 确保最终音量准确
		_audioManager.SetBusVolume(AudioManager.Bus.MUSIC, to);
	}

	private void OnStartPressed()
	{
		SaveManager.Instance?.NewGame();
		SceneTransition.Instance.ChangeScene("res://features/exploration/scenes/f1/f1_0.tscn");
	}

	private void OnLoadPressed()
	{
		if (SaveManager.Instance == null || !SaveManager.Instance.Load())
			GD.Print("没有存档，无法加载。");
	}

	private void OnCreditsPressed()
	{
		SceneTransition.Instance.ChangeScene("res://Scenes/game_scene/credits_screen.tscn");
	}

	private void OnExitPressed()
	{
		GetTree().Quit();
	}
}