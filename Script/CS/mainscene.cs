using Godot;

/// <summary>
/// MainScene — 战斗场景入口（黑箱调用）。
/// </summary>
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

        // TODO: ItemPanel 信号连接（等节点配置好后再启用）
        // if (ItemPanel != null)
        // {
        //     ItemPanel.ItemUsed += (int index) => BattleManager.UseItem(index);
        //     ItemPanel.ItemDiscarded += (int index) => BattleManager.DiscardItem(index);
        //     BattleManager.BattleStateChanged += () => ItemPanel.Refresh();
        // }

        ShowBuild();
    }

    private void CreateBattleInstances(GodotObject rules)
    {
        Player = GetNodeOrNull<PlayerBattle>("PlayerBattle");
        if (Player == null)
        {
            Player = new PlayerBattle();
            Player.Name = "PlayerBattle";
            AddChild(Player);
        }

        EnemyManager = GetNodeOrNull<EnemyManager>("EnemyManager");
        if (EnemyManager == null)
        {
            EnemyManager = new EnemyManager();
            EnemyManager.Name = "EnemyManager";
            AddChild(EnemyManager);

            var enemyConfigs = new Godot.Collections.Array<GodotObject>();
            var enemyData = rules?.Get(GDScriptKeys.GameRules.EnemyData).As<GodotObject>();
            if (enemyData != null)
                enemyConfigs.Add(enemyData);
            EnemyManager.SetEnemyConfigs(enemyConfigs);
        }
    }

    private void OnPlaceCardRequested(StringName cardId, Vector2I anchor, int rotationSteps)
    {
        var card = DataManager.Instance.GetCard(cardId);
        if (card == null) { BuildScreen.ShowMessage($"找不到卡牌资源：{cardId}"); return; }

        var result = BoardManager.PlaceCard(card, anchor, rotationSteps);
        if (result == null)
            BuildScreen.ShowMessage("这里放不下整件装备。");
        else
        {
            var data = card as GodotObject;
            BuildScreen.ShowMessage($"{data.Get(GDScriptKeys.CardData.DisplayName)} 已放入。");
        }
    }

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

    private void OnClearBuildRequested()
    {
        BoardManager.ResetBoard();
        BuildScreen.ShowMessage("随身武器库已清空。");
    }

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