using Godot;

/// <summary>
/// 友好 NPC。玩家靠近后可点击互动（对话、交易等）。
/// </summary>
[GlobalClass]
public partial class FriendlyNPC : NPCBase
{
	[ExportGroup("对话后商店")]
	/// <summary>启用后，Dialogic Timeline 结束时打开已拖入的商店。</summary>
	[Export] public bool OpenShopAfterDialogue { get; set; } = false;
	[Export] public ShopUI ShopUI { get; set; }
	[Export] public ExplorationHUD Hud { get; set; }

	private bool _shopRequestedByDialogic;

	public override void _Ready()
	{
		base._Ready();
		if (ShopUI != null)
			ShopUI.Closed += OnShopClosed;
	}

	public override void _ExitTree()
	{
		if (ShopUI != null)
			ShopUI.Closed -= OnShopClosed;
		base._ExitTree();
	}

	public override void HandleInteract()
	{
		if (HasConfiguredDialogue)
		{
			TryStartConfiguredDialogue();
			return;
		}

		GD.Print($"与「{NpcName}」对话：{DefaultDialog}");
	}

	protected override void OnDialogueSignalReceived(Variant argument)
	{
		// Timeline 中加入 Dialogic Signal “open_shop”，可让分支决定是否在结束后开店。
		if ((argument.VariantType == Variant.Type.String ||
			 argument.VariantType == Variant.Type.StringName) &&
			argument.AsString().Equals("open_shop", System.StringComparison.OrdinalIgnoreCase))
			_shopRequestedByDialogic = true;
	}

	protected override void OnDialogueCompleted()
	{
		if (OpenShopAfterDialogue || _shopRequestedByDialogic)
			OpenConfiguredShop();
		_shopRequestedByDialogic = false;
	}

	protected override void OnDialogueCancelled()
	{
		_shopRequestedByDialogic = false;
	}

	private void OpenConfiguredShop()
	{
		if (ShopUI == null)
		{
			GD.PushWarning($"FriendlyNPC「{NpcName}」要求对话后开店，但尚未拖入 ShopUI。");
			return;
		}

		Hud?.HideHUD();
		ShopUI.Open();
	}

	private void OnShopClosed() => Hud?.ShowHUD();
}
