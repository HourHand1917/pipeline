using Godot;
using Godot.Collections;
using System.Collections.Generic;

[GlobalClass]
public partial class UIManager : Node
{
    private const int MAX_GRID_BUTTONS = 12;

    [Signal] public delegate void LightCellRequestedEventHandler(Vector2I position);
    [Signal] public delegate void PlayCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void MoveRequestedEventHandler(int action);
    [Signal] public delegate void EndTurnRequestedEventHandler();
    [Signal] public delegate void BackToBuildRequestedEventHandler();

    private BoardManager boardManager;
    private BattleManager battleManager;

    public GridContainer BattleGrid { get; set; }
    public GridContainer EnergyGrid { get; set; }
    public VBoxContainer ActivationBox { get; set; }
    public VBoxContainer ReadyList { get; set; }
    public RichTextLabel LogLabel { get; set; }
    public Label StatusLabel { get; set; }
    public Label RoundLabel { get; set; }
    public Label PhaseLabel { get; set; }
    public Label ReadyLabel { get; set; }
    public Label DistanceLabel { get; set; }
    public HBoxContainer DistanceTrack { get; set; }
    public Button MoveBackButton { get; set; }
    public Button MoveForwardButton { get; set; }
    public Label MoveHint { get; set; }
    public TextureButton EndTurnButton { get; set; }
    public Button BackButton { get; set; }
    public TextureProgressBar HealthBar { get; set; }

    private List<GridCellButton> battleButtons = new();
    private List<TextureRect> energyBulbs = new();
    private Vector2I[] buttonPositions = new Vector2I[MAX_GRID_BUTTONS];
    private bool _isSetup;
    private int _lastCellCount = -1;
    private List<BaseButton> _actionButtons = new();

    private PlayerBattle _player;
    private EnemyManager _enemyManager;

    public Texture2D BulbOn { get; set; }
    public Texture2D BulbOff { get; set; }

    // ================================================================
    //  Setup
    // ================================================================

    public void Setup(BoardManager board, BattleManager battle)
    {
        if (_isSetup) DisconnectAll();

        boardManager = board;
        battleManager = battle;

        if (BattleGrid != null)
        {
            battleButtons.Clear();
            foreach (Node child in BattleGrid.GetChildren())
            {
                if (child is GridCellButton btn)
                    battleButtons.Add(btn);
            }

            for (int i = 0; i < MAX_GRID_BUTTONS; i++)
                buttonPositions[i] = new Vector2I(i % 4, i / 4);

            for (int i = 0; i < battleButtons.Count; i++)
            {
                int index = i;
                battleButtons[i].Pressed += () => EmitSignal(SignalName.LightCellRequested, buttonPositions[index]);
            }
        }

        if (EnergyGrid != null)
        {
            energyBulbs.Clear();
            foreach (Node child in EnergyGrid.GetChildren())
            {
                if (child is TextureRect bulb)
                    energyBulbs.Add(bulb);
            }
        }

        _actionButtons.Clear();
        if (MoveBackButton != null) { MoveBackButton.Pressed += () => EmitSignal(SignalName.MoveRequested, (int)BattleManager.MoveAction.Backward); _actionButtons.Add(MoveBackButton); }
        if (MoveForwardButton != null) { MoveForwardButton.Pressed += () => EmitSignal(SignalName.MoveRequested, (int)BattleManager.MoveAction.Forward); _actionButtons.Add(MoveForwardButton); }
        if (EndTurnButton != null) { EndTurnButton.Pressed += () => EmitSignal(SignalName.EndTurnRequested); _actionButtons.Add(EndTurnButton); }
        if (BackButton != null) { BackButton.Pressed += () => EmitSignal(SignalName.BackToBuildRequested); _actionButtons.Add(BackButton); }

        _isSetup = true;
    }

    // ================================================================
    //  绑定
    // ================================================================

    public void BindPlayer(PlayerBattle player)
    {
        if (player == null) return;
        _player = player;

        player.HealthChanged += (cur, max) =>
        {
            if (HealthBar != null) { HealthBar.MaxValue = max; HealthBar.Value = cur; }
            UpdateStatusLine();
        };
        player.ShieldChanged += (_) => UpdateStatusLine();
        player.EnergyChanged += (cur, max) => RefreshEnergyBulbs(cur, max);
        player.PositionChanged += (_) => RefreshDistanceTrack();
        player.BuffApplied += (buff, stacks) => GD.Print($"[UIManager] 玩家获得 Buff：{buff.Get("buff_name")} ×{stacks}");
        player.BuffRemoved += (buffId) => GD.Print($"[UIManager] 玩家移除 Buff：{buffId}");
        player.Died += () => { if (StatusLabel != null) StatusLabel.Text = "玩家阵亡！"; };
    }

    public void BindEnemyManager(EnemyManager manager)
    {
        if (manager == null) return;
        _enemyManager = manager;
        manager.EnemySpawned += (enemy) => BindEnemy(enemy);
        manager.EnemyDied += (_) => { UpdateStatusLine(); RefreshDistanceTrack(); };
        manager.AllEnemiesDefeated += () => { if (StatusLabel != null) StatusLabel.Text += "　｜　敌人全灭！"; };
        foreach (var enemy in manager.Enemies) BindEnemy(enemy);
    }

    private void BindEnemy(EnemyBattle enemy)
    {
        if (enemy == null) return;
        enemy.HealthChanged += (cur, max) => UpdateStatusLine();
        enemy.ShieldChanged += (_) => UpdateStatusLine();
        enemy.PositionChanged += (_) => RefreshDistanceTrack();
        enemy.Died += () => { if (StatusLabel != null) StatusLabel.Text += $"　｜　{enemy.DisplayName}倒下！"; RefreshDistanceTrack(); };
    }

    private void UpdateStatusLine()
    {
        if (StatusLabel == null || _player == null) return;
        var enemy = _enemyManager?.GetPrimaryEnemy();
        StatusLabel.Text = string.Format("玩家 HP {0}/{1}　护盾 {2}　｜　{3} HP {4}/{5}　护盾 {6}",
            _player.CurrentHp, _player.MaxHp, _player.Shield,
            enemy?.DisplayName ?? "假人", enemy?.CurrentHp ?? battleManager.EnemyHp,
            enemy?.MaxHp ?? battleManager.EnemyMaxHp, enemy?.Shield ?? battleManager.EnemyShield);
    }

    public override void _ExitTree() { battleButtons.Clear(); _actionButtons.Clear(); _isSetup = false; }
    private void DisconnectAll() { _isSetup = false; }

    // ================================================================
    //  刷新
    // ================================================================

    public void RefreshAll(int gridWidth, int gridHeight)
    {
        if (boardManager == null || battleManager == null) return;
        RefreshBoard(gridWidth, gridHeight);
        RefreshStatus();
        RefreshDistanceTrack();
        RefreshActivationButtons();
    }

    private void RefreshBoard(int gridWidth, int gridHeight)
    {
        bool canLight = battleManager.CurrentPhase == BattleManager.Phase.PlayerTurn;

        for (int i = 0; i < MAX_GRID_BUTTONS; i++)
        {
            var pos = buttonPositions[i];
            var btn = battleButtons[i];

            if (pos.X >= gridWidth || pos.Y >= gridHeight)
            {
                btn.SetText("");
                btn.SetState(CellState.Disabled);
                continue;
            }

            var cell = boardManager.GetCell(pos);
            var runtime = boardManager.GetCardByCell(pos);

            if (runtime == null)
            {
                btn.SetText("");
                btn.SetState(CellState.Normal);
                btn.Disabled = true;
                continue;
            }

            btn.Disabled = !canLight;

            var data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
            bool isLit = cell.Get(GDScriptKeys.CellRuntime.IsLit).AsBool();
            int cooldown = runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32();

            string text = $"{data.Get(GDScriptKeys.CardData.IconText)}\n{data.Get(GDScriptKeys.CardData.DisplayName)}";

            if (cooldown > 0)
            {
                btn.SetState(CellState.Cooldown);
                text += $"\n冷却{cooldown}";
            }
            else if (isLit)
            {
                btn.SetState(CellState.Charged);
                text += "\n已点亮";
            }
            else
            {
                btn.SetState(CellState.Normal);
                text += "\n未点亮";
            }

            btn.SetText(text);
        }
    }

    private void RefreshStatus()
    {
        UpdateStatusLine();
        if (RoundLabel != null) RoundLabel.Text = $"回合 {battleManager.RoundNumber}";
        if (PhaseLabel != null) PhaseLabel.Text = PhaseText();
        if (DistanceLabel != null) DistanceLabel.Text = $"玩家 {battleManager.PlayerMapPosition} · 怪物 {battleManager.EnemyMapPosition} · 距离 {battleManager.Distance} 格";
        if (MoveHint != null) { var rules = DataManager.Instance.GetRules(); int cost = rules?.Get(GDScriptKeys.GameRules.MoveEnergyCost).AsInt32() ?? 1; int step = rules?.Get(GDScriptKeys.GameRules.PlayerMoveStep).AsInt32() ?? 1; MoveHint.Text = $"移动{step}格消耗 {cost} ⚡"; }

        bool canAct = battleManager.CurrentPhase == BattleManager.Phase.PlayerTurn;
        if (MoveBackButton != null) { MoveBackButton.Text = "← 后退"; MoveBackButton.Disabled = !canAct || !battleManager.CanPlayerMove((int)BattleManager.MoveAction.Backward); }
        if (MoveForwardButton != null) { MoveForwardButton.Text = "前进 →"; MoveForwardButton.Disabled = !canAct || !battleManager.CanPlayerMove((int)BattleManager.MoveAction.Forward); }
        if (EndTurnButton != null) EndTurnButton.Disabled = !canAct;
    }

    private void RefreshEnergyBulbs(int current, int max)
    {
        for (int i = 0; i < energyBulbs.Count; i++)
        {
            if (energyBulbs[i] == null) continue;
            energyBulbs[i].Texture = i < current ? BulbOn : BulbOff;
        }
    }

    private void RefreshDistanceTrack()
    {
        if (DistanceTrack == null || battleManager == null) return;

        var rules = DataManager.Instance.GetRules();
        var battleMap = rules?.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int cellCount = battleMap?.Get(GDScriptKeys.BattleMap.CellCount).AsInt32() ?? 7;

        if (cellCount != _lastCellCount)
        {
            _lastCellCount = cellCount;
            foreach (Node child in DistanceTrack.GetChildren()) child.QueueFree();

            for (int i = 1; i <= cellCount; i++)
            {
                var cell = new PanelContainer();
                cell.CustomMinimumSize = new Vector2(144, 144);
                var vbox = new VBoxContainer();
                cell.AddChild(vbox);
                var occupant = new Control();
                occupant.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                occupant.SizeFlagsHorizontal = Control.SizeFlags.Fill;
                var glyphLabel = new Label();
                glyphLabel.HorizontalAlignment = HorizontalAlignment.Center;
                glyphLabel.VerticalAlignment = VerticalAlignment.Center;
                glyphLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                glyphLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill;
                glyphLabel.AddThemeFontSizeOverride("font_size", 64);
                occupant.AddChild(glyphLabel);
                vbox.AddChild(occupant);
                var label = new Label();
                label.Text = $"{i}";
                label.HorizontalAlignment = HorizontalAlignment.Center;
                vbox.AddChild(label);
                DistanceTrack.AddChild(cell);
            }
        }

        string playerGlyph = _player?.Glyph ?? "旅";
        Color playerTint = _player?.Tint ?? new Color("#f4cf61");
        string enemyGlyph = "怪";
        Color enemyTint = new Color("#e66c62");
        var primaryEnemy = _enemyManager?.GetPrimaryEnemy();
        if (primaryEnemy != null) { enemyGlyph = !string.IsNullOrEmpty(primaryEnemy.Glyph) ? primaryEnemy.Glyph : "怪"; enemyTint = primaryEnemy.Tint; }

        var trackChildren = DistanceTrack.GetChildren();
        for (int i = 0; i < trackChildren.Count; i++)
        {
            int cellNum = i + 1;
            var panel = trackChildren[i] as PanelContainer;
            if (panel == null) continue;
            var vbox = panel.GetChild(0) as VBoxContainer;
            if (vbox == null || vbox.GetChildCount() < 2) continue;
            var occupant = vbox.GetChild(0) as Control;
            var glyphLabel = occupant?.GetChildCount() > 0 ? occupant.GetChild(0) as Label : null;
            if (glyphLabel == null) continue;

            if (cellNum == battleManager.PlayerMapPosition && cellNum == battleManager.EnemyMapPosition)
            { occupant.Modulate = new Color(0.6f, 0.3f, 0.3f); glyphLabel.Text = $"{playerGlyph}/{enemyGlyph}"; glyphLabel.Modulate = Colors.White; }
            else if (cellNum == battleManager.PlayerMapPosition)
            { occupant.Modulate = new Color(playerTint.R, playerTint.G, playerTint.B, 0.3f); glyphLabel.Text = playerGlyph; glyphLabel.Modulate = playerTint; }
            else if (cellNum == battleManager.EnemyMapPosition)
            { occupant.Modulate = new Color(enemyTint.R, enemyTint.G, enemyTint.B, 0.3f); glyphLabel.Text = enemyGlyph; glyphLabel.Modulate = enemyTint; }
            else
            { occupant.Modulate = new Color(1, 1, 1, 1); glyphLabel.Text = ""; glyphLabel.Modulate = Colors.White; }
        }
    }

    private void RefreshActivationButtons()
    {
        if (ActivationBox != null) { foreach (Node child in ActivationBox.GetChildren()) child.QueueFree(); }
        if (ReadyList != null) { foreach (Node child in ReadyList.GetChildren()) child.QueueFree(); }

        int readyCount = 0;
        foreach (var runtime in boardManager.runtime_cards)
        {
            if (!boardManager.CheckCardReady(runtime)) continue;
            readyCount++;
            var data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
            var button = new Button();
            button.Text = $"发动｜{data.Get(GDScriptKeys.CardData.DisplayName)}";
            button.Disabled = battleManager.CurrentPhase != BattleManager.Phase.PlayerTurn;
            int id = runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32();
            button.Pressed += () => EmitSignal(SignalName.PlayCardRequested, id);
            ActivationBox.AddChild(button);
        }
        if (ReadyLabel != null) ReadyLabel.Text = $"{readyCount} 张就绪";
    }

    public void AppendLog(string text) { if (LogLabel != null) { LogLabel.AppendText("• " + text + "\n"); LogLabel.ScrollToLine(Mathf.Max(0, LogLabel.GetLineCount() - 1)); } }
    public void ClearLog() { if (LogLabel != null) LogLabel.Clear(); }

    private string PhaseText() => battleManager.CurrentPhase switch
    {
        BattleManager.Phase.Build => "构筑阶段",
        BattleManager.Phase.PlayerTurn => "玩家回合",
        BattleManager.Phase.EnemyTurn => "敌方回合",
        BattleManager.Phase.BattleEnd => "战斗结束",
        _ => "状态切换"
    };
}