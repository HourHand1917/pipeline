using Godot;
using Godot.Collections;

/// <summary>
/// 独立战斗场景入口。由探索地图的 NPC 触发。
/// 读 BattleDirector 的 pending rules，从 DataManager 恢复工作台棋盘和血量，
/// 战斗结束写回血量并返回探索场景。
/// </summary>
[GlobalClass]
public partial class BattleScene : Node2D
{
    [Export] public BoardManager BoardManager { get; set; }
    [Export] public EffectResolver EffectResolver { get; set; }
    [Export] public BattleManager BattleManager { get; set; }
    [Export] public BattleScreen BattleScreen { get; set; }

    private PlayerBattle _player;
    private EnemyManager _enemyManager;
    private int _gridWidth = 3;
    private int _gridHeight = 2;

    public override void _Ready()
    {
        var rulesPath = BattleDirector.Instance.PendingRulesPath;
        var rules = GD.Load<Resource>(rulesPath);
        if (rules == null)
        {
            GD.PushError($"BattleScene: 找不到战斗规则 {rulesPath}");
            return;
        }
        var rulesObj = rules as GodotObject;

        // 棋盘尺寸
        var boardSizes = rulesObj.Get(GDScriptKeys.GameRules.BoardSizes).As<Array<Vector2I>>();
        if (boardSizes.Count > 0)
        {
            _gridWidth = boardSizes[0].X;
            _gridHeight = boardSizes[0].Y;
        }
        // 能力成长：棋盘扩容覆盖默认尺寸
        var growthBoard = GrowthManager.Instance?.GetBoardSize();
        if (growthBoard.HasValue)
        {
            _gridWidth = growthBoard.Value.X;
            _gridHeight = growthBoard.Value.Y;
        }
        BoardManager.ConfigureBoard(new Vector2I(_gridWidth, _gridHeight), false);

        // 创建战斗实例
        CreateBattleInstances();

        // 注入 BattleManager
        BattleManager.Player = _player;
        BattleManager.EnemyManager = _enemyManager;
        BattleManager.BoardManager = BoardManager;
        BattleManager.EffectResolver = EffectResolver;
        BattleManager.GameRules = rules;
        BattleManager.Setup();

        // 连接 BattleScreen
        BattleScreen.Setup(BoardManager, BattleManager);
        EffectResolver.SetBoardManager(BoardManager);
        BattleScreen.BindBattleInstances(_player, _enemyManager);
        BattleManager.BattleScreenRef = BattleScreen;

        // 信号连接
        BattleScreen.LightCellRequested += (Vector2I pos) => BattleManager.TryLightCell(pos);
        BattleScreen.PlayCardRequested += (int id) => BattleManager.TryPlayCard(id);
        BattleScreen.MoveRequested += (int action) => BattleManager.TryMove(action);
        BattleScreen.EndTurnRequested += () => BattleManager.EndTurn();
        BattleScreen.BackToBuildRequested += () => GD.Print("战斗中无法返回构筑。");

        BoardManager.BoardChanged += RefreshViews;
        BattleManager.BattleStateChanged += RefreshViews;
        BattleManager.LogMessage += (string text) => BattleScreen.AppendLog(text);
        BattleManager.BattleEnded += OnBattleEnded;

        // 从工作台恢复构筑棋盘
        var savedBuild = DataManager.Instance.LoadBuild();
        if (savedBuild != null && savedBuild.Count > 0)
            BoardManager.BuildFromSavedData(savedBuild);

        // 直接开始战斗（构筑已在工作台完成）
        BattleScreen.ClearLog();
        BattleScreen.Visible = true;
        BattleScreen.RefreshAll(_gridWidth, _gridHeight);
        BattleManager.StartBattle();
    }

    private void CreateBattleInstances()
    {
        _player = GetNodeOrNull<PlayerBattle>("PlayerBattle");
        if (_player == null)
        {
            _player = new PlayerBattle();
            _player.Name = "PlayerBattle";
            AddChild(_player);
        }

        _enemyManager = GetNodeOrNull<EnemyManager>("EnemyManager");
        if (_enemyManager == null)
        {
            _enemyManager = new EnemyManager();
            _enemyManager.Name = "EnemyManager";
            AddChild(_enemyManager);
        }
    }

    private void RefreshViews()
    {
        BattleScreen.RefreshAll(_gridWidth, _gridHeight);
    }

    private void OnBattleEnded(bool playerWon)
    {
        BattleScreen.AppendLog(playerWon ? "战斗胜利！" : "战斗失败。");

        // 写回血量到 DataManager（探索/战斗间继承）
        DataManager.Instance.SetMaxHp(_player.MaxHp, false);
        DataManager.Instance.SetHp(_player.CurrentHp);

        if (playerWon)
            BattleDirector.Instance.OnBattleWon();
        else
            BattleDirector.Instance.OnBattleLost();
    }
}
