using Godot;

public partial class MainScene : Node2D
{
    [Export] public BoardManager BoardManager { get; set; }
    [Export] public EffectResolver EffectResolver { get; set; }
    [Export] public BattleManager BattleManager { get; set; }
    [Export] public BuildScreen BuildScreen { get; set; }
    [Export] public BattleScreen BattleScreen { get; set; }
    [Export] public ItemPanel ItemPanel { get; set; }

    public PlayerBattle Player { get; private set; }
    public EnemyManager EnemyManager { get; private set; }
    private int currentGridWidth = 3;
    private int currentGridHeight = 2;

    public override void _Ready()
    {
        var rules = DataManager.Instance.GetRules();
        Vector2I defaultSize = rules.Get(GDScriptKeys.GameRules.BoardSizes).As<Godot.Collections.Array<Vector2I>>()[0];
        currentGridWidth = defaultSize.X;
        currentGridHeight = defaultSize.Y;
        BoardManager.ConfigureBoard(defaultSize, false);

        CreateBattleInstances(rules);

        BattleManager.Player = Player;
        BattleManager.EnemyManager = EnemyManager;
        BattleManager.BoardManager = BoardManager;
        BattleManager.EffectResolver = EffectResolver;
        BattleManager.Setup();

        BuildScreen.Setup(BoardManager);
        BattleScreen.Setup(BoardManager, BattleManager);
        EffectResolver.SetBoardManager(BoardManager);
        BattleScreen.BindBattleInstances(Player, EnemyManager);

        BuildScreen.PlaceCardRequested += OnPlaceCardRequested;
        BuildScreen.RemoveCardRequested += (int id) => BoardManager.RemoveCard(id);
        BuildScreen.StartBattleRequested += OnStartBattleRequested;
        BuildScreen.ClearBuildRequested += OnClearBuildRequested;
        BuildScreen.BoardSizeRequested += OnBoardSizeRequested;

        BattleScreen.LightCellRequested += (Vector2I pos) => BattleManager.TryLightCell(pos);
        BattleScreen.PlayCardRequested += (int id) => BattleManager.TryPlayCard(id);
        BattleScreen.MoveRequested += (int action) => BattleManager.TryMove(action);
        BattleScreen.EndTurnRequested += () => BattleManager.EndTurn();
        BattleScreen.BackToBuildRequested += ShowBuild;

        BoardManager.BoardChanged += RefreshAllViews;
        BattleManager.BattleStateChanged += RefreshAllViews;
        BattleManager.LogMessage += (string text) => BattleScreen.AppendLog(text);
        BattleManager.BattleEnded += OnBattleEnded;

        if (ItemPanel != null)
        {
            ItemPanel.ItemUsed += (int index) => BattleManager.UseItem(index);
            ItemPanel.ItemDiscarded += (int index) => BattleManager.DiscardItem(index);
            BattleManager.BattleStateChanged += () => ItemPanel.Refresh();
        }

        ShowBuild();
    }

    private void CreateBattleInstances(GodotObject rules) { /* 保持不变 */ }
    private void OnPlaceCardRequested(StringName cardId, Vector2I anchor, int rotationSteps) { /* 保持不变 */ }
    private void OnBoardSizeRequested(Vector2I size) { /* 保持不变 */ }
    private void OnStartBattleRequested() { /* 保持不变 */ }
    private void OnBattleEnded(bool playerWon) { BattleScreen.AppendLog(playerWon ? "战斗胜利！" : "战斗失败。"); }
    private void OnClearBuildRequested() { BoardManager.ResetBoard(); BuildScreen.ShowMessage("随身武器库已清空。"); }
    private void ShowBuild() { /* 保持不变 */ }
    private void ShowBattle() { /* 保持不变 */ }
    private void RefreshAllViews() { BuildScreen.RefreshAll(); BattleScreen.RefreshAll(currentGridWidth, currentGridHeight); }
}