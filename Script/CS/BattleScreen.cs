using Godot;

/// <summary>
/// BattleScreen — 战斗界面协调层（实例化重构版）。
///
/// 玩家/敌人以独立场景实例挂载，BattleScreen 通过 UIManager 订阅它们的信号，
/// 实现信号驱动的 UI 更新（不再轮询 BattleManager 属性）。
/// </summary>
[GlobalClass]
public partial class BattleScreen : Control
{
    // ============ 信号（Facade：转发自 UIManager，供 MainScene 连接） ============
    [Signal] public delegate void LightCellRequestedEventHandler(Vector2I position);
    [Signal] public delegate void PlayCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void MoveRequestedEventHandler(int action);
    [Signal] public delegate void EndTurnRequestedEventHandler();
    [Signal] public delegate void BackToBuildRequestedEventHandler();

    // ============ 子管理器 ============
    public UIManager UIManager { get; private set; }

    // ============ 战斗实例引用 ============
    public PlayerBattle Player { get; set; }
    public EnemyManager EnemyManager { get; set; }
    private BoardManager boardManager;
    private BattleManager battleManager;

    // ============ 编辑器 Export（场景中的 UI 节点引用） ============
    [Export] private GridContainer battlegrid;
    [Export] private VBoxContainer activationbox;
    [Export] private VBoxContainer readylist;
    [Export] private RichTextLabel loglabel;
    [Export] private Label statuslabel;
    [Export] private Label energylabel;
    [Export] private Label roundlabel;
    [Export] private Label phaselabel;
    [Export] private Label readylabel;
    [Export] private Label distancelabel;
    [Export] private HBoxContainer distancetrack;
    [Export] private Button movebackbutton;
    [Export] private Button moveforwardbutton;
    [Export] private Label movehint;
    [Export] private TextureButton endturnbutton;
    [Export] private Button backbutton;
    [Export] private TextureProgressBar healthBar;

    // ================================================================
    //  Setup
    // ================================================================

    public void Setup(BoardManager board, BattleManager battle)
    {
        boardManager = board;
        battleManager = battle;

        UIManager = GetNodeOrNull<UIManager>("UIManager");
        if (UIManager == null)
        {
            UIManager = new UIManager();
            UIManager.Name = "UIManager";
            AddChild(UIManager);
        }

        // 注入 UI 节点引用
        UIManager.BattleGrid = battlegrid;
        UIManager.ActivationBox = activationbox;
        UIManager.ReadyList = readylist;
        UIManager.LogLabel = loglabel;
        UIManager.StatusLabel = statuslabel;
        UIManager.EnergyLabel = energylabel;
        UIManager.RoundLabel = roundlabel;
        UIManager.PhaseLabel = phaselabel;
        UIManager.ReadyLabel = readylabel;
        UIManager.DistanceLabel = distancelabel;
        UIManager.DistanceTrack = distancetrack;
        UIManager.MoveBackButton = movebackbutton;
        UIManager.MoveForwardButton = moveforwardbutton;
        UIManager.MoveHint = movehint;
        UIManager.EndTurnButton = endturnbutton;
        UIManager.BackButton = backbutton;
        UIManager.HealthBar = healthBar;

        UIManager.Setup(boardManager, battleManager);

        // 转发 UIManager 信号 → BattleScreen 信号（Facade）
        UIManager.LightCellRequested += (Vector2I pos) => EmitSignal(SignalName.LightCellRequested, pos);
        UIManager.PlayCardRequested += (int id) => EmitSignal(SignalName.PlayCardRequested, id);
        UIManager.MoveRequested += (int action) => EmitSignal(SignalName.MoveRequested, action);
        UIManager.EndTurnRequested += () => EmitSignal(SignalName.EndTurnRequested);
        UIManager.BackToBuildRequested += () => EmitSignal(SignalName.BackToBuildRequested);
    }

    /// <summary>
    /// 在战斗开始前调用——绑定 PlayerBattle + EnemyManager 信号到 UIManager。
    /// 之后 UI 自动响应实例状态变化，不再需要手动轮询。
    /// </summary>
    public void BindBattleInstances(PlayerBattle player, EnemyManager enemyManager)
    {
        Player = player;
        EnemyManager = enemyManager;

        UIManager.BindPlayer(Player);
        UIManager.BindEnemyManager(EnemyManager);
    }

    // ================================================================
    //  公共接口
    // ================================================================

    public void RefreshAll(int gridWidth, int gridHeight)
    {
        UIManager?.RefreshAll(gridWidth, gridHeight);
    }

    public void AppendLog(string text) => UIManager?.AppendLog(text);
    public void ClearLog() => UIManager?.ClearLog();
}