using Godot;

[GlobalClass]
public partial class BattleScreen : Control
{
    [Signal] public delegate void LightCellRequestedEventHandler(Vector2I position);
    [Signal] public delegate void PlayCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void MoveRequestedEventHandler(int action);
    [Signal] public delegate void EndTurnRequestedEventHandler();
    [Signal] public delegate void BackToBuildRequestedEventHandler();

    public UIManager UIManager { get; private set; }
    public PlayerBattle Player { get; set; }
    public EnemyManager EnemyManager { get; set; }
    private BoardManager boardManager;
    private BattleManager battleManager;

    [Export] private GridContainer battlegrid;
    [Export] private GridContainer energyGrid;
    [Export] private VBoxContainer activationbox;
    [Export] private VBoxContainer readylist;
    [Export] private RichTextLabel loglabel;
    [Export] private Label statuslabel;
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
    [Export] private Texture2D bulbOn;
    [Export] private Texture2D bulbOff;
    [Export] private ItemPanel itemPanel;

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

        UIManager.BattleGrid = battlegrid;
        UIManager.EnergyGrid = energyGrid;
        UIManager.ActivationBox = activationbox;
        UIManager.ReadyList = readylist;
        UIManager.LogLabel = loglabel;
        UIManager.StatusLabel = statuslabel;
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
        UIManager.BulbOn = bulbOn;
        UIManager.BulbOff = bulbOff;

        UIManager.LightCellRequested += (Vector2I pos) => EmitSignal(SignalName.LightCellRequested, pos);
        UIManager.PlayCardRequested += (int id) => EmitSignal(SignalName.PlayCardRequested, id);
        UIManager.MoveRequested += (int action) => EmitSignal(SignalName.MoveRequested, action);
        UIManager.EndTurnRequested += () => EmitSignal(SignalName.EndTurnRequested);
        UIManager.BackToBuildRequested += () => EmitSignal(SignalName.BackToBuildRequested);

        itemPanel?.Refresh();
    }

    public void BindBattleInstances(PlayerBattle player, EnemyManager enemyManager)
    {
        Player = player;
        EnemyManager = enemyManager;
        UIManager.BindPlayer(Player);
        UIManager.BindEnemyManager(EnemyManager);
    }

    public void RefreshAll(int gridWidth, int gridHeight) => UIManager?.RefreshAll(gridWidth, gridHeight);
    public void AppendLog(string text) => UIManager?.AppendLog(text);
    public void ClearLog() => UIManager?.ClearLog();
}