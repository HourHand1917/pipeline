using Godot;
using System;

public partial class MainMenu : Control
{
	private bool waitingForInput = true;
	[Export] private AnimationPlayer animationPlayer;
	public override void _Ready()
	{
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
