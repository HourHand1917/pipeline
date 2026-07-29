using Godot;

public partial class MainScene : Node2D
{
    [Export] public BoardManager BoardManager { get; set; }
    [Export] public EffectResolver EffectResolver { get; set; }
    [Export] public BattleManager BattleManager { get; set; }
    [Export] public BuildScreen BuildScreen { get; set; }
    [Export] public BattleScreen BattleScreen { get; set; }

    private int currentGridWidth = 3;
    private int currentGridHeight = 2;

    public override void _Ready()
    {
        var rules = DataManager.Instance.GetRules();
        Vector2I defaultSize = rules.Get("board_sizes").As<Godot.Collections.Array<Vector2I>>()[0];
        currentGridWidth = defaultSize.X;
        currentGridHeight = defaultSize.Y;
        BoardManager.ConfigureBoard(defaultSize, false);

        BattleManager.Setup(BoardManager, EffectResolver);
        BuildScreen.Setup(BoardManager);
        BattleScreen.Setup(BoardManager, BattleManager);

        // BuildScreen 信号
        BuildScreen.PlaceCardRequested += OnPlaceCardRequested;
        BuildScreen.RemoveCardRequested += (int id) => BoardManager.RemoveCard(id);
        BuildScreen.StartBattleRequested += OnStartBattleRequested;
        BuildScreen.ClearBuildRequested += OnClearBuildRequested;
        BuildScreen.BoardSizeRequested += OnBoardSizeRequested;

        // BattleScreen 信号
        BattleScreen.LightCellRequested += (Vector2I pos) => BattleManager.TryLightCell(pos);
        BattleScreen.PlayCardRequested += (int id) => BattleManager.TryPlayCard(id);
        BattleScreen.MoveRequested += (int action) => BattleManager.TryMove(action);
        BattleScreen.EndTurnRequested += () => BattleManager.EndTurn();
        BattleScreen.BackToBuildRequested += ShowBuild;

        // BoardManager / BattleManager 信号
        BoardManager.BoardChanged += RefreshAllViews;
        BattleManager.BattleStateChanged += RefreshAllViews;
        BattleManager.LogMessage += (string text) => BattleScreen.AppendLog(text);
        BattleManager.BattleEnded += OnBattleEnded;

        EffectResolver.SetBoardManager(BoardManager);
        
        ShowBuild();
    }

    // ================================================================
    //  放置卡牌
    // ================================================================

    private void OnPlaceCardRequested(StringName cardId, Vector2I anchor, int rotationSteps)
    {
        var card = DataManager.Instance.GetCard(cardId);
        if (card == null)
        {
            BuildScreen.ShowMessage($"找不到卡牌资源：{cardId}");
            return;
        }

        var result = BoardManager.PlaceCard(card, anchor, rotationSteps);
        if (result == null)
            BuildScreen.ShowMessage("这里放不下整件装备。");
        else
        {
            var data = card as GodotObject;
            BuildScreen.ShowMessage($"{data.Get("display_name")} 已放入。");
        }
    }

    // ================================================================
    //  板子尺寸切换
    // ================================================================

    private void OnBoardSizeRequested(Vector2I size)
    {
        int removedCount = BoardManager.ConfigureBoard(size, true);
        currentGridWidth = size.X;
        currentGridHeight = size.Y;
        BuildScreen.ApplyBoardSize(size.X, size.Y);

        if (removedCount > 0)
            BuildScreen.ShowMessage($"切换为 {size.Y}×{size.X}，{removedCount} 件越界装备已取出。");
        else
            BuildScreen.ShowMessage($"已切换为 {size.Y}×{size.X} 箱型。");
    }

    // ================================================================
    //  战斗开始 / 结束
    // ================================================================

    private void OnStartBattleRequested()
    {
        if (BoardManager.runtime_cards.Count == 0)
        {
            BuildScreen.ShowMessage("至少放入一件装备才能开始试战。");
            return;
        }

        DataManager.Instance.SaveBuildWithSize(BoardManager.runtime_cards,
            new Vector2I(currentGridWidth, currentGridHeight));
        BattleScreen.ClearLog();
        ShowBattle();
        BattleManager.StartBattle();
    }

    private void OnBattleEnded(bool playerWon)
    {
        BattleScreen.AppendLog(playerWon ? "战斗胜利！" : "战斗失败。");
    }

    // ================================================================
    //  清空构筑
    // ================================================================

    private void OnClearBuildRequested()
    {
        BoardManager.ResetBoard();
        BuildScreen.ShowMessage("随身武器库已清空。");
    }

    // ================================================================
    //  界面切换
    // ================================================================

    private void ShowBuild()
    {
        if (BattleManager.CurrentPhase != BattleManager.Phase.Build)
            BattleManager.ReturnToBuild();

        BuildScreen.Visible = true;
        BattleScreen.Visible = false;
        BuildScreen.RefreshAll();
    }

    private void ShowBattle()
    {
        BuildScreen.Visible = false;
        BattleScreen.Visible = true;
        BattleScreen.RefreshAll(currentGridWidth, currentGridHeight);
    }

    private void RefreshAllViews()
    {
        BuildScreen.RefreshAll();
        BattleScreen.RefreshAll(currentGridWidth, currentGridHeight);
    }
}