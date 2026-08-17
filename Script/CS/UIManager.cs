using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

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
    public PackedScene TrackSlotScene { get; set; }
    public PlayerTV PlayerTV { get; set; }
    public GridContainer PlayerBuffGrid { get; set; }
    public PackedScene BuffShowScene { get; set; }

    private List<GridCellButton> battleButtons = new();
    private List<TextureRect> energyBulbs = new();
    private Vector2I[] buttonPositions = new Vector2I[MAX_GRID_BUTTONS];
    private bool _isSetup;
    private int _lastCellCount = -1;
    private List<BaseButton> _actionButtons = new();

    private PlayerBattle _player;
    private EnemyManager _enemyManager;
    private Node _dangerPredictor;
    private Label _globalWarningLabel;

    private static readonly Color AccentCyan = new("#74e8ec");
    private static readonly Color AccentAmber = new("#f1c453");
    private static readonly Color MutedText = new("#a9bbc1");

    public Texture2D BulbOn { get; set; }
    public Texture2D BulbOff { get; set; }
    public AnimatedSprite2D RubberHeart { get; set; }

    // ================================================================
    //  Setup
    // ================================================================

    public void Setup(BoardManager board, BattleManager battle)
    {
        if (_isSetup) DisconnectAll();

        boardManager = board;
        battleManager = battle;
        EnsureDangerUi();
        ApplyLightweightTheme();

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
            UpdateHeartbeat(cur, max);
            UpdateStatusLine();
        };
        player.ShieldChanged += (_) => UpdateStatusLine();
        player.EnergyChanged += (cur, max) => RefreshEnergyBulbs(cur, max);
        player.PositionChanged += (_) => RefreshDistanceTrack();
        player.BuffApplied += (buff, stacks) => RefreshPlayerBuffs();
        player.BuffRemoved += (buffId) => RefreshPlayerBuffs();
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
        enemy.IntentChanged += (_) => RefreshDistanceTrack();
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
        RefreshPlayerBuffs();
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
        if (DistanceLabel != null)
        {
            var focusedEnemy = PlayerTV?.GetTrackedEnemy();
            if (focusedEnemy == null || !focusedEnemy.IsAlive)
                focusedEnemy = _enemyManager?.GetPrimaryEnemy();
            int distance = focusedEnemy == null ? 0 : battleManager.GetDistanceTo(focusedEnemy);
            DistanceLabel.Text = focusedEnemy == null
                ? $"玩家 {battleManager.PlayerMapPosition} · 暂无目标"
                : $"玩家 {battleManager.PlayerMapPosition} · {focusedEnemy.DisplayName} {focusedEnemy.MapPosition} · 距离 {distance} 格";
        }
        if (MoveHint != null)
        {
            var rules = battleManager.GetRules();
            int cost = rules?.Get(GDScriptKeys.GameRules.MoveEnergyCost).AsInt32() ?? 1;
            int step = rules?.Get(GDScriptKeys.GameRules.PlayerMoveStep).AsInt32() ?? 1;
            MoveHint.Text = $"移动{step}格消耗 {cost} ⚡";
        }

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
    if (DistanceTrack == null || battleManager == null || TrackSlotScene == null) return;

    var rules = battleManager.GetRules();
    if (rules == null) return;

    var battleMap = rules?.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
    int cellCount = battleMap?.Get(GDScriptKeys.BattleMap.CellCount).AsInt32() ?? 7;

    if (cellCount != _lastCellCount)
    {
        _lastCellCount = cellCount;

        foreach (Node child in DistanceTrack.GetChildren())
        {
            DistanceTrack.RemoveChild(child);
            child.QueueFree();
        }

        for (int i = 1; i <= cellCount; i++)
        {
            var slot = TrackSlotScene.Instantiate<TrackSlot>();
            slot.CustomMinimumSize = new Vector2(144, 144);
            slot.Configure(i, "", Colors.White, null);
            slot.SlotClicked += OnTrackSlotClicked;
            DistanceTrack.AddChild(slot);
        }
    }

    string playerGlyph = _player?.Glyph ?? "旅";
    Color playerTint = _player?.Tint ?? new Color("#f4cf61");

    var slots = DistanceTrack.GetChildren();
    for (int i = 0; i < slots.Count; i++)
    {
        var slot = slots[i] as TrackSlot;
        if (slot == null) continue;
        int cellNum = i + 1;

        bool isPlayer = cellNum == battleManager.PlayerMapPosition;
        EnemyBattle enemyHere = null;
        if (_enemyManager != null)
        {
            foreach (var enemy in _enemyManager.Enemies)
            {
                if (enemy != null && enemy.IsAlive && enemy.MapPosition == cellNum)
                { enemyHere = enemy; break; }
            }
        }
        bool isEnemy = enemyHere != null;

        if (isPlayer && isEnemy)
            slot.Configure(cellNum, $"{playerGlyph}/{enemyHere.Glyph}", Colors.White, enemyHere as GodotObject);
        else if (isPlayer)
            slot.Configure(cellNum, playerGlyph, playerTint, _player as GodotObject);
        else if (isEnemy)
            slot.Configure(cellNum, enemyHere.Glyph, enemyHere.Tint, enemyHere as GodotObject);
        else
            slot.Configure(cellNum, "", Colors.White, null);
    }

    ApplyDangerPrediction(cellCount, slots);
    RefreshTrackedEnemyPanel();
}

        private void OnTrackSlotClicked(int cellNumber, GodotObject combatant)
    {
        if (combatant == null)
        {
            PlayerTV?.ClearEnemyPanel();
            return;
        }

        var enemy = combatant as EnemyBattle;
        if (enemy == null)
        {
            PlayerTV?.ClearEnemyPanel();
            return;
        }

        ShowEnemyIntent(enemy);
    }

    /// <summary>
    /// Read-only intent projection. The action has already been selected and cached by
    /// BattleManager; UI refreshes must never call get_action_for_distance/select_action.
    /// </summary>
    private void ShowEnemyIntent(EnemyBattle enemy)
    {
        if (enemy == null || !enemy.IsAlive || battleManager == null) return;
        var action = battleManager.GetPlannedAction(enemy);
        string actionName = action?.Get("display_name").AsString() ?? "观察局势";
        string intent = action?.Get("intent_text").AsString() ?? "尚未锁定攻击区域";
        int distance = battleManager.GetDistanceTo(enemy);
        PlayerTV?.UpdateEnemyPanel(enemy, actionName, $"距离 {distance} 格｜{intent}");
    }

    private void RefreshTrackedEnemyPanel()
    {
        var tracked = PlayerTV?.GetTrackedEnemy();
        if (tracked == null) return;
        if (!tracked.IsAlive)
        {
            PlayerTV.ClearEnemyPanel();
            return;
        }
        ShowEnemyIntent(tracked);
    }

    private void EnsureDangerUi()
    {
        if (_dangerPredictor == null || !GodotObject.IsInstanceValid(_dangerPredictor))
        {
            var scene = GD.Load<PackedScene>("res://features/combat_prediction/scenes/danger_area_predictor.tscn");
            if (scene != null)
            {
                _dangerPredictor = scene.Instantiate();
                _dangerPredictor.Name = "DangerAreaPredictorUI";
                AddChild(_dangerPredictor);
            }
        }

        if (_globalWarningLabel == null && DistanceTrack?.GetParent() is Container parent)
        {
            _globalWarningLabel = new Label
            {
                Name = "DangerGlobalWarning",
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = false,
                Text = "",
                CustomMinimumSize = new Vector2(0, 34),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _globalWarningLabel.AddThemeFontSizeOverride("font_size", 21);
            _globalWarningLabel.AddThemeColorOverride("font_color", AccentAmber);
            _globalWarningLabel.AddThemeColorOverride("font_outline_color", new Color("#28150f"));
            _globalWarningLabel.AddThemeConstantOverride("outline_size", 5);
            parent.AddChild(_globalWarningLabel);
            parent.MoveChild(_globalWarningLabel, DistanceTrack.GetIndex() + 1);
        }
    }

    private void ApplyDangerPrediction(int cellCount, Godot.Collections.Array<Node> slots)
    {
        var currentCells = new HashSet<int>();
        var futureCells = new HashSet<int>();
        var warnings = new HashSet<string>();

        if (_dangerPredictor != null && _enemyManager != null && _player != null)
        {
            int[] occupied = _enemyManager.Enemies
                .Where(enemy => enemy != null && enemy.IsAlive)
                .Select(enemy => enemy.MapPosition)
                .Distinct()
                .ToArray();

            foreach (var enemy in _enemyManager.Enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                var action = battleManager.GetPlannedAction(enemy);
                if (action == null) continue;

                var prediction = _dangerPredictor.Call(
                    "predict_action", action, enemy.MapPosition, enemy.Facing,
                    _player.MapPosition, cellCount, occupied).AsGodotDictionary();

                if (prediction.ContainsKey("current_cells"))
                    foreach (int cell in prediction["current_cells"].AsInt32Array()) currentCells.Add(cell);
                if (prediction.ContainsKey("future_cells"))
                    foreach (int cell in prediction["future_cells"].AsInt32Array()) futureCells.Add(cell);
                if (prediction.ContainsKey("global_warning"))
                {
                    string warning = prediction["global_warning"].AsString();
                    if (!string.IsNullOrWhiteSpace(warning)) warnings.Add(warning.Trim());
                }
            }
        }

        foreach (Node node in slots)
        {
            if (node is TrackSlot slot)
                slot.SetDangerState(currentCells.Contains(slot.CellNumber), futureCells.Contains(slot.CellNumber));
        }

        if (_globalWarningLabel != null)
        {
            _globalWarningLabel.Text = warnings.Count == 0 ? "" : $"⚠ 全局危险：{string.Join(" ｜ ", warnings)}";
            _globalWarningLabel.Visible = warnings.Count > 0;
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
            button.CustomMinimumSize = new Vector2(230, 44);
            button.AddThemeFontSizeOverride("font_size", 19);
            button.AddThemeColorOverride("font_color", AccentCyan);
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color("#162c33"), new Color("#427c83"), 2));
            button.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color("#24515a"), AccentCyan, 2));
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

    private void ApplyLightweightTheme()
    {
        RoundLabel?.AddThemeColorOverride("font_color", AccentAmber);
        PhaseLabel?.AddThemeColorOverride("font_color", AccentCyan);
        DistanceLabel?.AddThemeColorOverride("font_color", Colors.White);
        MoveHint?.AddThemeColorOverride("font_color", MutedText);
        ReadyLabel?.AddThemeColorOverride("font_color", AccentCyan);
        StatusLabel?.AddThemeColorOverride("font_color", new Color("#d9f3f1"));

        foreach (var button in new Button[] { MoveBackButton, MoveForwardButton, BackButton })
        {
            if (button == null) continue;
            button.AddThemeColorOverride("font_color", Colors.White);
            button.AddThemeColorOverride("font_hover_color", AccentCyan);
            button.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color("#18262b"), new Color("#53666b"), 2));
            button.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color("#27434a"), AccentCyan, 2));
        }
    }

    private static StyleBoxFlat MakeButtonStyle(Color background, Color border, int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
    }

    private void UpdateHeartbeat(int currentHp, int maxHp)
    {
        if (RubberHeart == null) return;

        if (currentHp <= 0)
        {
            RubberHeart.Stop();
            return;
        }

        float ratio = (float)currentHp / maxHp;

        float fps;
        if (ratio > 0.5f)
            fps = 8f;
        else if (ratio > 0.25f)
            fps = 10f;
        else
            fps = 12f;

        RubberHeart.SpeedScale = fps / 8f;
        if (!RubberHeart.IsPlaying())
            RubberHeart.Play("idle");
    }

private void RefreshPlayerBuffs()
{
    if (PlayerBuffGrid == null || BuffShowScene == null || _player == null) return;

    foreach (Node child in PlayerBuffGrid.GetChildren())
    {
        PlayerBuffGrid.RemoveChild(child);
        child.QueueFree();
    }

    var collectedBuffs = new System.Collections.Generic.Dictionary<string, (GodotObject buff, int stacks)>();

    var playerStats = _player.GetStats();
    if (playerStats != null)
    {
        var buffs = playerStats.Get("buffs").As<Array>();
        CollectBuffs(buffs, collectedBuffs);
    }

    if (boardManager != null)
    {
        foreach (var runtime in boardManager.runtime_cards)
        {
            var cells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
            foreach (Vector2I pos in cells)
            {
                var cell = boardManager.GetCell(pos);
                var cellStats = cell?.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>();
                if (cellStats == null) continue;
                var buffs = cellStats.Get("buffs").As<Array>();
                CollectBuffs(buffs, collectedBuffs);
            }
        }
    }

    foreach (var pair in collectedBuffs.Values)
    {
        var icon = pair.buff.Get(GDScriptKeys.Buff.Icon).As<Texture2D>();
        var buffName = pair.buff.Get(GDScriptKeys.Buff.BuffName).AsString();
        var description = pair.buff.Get(GDScriptKeys.Buff.Description).AsString();
        var slot = BuffShowScene.Instantiate<BuffShow>();
        slot.Setup(icon, pair.stacks, buffName, description);
        PlayerBuffGrid.AddChild(slot);
    }
}

private void CollectBuffs(Array buffs, System.Collections.Generic.Dictionary<string, (GodotObject buff, int stacks)> collected)
{
    foreach (var bi in buffs)
    {
        if (bi.Obj == null) continue;
        var instance = bi.As<GodotObject>();
        var buff = instance.Get("buff").As<GodotObject>();
        string id = buff.Get(GDScriptKeys.Buff.Id).AsString();
        int stacks = instance.Get("stacks").AsInt32();

        if (collected.ContainsKey(id))
            collected[id] = (buff, collected[id].stacks + stacks);
        else
            collected[id] = (buff, stacks);
    }
}
}
