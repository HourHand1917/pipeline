using Godot;

/// <summary>
/// MainScene — 战斗场景入口（黑箱调用）。
/// </summary>
public partial class idk : Node2D
{
    [Export] public BoardManager BoardManager { get; set; }
    [Export] public EffectResolver EffectResolver { get; set; }
    [Export] public BattleManager BattleManager { get; set; }
    [Export] public BuildScreen BuildScreen { get; set; }
    [Export] public BattleScreen BattleScreen { get; set; }

    [Export] private Resource _testGameRules;

    public PlayerBattle Player { get; private set; }
    public EnemyManager EnemyManager { get; private set; }

    private int currentGridWidth = 3;
    private int currentGridHeight = 2;
    private GodotObject _rules;

    public override void _Ready()
    {
        if (_testGameRules != null)
            _rules = _testGameRules as GodotObject;
        else
            _rules = DataManager.Instance.GetRules();

        if (_rules == null) return;

        // ============ 调试 ============
        GD.Print($"[调试] _testGameRules: {_testGameRules != null}");
        GD.Print($"[调试] _rules: {_rules != null}");

        var boardSizes = _rules.Get(GDScriptKeys.GameRules.BoardSizes).As<Godot.Collections.Array<Vector2I>>();
        GD.Print($"[调试] boardSizes.Count: {boardSizes.Count}");

        var battleMap = _rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        GD.Print($"[调试] battleMap: {battleMap != null}");

        if (battleMap != null)
        {
            GD.Print($"[调试] cell_count: {battleMap.Get(GDScriptKeys.BattleMap.CellCount).AsInt32()}");
            var enemyList = battleMap.Get("enemy_data_list").As<Godot.Collections.Array>();
            GD.Print($"[调试] enemy_data_list.Count: {enemyList.Count}");
            var startCells = battleMap.Get("enemy_start_cells").As<Godot.Collections.Array<int>>();
            GD.Print($"[调试] enemy_start_cells.Count: {startCells.Count}");
            for (int i = 0; i < startCells.Count; i++)
                GD.Print($"[调试] startCells[{i}]: {startCells[i]}");
        }
        // ============ 调试结束 ============

        Vector2I defaultSize = boardSizes[0];
        currentGridWidth = defaultSize.X;
        currentGridHeight = defaultSize.Y;
        BoardManager.ConfigureBoard(defaultSize, false);

        CreateBattleInstances(_rules);

        BattleManager.Player = Player;
        BattleManager.EnemyManager = EnemyManager;
        BattleManager.BoardManager = BoardManager;
        BattleManager.EffectResolver = EffectResolver;
        BattleManager.GameRules = _testGameRules;
        BattleManager.Setup();

        BuildScreen.Setup(BoardManager);
        BattleScreen.Setup(BoardManager, BattleManager);
        EffectResolver.SetBoardManager(BoardManager);
        BattleScreen.BindBattleInstances(Player, EnemyManager);

        BattleManager.BattleScreenRef = BattleScreen;

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

        ShowBuild();
    }

    public void ReadyWithBattleId(string battleId)
    {
        string path = $"res://data/battles/{battleId}.tres";
        var res = GD.Load<Resource>(path);
        if (res != null)
        {
            _testGameRules = res;
            _Ready();
        }
        else
        {
            GD.PushError($"找不到战斗地图：{path}");
        }
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