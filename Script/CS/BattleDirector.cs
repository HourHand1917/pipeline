using Godot;
using Godot.Collections;

/// <summary>
/// 全局战斗调度器（Autoload）。持有「待开始的战斗」和「返回信息」，
/// 解决 change_scene 会销毁场景内节点、跨场景传参必须放 Autoload 的问题。
/// </summary>
[GlobalClass]
public partial class BattleDirector : Node
{
	private const string DefaultBattleStartSfxPath =
		"res://features/dialogue/npc/audio/combat/common/battle_start.ogg";

	public static BattleDirector Instance { get; private set; }

	/// <summary>战斗失败时执行的一次性清理回调（如 F4 重置两阶段标记）。</summary>
	private System.Action _onBattleLostReset;

	/// <summary>注册失败时的清理回调。胜利时会被清空。</summary>
	public void RegisterBattleLostReset(System.Action reset) => _onBattleLostReset = reset;

	/// <summary>待加载的战斗 rules .tres 路径</summary>
	public string PendingRulesPath { get; private set; } = "";
	/// <summary>触发战斗的 NPC 持久化 id</summary>
	public StringName NpcPersistenceId { get; private set; } = "";
	/// <summary>NPC 所在地图（也是战斗结束返回的地图）</summary>
	public StringName NpcMapId { get; private set; } = "";
	/// <summary>战斗结束返回的生成点</summary>
	public StringName ReturnSpawnId { get; private set; } = "";
	/// <summary>刚打赢战斗的 NPC id（NPC 返回后据此一次性自动弹战利品页）</summary>
	public StringName PendingLootNpcId { get; private set; } = "";
	/// <summary>Optional story level checkpoint granted only after this battle is won.</summary>
	public int PendingLevelAfterVictory { get; private set; } = -1;
	/// <summary>Optional scene opened after victory instead of returning to exploration.</summary>
	public string PendingVictoryScenePath { get; private set; } = "";

	/// <summary>进入战斗前从探索场景背景图截取的静态区域。</summary>
	public Texture2D PendingBattleBackdropTexture { get; private set; }
	public Rect2 PendingBattleBackdropRegion { get; private set; }
	public string PendingBattleBackdropSource { get; private set; } = "";

	/// <summary>地图默认音乐（战斗结束恢复用）</summary>
	[Export] public AudioStream DefaultMapMusic { get; set; }
	[Export] public AudioStream BattleStartSfx { get; set; }

	public override void _Ready()
	{
		if (Instance != null) { GD.PushError("BattleDirector: 重复实例化"); return; }
		Instance = this;
		BattleStartSfx ??= ResourceLoader.Load<AudioStream>(DefaultBattleStartSfxPath);
	}

	/// <summary>由 NPC 调用，进入战斗场景。</summary>
	public void StartBattle(
		string battleScenePath,
		string rulesPath,
		StringName npcId,
		StringName mapId,
		StringName returnSpawnId,
		Node2D backdropFocus = null,
		int levelAfterVictory = -1,
		string victoryScenePath = "")
	{
		PendingRulesPath = rulesPath;
		NpcPersistenceId = npcId;
		NpcMapId = mapId;
		ReturnSpawnId = returnSpawnId;
		PendingLevelAfterVictory = levelAfterVictory;
		PendingVictoryScenePath = victoryScenePath ?? "";
		PrepareBattleBackdrop(backdropFocus);
		GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlaySfx(BattleStartSfx);

		// 进入战斗转场前锁定玩家移动，避免圈缩转场期间还能走动
		PlayerController.Instance?.LockMovement();

		SceneTransition.Instance.ChangeScene(battleScenePath);
	}

	/// <summary>
	/// Finds the authored exploration background and stores a wide, static crop
	/// around the encounter. It deliberately ignores actors, UI and interactable
	/// sprites, so the battle never shows a frozen HUD or duplicate characters.
	/// </summary>
	public void PrepareBattleBackdrop(Node2D focus)
	{
		PendingBattleBackdropTexture = null;
		PendingBattleBackdropRegion = default;
		PendingBattleBackdropSource = "";

		Node scene = GetTree()?.CurrentScene;
		if (scene == null || focus == null)
			return;

		Sprite2D best = null;
		double bestScore = 0.0;
		FindBackdropCandidate(scene, ref best, ref bestScore);
		if (best?.Texture == null)
			return;

		Rect2 sourceRect = best.RegionEnabled
			? best.RegionRect
			: new Rect2(Vector2.Zero, best.Texture.GetSize());
		if (sourceRect.Size.X <= 1.0f || sourceRect.Size.Y <= 1.0f)
			return;

		Vector2 localFocus = best.ToLocal(focus.GlobalPosition);
		Vector2 textureFocus = sourceRect.Position + localFocus;
		if (best.Centered)
			textureFocus += sourceRect.Size * 0.5f;

		Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1440, 1080);
		float authoredViewportWidth = ProjectSettings
			.GetSetting("display/window/size/viewport_width", 1440)
			.AsSingle();
		float referenceViewportWidth = Mathf.Max(viewportSize.X, authoredViewportWidth);
		float localViewportWidth = referenceViewportWidth
			/ Mathf.Max(0.01f, Mathf.Abs(best.GlobalScale.X));
		// Long battle tracks need more environment around the encounter. Keep
		// the source crop wider than the exploration camera, but use the same
		// tall stage proportion as the authored exploration view instead of
		// the old letterbox strip. The crop remains completely static while
		// only the combatant track scrolls horizontally.
		float cropWidth = Mathf.Min(sourceRect.Size.X, Mathf.Max(localViewportWidth * 1.45f, 1440.0f));
		const float BattleStageAspect = 1440.0f / 700.0f;
		float cropHeight = Mathf.Min(sourceRect.Size.Y, cropWidth / BattleStageAspect);
		cropWidth = Mathf.Min(cropWidth, cropHeight * BattleStageAspect);

		float cropX = Mathf.Clamp(
			textureFocus.X - cropWidth * 0.5f,
			sourceRect.Position.X,
			sourceRect.End.X - cropWidth);
		// The NPC/player origin is authored on the exploration floor. Keep it
		// near the bottom edge so the combat sprites can stand on that same
		// ground line, with the remainder of the battle screen filled black.
		float cropY = Mathf.Clamp(
			textureFocus.Y - cropHeight * 0.96f,
			sourceRect.Position.Y,
			sourceRect.End.Y - cropHeight);

		PendingBattleBackdropTexture = best.Texture;
		PendingBattleBackdropRegion = new Rect2(cropX, cropY, cropWidth, cropHeight);
		PendingBattleBackdropSource = best.GetPath().ToString();
	}

	private static void FindBackdropCandidate(Node node, ref Sprite2D best, ref double bestScore)
	{
		// UI and interactable subtrees contain portraits, dialogues and characters,
		// never the authored world backdrop we want to carry into battle.
		if (node is CanvasLayer || node is Control || node is Area2D || node is CharacterBody2D)
			return;

		if (node is Sprite2D sprite && sprite.Texture != null && sprite.IsVisibleInTree())
		{
			Vector2 size = sprite.Texture.GetSize();
			Vector2 scale = sprite.GlobalScale.Abs();
			double score = size.X * size.Y * Mathf.Max(0.01f, scale.X * scale.Y);
			string nodeName = sprite.Name.ToString();
			if (nodeName.Contains("背景", System.StringComparison.Ordinal)
				|| nodeName.Contains("background", System.StringComparison.OrdinalIgnoreCase))
				score *= 1000.0;
			if (score > bestScore)
			{
				best = sprite;
				bestScore = score;
			}
		}

		foreach (Node child in node.GetChildren())
			FindBackdropCandidate(child, ref best, ref bestScore);
	}

	/// <summary>战斗胜利：标记 NPC 已打败（尸体），返回探索场景。</summary>
	public void OnBattleWon()
	{
		_onBattleLostReset = null;
		string victoryScenePath = PendingVictoryScenePath;
		PendingVictoryScenePath = "";
		if (PendingLevelAfterVictory >= 0)
			DataManager.Instance?.SetLevelAtLeast(PendingLevelAfterVictory);
		if (!string.IsNullOrEmpty(NpcPersistenceId) && !string.IsNullOrEmpty(NpcMapId))
		{
			GameState.Instance?.SetObjectState(
				NpcMapId.ToString(), NpcPersistenceId.ToString(),
				new Dictionary { { "defeated", true } });
		}
		PendingLootNpcId = string.IsNullOrWhiteSpace(victoryScenePath)
			? NpcPersistenceId
			: new StringName();
		PendingLevelAfterVictory = -1;
		if (!string.IsNullOrWhiteSpace(victoryScenePath))
		{
			GetNodeOrNull<AudioManager>("/root/AudioManager")
				?.SetBusVolume(AudioManager.Bus.MUSIC, 1.0f);
			SceneTransition.Instance?.ChangeScene(victoryScenePath);
			return;
		}
		ReturnToExploration();
	}

	/// <summary>战斗失败：返回探索场景（血量已归零，后续存档系统处理死亡）。</summary>
	public void OnBattleLost()
	{
		PendingLevelAfterVictory = -1;
		PendingVictoryScenePath = "";
		StringName mapId;
		StringName spawnId;
		if (IsTutorialMap(NpcMapId))
		{
			mapId = "f1_0";
			spawnId = ""; // 空生成点 → f1_0 默认出生位置（教程起点）
		}
		else
		{
			mapId = "home";
			spawnId = "home_entry";
		}

		// 复活回满血
		if (DataManager.Instance != null)
			DataManager.Instance.SetHp(DataManager.Instance.MaxPlayerHp);

		// 战斗失败：执行一次性清理（如 F4 重置两阶段标记），让玩家下次从头重打。
		_onBattleLostReset?.Invoke();
		_onBattleLostReset = null;

		RestoreMapMusic();
		MapManager.Instance.TravelTo(mapId, spawnId);
	}

	private static bool IsTutorialMap(StringName mapId)
	{
		string s = mapId.ToString();
		return s == "f1_0" || s == "f1_1" || s == "f1_2";
	}

	/// <summary>NPC 领取「刚打赢」标记。只匹配该 NPC 一次，消费后返回 false。</summary>
	public bool ConsumePendingLoot(StringName npcId)
	{
		if (PendingLootNpcId != npcId) return false;
		PendingLootNpcId = "";
		return true;
	}

	private void ReturnToExploration()
	{
		RestoreMapMusic();
		MapManager.Instance.TravelTo(NpcMapId, ReturnSpawnId);
	}

	/// <summary>战斗结束后恢复地图音乐。</summary>
	private void RestoreMapMusic()
	{
		var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
		audio?.PlayMusicWithFade(DefaultMapMusic);
	}
}