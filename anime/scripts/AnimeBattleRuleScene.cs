using Godot;
using Godot.Collections;

/// <summary>
/// Anime-only copy of the production BattleScene setup. The original combat
/// files are never changed: designers drag one GameRules resource here and
/// this scene creates the same runtime battle plus the additive animation Hub.
/// </summary>
[GlobalClass]
public partial class AnimeBattleRuleScene : Node2D
{
	[Signal] public delegate void TestBattleFinishedEventHandler(bool playerWon);

	[ExportGroup("Battle Runtime (copied from BattleScene)")]
	[Export] public BoardManager BoardManager { get; set; }
	[Export] public EffectResolver EffectResolver { get; set; }
	[Export] public BattleManager BattleManager { get; set; }
	[Export] public BattleScreen BattleScreen { get; set; }
	[Export] public PackedScene TrackSlotScene { get; set; }

	[ExportGroup("Drag To Configure")]
	[Export(PropertyHint.ResourceType, "GameRules")]
	public Resource BattleRules { get; set; }

	[ExportGroup("Test Fallback")]
	[Export(PropertyHint.ResourceType, "CardData")]
	public Resource FallbackTestCard { get; set; }

	public PlayerBattle Player { get; private set; }
	public EnemyManager EnemyManager { get; private set; }

	private int _gridWidth = 3;
	private int _gridHeight = 2;

	public override void _Ready()
	{
		if (BattleRules == null)
		{
			GD.PushError("AnimeBattleRuleScene: 请在 Inspector 的 Battle Rules 中拖入规则资源。");
			return;
		}

		GodotObject rules = BattleRules;
		Array<Vector2I> boardSizes = rules
			.Get(GDScriptKeys.GameRules.BoardSizes)
			.As<Array<Vector2I>>();
		if (boardSizes.Count > 0)
		{
			_gridWidth = boardSizes[0].X;
			_gridHeight = boardSizes[0].Y;
		}
		BoardManager.ConfigureBoard(new Vector2I(_gridWidth, _gridHeight), false);

		CreateBattleInstances();

		BattleManager.Player = Player;
		BattleManager.EnemyManager = EnemyManager;
		BattleManager.BoardManager = BoardManager;
		BattleManager.EffectResolver = EffectResolver;
		BattleManager.GameRules = BattleRules;
		BattleManager.Setup();

		BattleScreen.Setup(BoardManager, BattleManager);
		// The anime-only slot is visually identical to the production slot.
		// Its decorative child Controls ignore the mouse so the root
		// TrackSlot receives the real click, exactly as intended by the
		// production MoveToCellRequested route.
		if (TrackSlotScene != null)
			BattleScreen.UIManager.TrackSlotScene = TrackSlotScene;
		EffectResolver.SetBoardManager(BoardManager);
		BattleScreen.BindBattleInstances(Player, EnemyManager);
		BattleManager.BattleScreenRef = BattleScreen;

		// This is the same input route used by the production BattleScene.
		// In particular, clicking an empty TrackSlot reaches TryMoveToCell.
		BattleScreen.LightCellRequested += position => BattleManager.TryLightCell(position);
		BattleScreen.PlayCardRequested += instanceId => BattleManager.TryPlayCard(instanceId);
		BattleScreen.MoveRequested += action => BattleManager.TryMove(action);
		BattleScreen.MoveToCellRequested += cell => BattleManager.TryMoveToCell(cell);
		BattleScreen.EndTurnRequested += () => BattleManager.EndTurn();
		BattleScreen.BackToBuildRequested += () =>
			GD.Print("AnimeBattleRuleScene: 测试战斗中不返回构筑界面。");

		BoardManager.BoardChanged += RefreshViews;
		BattleManager.BattleStateChanged += RefreshViews;
		BattleManager.LogMessage += text => BattleScreen.AppendLog(text);
		BattleManager.BattleEnded += OnBattleEnded;

		Array<Dictionary> savedBuild = DataManager.Instance.LoadBuild();
		if (savedBuild != null && savedBuild.Count > 0)
			BoardManager.BuildFromSavedData(savedBuild);

		// A fresh project may not have a saved build yet. The copied test
		// scene includes one fallback card so it remains immediately playable.
		if (BoardManager.runtime_cards.Count == 0 && FallbackTestCard != null)
			BoardManager.PlaceCard(FallbackTestCard, Vector2I.Zero, 0);

		BattleScreen.ClearLog();
		BattleScreen.Visible = true;
		BattleScreen.RefreshAll(_gridWidth, _gridHeight);
		BattleManager.StartBattle();
	}

	private void CreateBattleInstances()
	{
		Player = GetNodeOrNull<PlayerBattle>("PlayerBattle");
		if (Player == null)
		{
			Player = new PlayerBattle { Name = "PlayerBattle" };
			AddChild(Player);
		}

		EnemyManager = GetNodeOrNull<EnemyManager>("EnemyManager");
		if (EnemyManager == null)
		{
			EnemyManager = new EnemyManager { Name = "EnemyManager" };
			AddChild(EnemyManager);
		}
	}

	private void RefreshViews() => BattleScreen.RefreshAll(_gridWidth, _gridHeight);

	private void OnBattleEnded(bool playerWon)
	{
		BattleScreen.AppendLog(playerWon ? "战斗胜利。" : "战斗失败。");
		EmitSignal(SignalName.TestBattleFinished, playerWon);
	}
}
