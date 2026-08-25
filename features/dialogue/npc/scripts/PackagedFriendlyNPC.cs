using Godot;

/// <summary>
/// features/dialogue/npc 专用的可拖拽友好 NPC。
/// 先进行内存角色注册，再加载默认 Timeline，避免要求修改 project.godot。
/// </summary>
[GlobalClass]
public partial class PackagedFriendlyNPC : FriendlyNPC
{
	[ExportGroup("封装对话内容")]
	[Export(PropertyHint.File, "*.dtl")]
	public string DefaultTimelinePath { get; set; } = "";

	[Export]
	public StringName DialogicCharacterIdentifier { get; set; } = "";

	[Export]
	public string[] ExtraDialogicCharacterIdentifiers { get; set; } = System.Array.Empty<string>();

	[Export]
	public Godot.Collections.Array<Resource> ExtraDialogicCharacters { get; set; } = new();

	[ExportGroup("卡牌奖励")]
	[Export]
	public Godot.Collections.Array<Resource> RewardCards { get; set; } = new();

	public override void _Ready()
	{
		PackagedDialogueRegistration.RegisterConfiguredCharacters(this);
		PackagedDialogueRegistration.Register(DialogicCharacterIdentifier, DialogueCharacter);
		PackagedDialogueRegistration.RegisterExtra(
			ExtraDialogicCharacterIdentifiers,
			ExtraDialogicCharacters);

		if (DialogueTimeline == null && !string.IsNullOrWhiteSpace(DefaultTimelinePath))
			DialogueTimeline = GD.Load<Resource>(DefaultTimelinePath);

		base._Ready();
	}

	/// <summary>
	/// 处理 Dialogic Timeline 中的信号
	/// </summary>
	protected override void OnDialogueSignalReceived(Variant argument)
	{
		base.OnDialogueSignalReceived(argument);
		
		if (argument.VariantType != Variant.Type.String &&
			argument.VariantType != Variant.Type.StringName)
			return;

		string signalName = argument.AsString();
		
		switch (signalName)
		{
			case "grant_all_cards":
				GrantAllRewardCards();
				break;
			case "grant_card_0":
				GrantRewardCardByIndex(0);
				break;
			case "grant_card_1":
				GrantRewardCardByIndex(1);
				break;
			case "grant_card_2":
				GrantRewardCardByIndex(2);
				break;
			case "grant_card_3":
				GrantRewardCardByIndex(3);
				break;
		}
	}

	/// <summary>
	/// 发放所有奖励卡牌
	/// </summary>
	public int GrantAllRewardCards()
	{
		if (RewardCards == null || RewardCards.Count == 0)
		{
			GD.Print("PackagedFriendlyNPC: 没有配置奖励卡牌");
			return 0;
		}

		DataManager data = DataManager.Instance;
		if (data == null)
		{
			GD.PushError("PackagedFriendlyNPC: DataManager 不可用");
			return 0;
		}

		int grantedCount = 0;
		foreach (var cardResource in RewardCards)
		{
			if (cardResource == null)
			{
				GD.PushWarning("PackagedFriendlyNPC: RewardCards 中存在 null 资源，已跳过");
				continue;
			}

			data.AcquireCard(cardResource, 1);
			grantedCount++;
			
			var cardObj = (GodotObject)cardResource;
			string cardName = cardObj?.Get("display_name").AsString() ?? "未知卡牌";
			GD.Print($"PackagedFriendlyNPC: 发放卡牌 [{cardName}]");
		}

		return grantedCount;
	}

	/// <summary>
	/// 发放指定索引的卡牌
	/// </summary>
	public bool GrantRewardCardByIndex(int cardIndex)
	{
		if (RewardCards == null || cardIndex < 0 || cardIndex >= RewardCards.Count)
		{
			GD.PushError($"PackagedFriendlyNPC: 无效的卡牌索引 {cardIndex}");
			return false;
		}

		var cardResource = RewardCards[cardIndex];
		if (cardResource == null)
		{
			GD.PushError($"PackagedFriendlyNPC: 索引 {cardIndex} 处的卡牌资源为 null");
			return false;
		}

		DataManager data = DataManager.Instance;
		if (data == null)
		{
			GD.PushError("PackagedFriendlyNPC: DataManager 不可用");
			return false;
		}

		data.AcquireCard(cardResource, 1);
		
		var cardObj = (GodotObject)cardResource;
		string cardName = cardObj?.Get("display_name").AsString() ?? "未知卡牌";
		GD.Print($"PackagedFriendlyNPC: 发放卡牌 [{cardName}]");
		
		return true;
	}
}