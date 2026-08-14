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
    [Export] public CombatCampaignController CampaignController { get; set; }

    public PlayerBattle Player { get; private set; }
    public EnemyManager EnemyManager { get; private set; }

    private int currentGridWidth = 3;
    private int currentGridHeight = 2;
    private GodotObject _rules;
    private bool _pendingPreservePlayerState;
    private bool _pendingPreserveBoardRuntimeState;
    private Label _campaignBanner;

    public override void _Ready()
    {
        // In inherited scenes Godot can occasionally lose a NodePath-exported C#
        // reference while the child node itself is still present. Resolve the
        // campaign controller by its stable scene name so the production entry
        // always starts the four-battle campaign instead of the legacy test rule.
        CampaignController ??= GetNodeOrNull<CombatCampaignController>("CombatCampaignController");

        if (CampaignController != null && CampaignController.PeekInitialRules() != null)
            _rules = CampaignController.PeekInitialRules() as GodotObject;
        else if (_testGameRules != null)
            _rules = _testGameRules as GodotObject;
        else
            _rules = DataManager.Instance.GetRules();

        if (_rules == null) return;

        var boardSizes = _rules.Get(GDScriptKeys.GameRules.BoardSizes).As<Godot.Collections.Array<Vector2I>>();
        var battleMap = _rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();

        Vector2I defaultSize = boardSizes[0];
        currentGridWidth = defaultSize.X;
        currentGridHeight = defaultSize.Y;
        BoardManager.ConfigureBoard(defaultSize, false);

        CreateBattleInstances(_rules);

        BattleManager.Player = Player;
        BattleManager.EnemyManager = EnemyManager;
        BattleManager.BoardManager = BoardManager;
        BattleManager.EffectResolver = EffectResolver;
        BattleManager.GameRules = _rules as Resource;
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

        if (CampaignController != null)
        {
            CampaignController.Bind(this);
            CampaignController.WaveChanged += (_, _, _) => RefreshCampaignBanner();
            CampaignController.CampaignStateChanged += (_) => RefreshCampaignBanner();
            CampaignController.InitializeCampaign();
            EnsureCampaignBanner();
            RefreshCampaignBanner();
        }
        else
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
        BattleManager.StartBattle(_pendingPreservePlayerState, _pendingPreserveBoardRuntimeState);
        _pendingPreservePlayerState = false;
        _pendingPreserveBoardRuntimeState = false;
        CampaignController?.NotifyWaveStarted();
    }

    private void OnBattleEnded(bool playerWon)
    {
        BattleScreen.AppendLog(playerWon ? "战斗胜利！" : "战斗失败。");
        CampaignController?.HandleBattleEnded(playerWon);
    }

    public void PrepareCampaignWave(
        Resource gameRules,
        bool preservePlayerState,
        bool preserveBoardRuntimeState,
        bool startImmediately,
        string message)
    {
        if (gameRules == null) return;
        _testGameRules = gameRules;
        _rules = gameRules as GodotObject;
        BattleManager.GameRules = gameRules;
        _pendingPreservePlayerState = preservePlayerState;
        _pendingPreserveBoardRuntimeState = preserveBoardRuntimeState;
        EnsureCampaignBanner();
        RefreshCampaignBanner();

        var boardSizes = _rules.Get(GDScriptKeys.GameRules.BoardSizes).As<Godot.Collections.Array<Vector2I>>();
        if (boardSizes.Count > 0 && !preserveBoardRuntimeState)
        {
            currentGridWidth = boardSizes[0].X;
            currentGridHeight = boardSizes[0].Y;
            BoardManager.ConfigureBoard(boardSizes[0], true);
            BuildScreen.ApplyBoardSize(currentGridWidth, currentGridHeight);
        }

        if (startImmediately)
        {
            BattleScreen.ClearLog();
            BattleScreen.AppendLog(message);
            ShowBattle();
            BattleManager.StartBattle(preservePlayerState, preserveBoardRuntimeState);
            _pendingPreservePlayerState = false;
            _pendingPreserveBoardRuntimeState = false;
            CampaignController?.NotifyWaveStarted();
        }
        else
        {
            ShowBuild();
            BuildScreen.ShowMessage(message);
        }
    }

    public void ShowCampaignCompletion(string message)
    {
        BattleScreen.AppendLog(message);
        BuildScreen.ShowMessage(message);
        ShowBuild();
        RefreshCampaignBanner();
    }

    private void EnsureCampaignBanner()
    {
        if (_campaignBanner != null || CampaignController == null) return;
        var layer = new CanvasLayer { Name = "CampaignProgressLayer", Layer = 96 };
        AddChild(layer);
        var panel = new PanelContainer
        {
            Name = "CampaignProgressPanel",
            OffsetLeft = 24,
            OffsetTop = 18,
            OffsetRight = 730,
            OffsetBottom = 64,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#101b20e6"),
            BorderColor = new Color("#5ed7dc"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        _campaignBanner = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _campaignBanner.AddThemeFontSizeOverride("font_size", 21);
        _campaignBanner.AddThemeColorOverride("font_color", new Color("#8ff4f1"));
        panel.AddChild(_campaignBanner);
        layer.AddChild(panel);
    }

    private void RefreshCampaignBanner()
    {
        if (_campaignBanner == null || CampaignController == null) return;
        _campaignBanner.Text = $"PIPELINE 战斗演示　｜　{CampaignController.ProgressText}";
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
