using Godot;

/// <summary>
/// 大门交互物。点击后打开地图选择 UI，并隐藏 HUD。
/// </summary>
[GlobalClass]
public partial class GateInteractable : InteractableBase
{
	[Export] public MapSelectUI MapSelectUI { get; set; }
	[Export] public ExplorationHUD Hud { get; set; }

	public override void _Ready()
	{
		base._Ready();
		if (MapSelectUI != null)
			MapSelectUI.Closed += () => Hud?.ShowHUD();
	}

	public override void HandleInteract()
	{
		Hud?.HideHUD();
		MapSelectUI?.Open();
	}
}
