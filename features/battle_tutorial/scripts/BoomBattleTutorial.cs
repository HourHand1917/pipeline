using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

/// <summary>
/// Six-step, state-verified tutorial for the single-Boom encounter.
/// The overlay never replaces battle controls: four blockers leave one real
/// control exposed, so the player learns the production interaction itself.
/// </summary>
[GlobalClass]
public partial class BoomBattleTutorial : CanvasLayer
{
    [Signal] public delegate void TutorialStepChangedEventHandler(int step, string instruction);
    [Signal] public delegate void TutorialCompletedEventHandler();

    public enum TutorialStep
    {
        LightCells = 0,
        UseLitCards = 1,
        MoveOnTrack = 2,
        SwitchEnemyPanel = 3,
        InspectEnemy = 4,
        EndTurn = 5,
        Complete = 6,
    }

    [ExportGroup("Activation")]
    [Export] public bool TutorialEnabled { get; set; } = true;
    [Export] public bool RequireSingleBoomEncounter { get; set; } = true;

    [ExportGroup("Presentation")]
    [Export(PropertyHint.Range, "0,40,1")] public float TargetPadding { get; set; } = 12f;
    [Export(PropertyHint.Range, "0,0.9,0.01")] public float DimOpacity { get; set; } = 0.66f;

    [ExportGroup("Guide text")]
    [Export] public string LightCellsTitle { get; set; } = "点亮格子";
    [Export] public string LightCellsInstruction { get; set; } = "点击高亮格子，消耗能量点亮卡牌。";
    [Export] public string UseCardsTitle { get; set; } = "使用已点亮卡牌";
    [Export] public string UseCardsInstruction { get; set; } = "卡牌全部点亮后，再点击卡牌即可使用。";
    [Export] public string MoveTitle { get; set; } = "点击格子移动";
    [Export] public string MoveInstruction { get; set; } = "点击轨道上的空格，移动玩家位置。";
    [Export] public string SwitchPanelTitle { get; set; } = "切换敌人面板";
    [Export] public string SwitchPanelInstruction { get; set; } = "点击上箭头，切换到敌人信息面板。";
    [Export] public string InspectEnemyTitle { get; set; } = "观察血量与意图";
    [Export] public string InspectEnemyInstruction { get; set; } = "点击 Boom 脚下，查看血量与行动意图。";
    [Export] public string EndTurnTitle { get; set; } = "结束回合";
    [Export] public string EndTurnInstruction { get; set; } = "行动完成后，点击结束回合，让敌人开始行动。";

    public bool IsTutorialActive => _active;
    public TutorialStep CurrentTutorialStep => _step;
    public Control CurrentTarget => _target;
    public Rect2 CurrentInputRect => _inputRect;

    private BattleManager _battle;
    private BattleScreen _screen;
    private BoardManager _board;
    private UIManager _ui;
    private PlayerBattle _player;
    private EnemyBattle _boom;

    private Control _root;
    private readonly ColorRect[] _blockers = new ColorRect[4];
    private Panel _highlight;
    private PanelContainer _guideBubble;
    private Label _stepLabel;
    private Label _titleLabel;
    private Label _bodyLabel;
    private Label _arrowLabel;

    private TutorialStep _step = TutorialStep.LightCells;
    private Control _target;
    private Rect2 _targetRect;
    private Rect2 _inputRect;
    private int _tutorialCardId = -1;
    private readonly HashSet<int> _readyCardIds = new();
    private int _moveStartCell;
    private int _endTurnStartRound;
    private bool _bound;
    private bool _active;
    private bool _activationAttempted;
    private bool _completedForEncounter;

    public override void _Ready()
    {
        Layer = 90;
        ProcessMode = ProcessModeEnum.Always;
        CreateOverlay();
        Visible = false;
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _Process(double delta)
    {
        if (!TutorialEnabled) return;
        if (!_bound) TryBind();
        if (!_bound) return;

        if (!_active)
        {
            if (!_completedForEncounter && !_activationAttempted
                && _battle.CurrentPhase == BattleManager.Phase.PlayerTurn && IsBoomEncounter())
                StartTutorial();
            return;
        }

        if (_battle.CurrentPhase == BattleManager.Phase.BattleEnd)
        {
            FinishTutorial();
            return;
        }

        EvaluateCurrentStep();
        ResolveTarget();
        LayoutOverlay();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_active || !Visible) return;

        // ESC：直接退出教学，恢复战斗操作
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            FinishTutorial();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton mouse)
        {
            if (!_inputRect.HasPoint(mouse.Position))
                GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventScreenTouch touch)
        {
            if (!_inputRect.HasPoint(touch.Position))
                GetViewport().SetInputAsHandled();
            return;
        }

        // Tutorial steps are mouse interactions. Suppress unrelated gameplay
        // shortcuts until the highlighted production control is used.
        if (@event is InputEventKey key2 && key2.Pressed)
            GetViewport().SetInputAsHandled();
    }

    private void TryBind()
    {
        Node scope = GetParent() ?? GetTree().CurrentScene;
        _battle = FindDescendant<BattleManager>(scope);
        _screen = FindDescendant<BattleScreen>(scope);
        if (_battle == null || _screen == null || _screen.UIManager == null || _battle.BoardManager == null)
            return;

        _board = _battle.BoardManager;
        _ui = _screen.UIManager;
        _bound = true;
    }

    private bool IsBoomEncounter()
    {
        _player = _battle.Player;
        if (_player == null || _battle.EnemyManager == null || _ui.BattleGrid == null || _ui.DistanceTrack == null)
            return false;

        int living = 0;
        _boom = null;
        foreach (EnemyBattle enemy in _battle.EnemyManager.Enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            living++;
            if (string.Equals(enemy.EnemyId, "boom", StringComparison.OrdinalIgnoreCase))
                _boom = enemy;
        }

        return _boom != null && (!RequireSingleBoomEncounter || living == 1);
    }

    private void StartTutorial()
    {
        _activationAttempted = true;
        if (!SelectTutorialCard())
        {
            GD.PushWarning("BoomBattleTutorial：没有找到可在保留移动能量后点亮的卡牌，教程未启动。");
            return;
        }

        _active = true;
        Visible = true;
        EnterStep(TutorialStep.LightCells);
    }

    private bool SelectTutorialCard()
    {
        int moveCost = 1;
        GodotObject rules = _battle.GetRules();
        if (rules != null)
            moveCost = Mathf.Max(0, rules.Get(GDScriptKeys.GameRules.MoveEnergyCost).AsInt32());

        int lightBudget = Mathf.Max(1, _player.Energy - moveCost);
        GodotObject best = null;
        int bestScore = int.MaxValue;
        foreach (GodotObject runtime in _board.runtime_cards)
        {
            if (runtime == null || runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32() > 0)
                continue;
            Array<Vector2I> cells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
            if (cells.Count == 0 || cells.Count > lightBudget) continue;

            GodotObject data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
            if (!CanSafelyExecuteTutorialCard(data, out int actionScore)) continue;
            int score = actionScore * 100 + cells.Count * 10;
            if (score >= bestScore) continue;
            best = runtime;
            bestScore = score;
        }

        if (best == null) return false;
        _tutorialCardId = best.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32();
        return true;
    }

    private bool CanSafelyExecuteTutorialCard(GodotObject data, out int actionScore)
    {
        actionScore = int.MaxValue;
        if (data == null || _player == null || _boom == null) return false;

        bool hasDamage = data.Call(GDScriptKeys.CardData.HasDamageEffect).AsBool();
        Godot.Collections.Array effects = data.Get(GDScriptKeys.CardData.Effects).As<Godot.Collections.Array>();
        if (hasDamage)
        {
            int minRange = data.Get(GDScriptKeys.CardData.MinRange).AsInt32();
            int maxRange = data.Get(GDScriptKeys.CardData.MaxRange).AsInt32();
            int facing = _boom.MapPosition > _player.MapPosition
                ? BattleManager.FacingPositive
                : BattleManager.FacingNegative;
            if (!BattleManager.IsInRange(_player.MapPosition, _boom.MapPosition, facing, minRange, maxRange))
                return false;

            int damage = 0;
            foreach (Variant effectVariant in effects)
            {
                GodotObject effect = effectVariant.As<GodotObject>();
                if (effect != null && effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString() == "damage")
                    damage += effect.Get(GDScriptKeys.CombatEffect.Amount).AsInt32();
            }
            if (damage == 0 && data.Get(GDScriptKeys.CardData.EffectType).AsString() == "damage")
                damage = data.Get(GDScriptKeys.CardData.EffectValue).AsInt32();
            damage += _player.StrengthThisTurn;
            if (damage <= 0 || damage >= _boom.CurrentHp + _boom.Shield) return false;
            actionScore = 3;
            return true;
        }

        bool hasGuaranteedEffect = false;
        int bestEffectScore = int.MaxValue;
        foreach (Variant effectVariant in effects)
        {
            GodotObject effect = effectVariant.As<GodotObject>();
            if (effect == null) continue;
            string type = effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString();
            switch (type)
            {
                case "energy":
                    hasGuaranteedEffect = true;
                    bestEffectScore = Math.Min(bestEffectScore, 0);
                    break;
                case "shield":
                case "apply_buff":
                    hasGuaranteedEffect = true;
                    bestEffectScore = Math.Min(bestEffectScore, 1);
                    break;
                case "heal" when _player.CurrentHp < _player.MaxHp:
                    hasGuaranteedEffect = true;
                    bestEffectScore = Math.Min(bestEffectScore, 2);
                    break;
            }
        }

        if (effects.Count == 0)
        {
            string legacyType = data.Get(GDScriptKeys.CardData.EffectType).AsString();
            if (legacyType == "energy") { hasGuaranteedEffect = true; bestEffectScore = 0; }
            else if (legacyType == "shield") { hasGuaranteedEffect = true; bestEffectScore = 1; }
            else if (legacyType == "heal" && _player.CurrentHp < _player.MaxHp)
            { hasGuaranteedEffect = true; bestEffectScore = 2; }
        }

        if (!hasGuaranteedEffect) return false;
        actionScore = bestEffectScore;
        return true;
    }

    private void EvaluateCurrentStep()
    {
        switch (_step)
        {
            case TutorialStep.LightCells:
            {
                GodotObject runtime = _board.GetRuntimeCard(_tutorialCardId);
                if (runtime != null && _board.CheckCardReady(runtime))
                    EnterStep(TutorialStep.UseLitCards);
                break;
            }
            case TutorialStep.UseLitCards:
            {
                RefreshReadyCardSet();
                if (_readyCardIds.Count == 0)
                {
                    _moveStartCell = _player.MapPosition;
                    EnterStep(TutorialStep.MoveOnTrack);
                }
                break;
            }
            case TutorialStep.MoveOnTrack:
                if (_player.MapPosition != _moveStartCell)
                    EnterStep(TutorialStep.SwitchEnemyPanel);
                break;
            case TutorialStep.SwitchEnemyPanel:
                if (GetEnemyPanel()?.Visible == true)
                    EnterStep(TutorialStep.InspectEnemy);
                break;
            case TutorialStep.InspectEnemy:
                if (_ui.PlayerTV?.GetTrackedEnemy() == _boom)
                    EnterStep(TutorialStep.EndTurn);
                break;
            case TutorialStep.EndTurn:
                if (_battle.CurrentPhase != BattleManager.Phase.PlayerTurn
                    || _battle.RoundNumber > _endTurnStartRound)
                    FinishTutorial();
                break;
        }
    }

    private void EnterStep(TutorialStep next)
    {
        _step = next;
        if (next == TutorialStep.UseLitCards)
        {
            _readyCardIds.Clear();
            RefreshReadyCardSet();
        }
        else if (next == TutorialStep.EndTurn)
        {
            _endTurnStartRound = _battle.RoundNumber;
        }

        string instruction = next switch
        {
            TutorialStep.LightCells => LightCellsInstruction,
            TutorialStep.UseLitCards => UseCardsInstruction,
            TutorialStep.MoveOnTrack => MoveInstruction,
            TutorialStep.SwitchEnemyPanel => SwitchPanelInstruction,
            TutorialStep.InspectEnemy => InspectEnemyInstruction,
            TutorialStep.EndTurn => EndTurnInstruction,
            _ => "",
        };
        EmitSignal(SignalName.TutorialStepChanged, (int)next, instruction);
        ResolveTarget();
        UpdateGuideText(instruction);
    }

    private void RefreshReadyCardSet()
    {
        var stillReady = new HashSet<int>();
        foreach (GodotObject runtime in _board.runtime_cards)
        {
            if (runtime == null || !_board.CheckCardReady(runtime)) continue;
            int id = runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32();
            stillReady.Add(id);
        }
        _readyCardIds.Clear();
        foreach (int id in stillReady) _readyCardIds.Add(id);
    }

    private void ResolveTarget()
    {
        _target = _step switch
        {
            TutorialStep.LightCells => FindNextUnlitCellButton(_tutorialCardId),
            TutorialStep.UseLitCards => FindReadyCardButton(),
            TutorialStep.MoveOnTrack => FindMoveTarget(),
            TutorialStep.SwitchEnemyPanel => _ui.PlayerTV?.GetNodeOrNull<Control>("UpBtn"),
            TutorialStep.InspectEnemy => FindEnemySlot(),
            TutorialStep.EndTurn => _ui.EndTurnButton,
            _ => null,
        };

        if (_target == null || !GodotObject.IsInstanceValid(_target) || !_target.IsVisibleInTree())
        {
            _targetRect = new Rect2();
            _inputRect = new Rect2();
        }
        else
        {
            _inputRect = ExpandAndClamp(_target.GetGlobalRect(), 0);
            _targetRect = ExpandAndClamp(_target.GetGlobalRect(), TargetPadding);
        }
    }

    private Control FindNextUnlitCellButton(int instanceId)
    {
        GodotObject runtime = _board.GetRuntimeCard(instanceId);
        if (runtime == null) return null;
        Array<Vector2I> occupied = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
        foreach (Vector2I position in occupied)
        {
            GodotObject cell = _board.GetCell(position);
            if (cell != null && !cell.Get(GDScriptKeys.CellRuntime.IsLit).AsBool())
                return GetBattleGridButton(position);
        }
        return occupied.Count > 0 ? GetBattleGridButton(occupied[0]) : null;
    }

    private Control FindReadyCardButton()
    {
        foreach (int id in _readyCardIds)
        {
            GodotObject runtime = _board.GetRuntimeCard(id);
            if (runtime == null || !_board.CheckCardReady(runtime)) continue;
            Array<Vector2I> occupied = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
            if (occupied.Count > 0) return GetBattleGridButton(occupied[0]);
        }
        return null;
    }

    private Control GetBattleGridButton(Vector2I position)
    {
        int index = position.Y * 4 + position.X;
        if (_ui.BattleGrid == null || index < 0 || index >= _ui.BattleGrid.GetChildCount()) return null;
        return _ui.BattleGrid.GetChild(index) as Control;
    }

    private TrackSlot FindMoveTarget()
    {
        if (_ui.DistanceTrack == null || _player == null) return null;
        TrackSlot best = null;
        int bestDistance = int.MaxValue;
        foreach (Node child in _ui.DistanceTrack.GetChildren())
        {
            if (child is not TrackSlot slot || slot.OccupantRef != null) continue;
            int distance = Math.Abs(slot.CellNumber - _player.MapPosition);
            if (distance == 0 || distance >= bestDistance) continue;
            best = slot;
            bestDistance = distance;
        }
        return best;
    }

    private TrackSlot FindEnemySlot()
    {
        if (_ui.DistanceTrack == null || _boom == null) return null;
        foreach (Node child in _ui.DistanceTrack.GetChildren())
            if (child is TrackSlot slot && slot.OccupantRef == _boom)
                return slot;
        return null;
    }

    private Control GetEnemyPanel()
        => _ui.PlayerTV?.GetNodeOrNull<Control>("PanelStack/EnemyPanel");

    private void FinishTutorial()
    {
        if (!_active) return;
        _active = false;
        _completedForEncounter = true;
        _step = TutorialStep.Complete;
        _target = null;
        _targetRect = new Rect2();
        _inputRect = new Rect2();
        Visible = false;
        EmitSignal(SignalName.TutorialCompleted);
    }

    private void CreateOverlay()
    {
        _root = new Control
        {
            Name = "TutorialOverlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        Color dim = new(0.015f, 0.025f, 0.035f, DimOpacity);
        for (int i = 0; i < _blockers.Length; i++)
        {
            _blockers[i] = new ColorRect
            {
                Name = $"InputBlocker{i}",
                Color = dim,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            _root.AddChild(_blockers[i]);
        }

        _highlight = new Panel
        {
            Name = "HighlightFrame",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var highlightStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.9f, 0.95f, 0.06f),
            BorderColor = new Color("#74e8ec"),
            BorderWidthLeft = 5,
            BorderWidthTop = 5,
            BorderWidthRight = 5,
            BorderWidthBottom = 5,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        _highlight.AddThemeStyleboxOverride("panel", highlightStyle);
        _root.AddChild(_highlight);

        _arrowLabel = new Label
        {
            Name = "GuideArrow",
            Text = "▼",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _arrowLabel.AddThemeFontSizeOverride("font_size", 36);
        _arrowLabel.AddThemeColorOverride("font_color", new Color("#f1c453"));
        _root.AddChild(_arrowLabel);

        _guideBubble = new PanelContainer
        {
            Name = "GuideBubble",
            CustomMinimumSize = new Vector2(470, 146),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var bubbleStyle = new StyleBoxFlat
        {
            BgColor = new Color("#101b20f2"),
            BorderColor = new Color("#74e8ec"),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 18,
            ContentMarginBottom = 18,
        };
        _guideBubble.AddThemeStyleboxOverride("panel", bubbleStyle);
        _root.AddChild(_guideBubble);

        var textStack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        textStack.AddThemeConstantOverride("separation", 6);
        _guideBubble.AddChild(textStack);

        _stepLabel = NewGuideLabel(16, new Color("#f1c453"));
        _titleLabel = NewGuideLabel(23, new Color("#74e8ec"));
        _bodyLabel = NewGuideLabel(21, Colors.White);
        _bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textStack.AddChild(_stepLabel);
        textStack.AddChild(_titleLabel);
        textStack.AddChild(_bodyLabel);
    }

    private static Label NewGuideLabel(int fontSize, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color("#071014"));
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    private void UpdateGuideText(string instruction)
    {
        _stepLabel.Text = $"BOOM 战斗教学  ·  {(int)_step + 1}/6";
        _titleLabel.Text = _step switch
        {
            TutorialStep.LightCells => LightCellsTitle,
            TutorialStep.UseLitCards => UseCardsTitle,
            TutorialStep.MoveOnTrack => MoveTitle,
            TutorialStep.SwitchEnemyPanel => SwitchPanelTitle,
            TutorialStep.InspectEnemy => InspectEnemyTitle,
            TutorialStep.EndTurn => EndTurnTitle,
            _ => "",
        };
        _bodyLabel.Text = instruction;
    }

    private void LayoutOverlay()
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (_inputRect.Size.X <= 0 || _inputRect.Size.Y <= 0)
        {
            SetRect(_blockers[0], new Rect2(Vector2.Zero, viewport));
            for (int i = 1; i < _blockers.Length; i++) SetRect(_blockers[i], new Rect2());
            _highlight.Visible = false;
            _arrowLabel.Visible = false;
            return;
        }

        _highlight.Visible = true;
        _arrowLabel.Visible = true;
        SetRect(_blockers[0], new Rect2(0, 0, viewport.X, _inputRect.Position.Y));
        SetRect(_blockers[1], new Rect2(0, _inputRect.Position.Y, _inputRect.Position.X, _inputRect.Size.Y));
        SetRect(_blockers[2], new Rect2(_inputRect.End.X, _inputRect.Position.Y,
            Mathf.Max(0, viewport.X - _inputRect.End.X), _inputRect.Size.Y));
        SetRect(_blockers[3], new Rect2(0, _inputRect.End.Y, viewport.X,
            Mathf.Max(0, viewport.Y - _inputRect.End.Y)));
        SetRect(_highlight, _targetRect);

        float pulse = 0.78f + Mathf.Sin(Time.GetTicksMsec() * 0.006f) * 0.22f;
        _highlight.SelfModulate = new Color(1, 1, 1, pulse);

        const float bubbleWidth = 520f;
        const float bubbleHeight = 154f;
        const float gap = 34f;
        bool placeBelow = _targetRect.End.Y + gap + bubbleHeight <= viewport.Y - 24;
        float bubbleY = placeBelow
            ? _targetRect.End.Y + gap
            : _targetRect.Position.Y - gap - bubbleHeight;
        float bubbleX = Mathf.Clamp(_targetRect.GetCenter().X - bubbleWidth * 0.5f,
            24f, Mathf.Max(24f, viewport.X - bubbleWidth - 24f));
        SetRect(_guideBubble, new Rect2(bubbleX, Mathf.Clamp(bubbleY, 24, viewport.Y - bubbleHeight - 24),
            bubbleWidth, bubbleHeight));

        _arrowLabel.Text = placeBelow ? "▲" : "▼";
        float arrowY = placeBelow ? _targetRect.End.Y + 2 : _targetRect.Position.Y - 34;
        SetRect(_arrowLabel, new Rect2(_targetRect.GetCenter().X - 24, arrowY, 48, 32));
    }

    private Rect2 ExpandAndClamp(Rect2 source, float padding)
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float left = Mathf.Clamp(source.Position.X - padding, 0, viewport.X);
        float top = Mathf.Clamp(source.Position.Y - padding, 0, viewport.Y);
        float right = Mathf.Clamp(source.End.X + padding, 0, viewport.X);
        float bottom = Mathf.Clamp(source.End.Y + padding, 0, viewport.Y);
        return new Rect2(left, top, Mathf.Max(0, right - left), Mathf.Max(0, bottom - top));
    }

    private static void SetRect(Control control, Rect2 rect)
    {
        control.Position = rect.Position;
        control.Size = new Vector2(Mathf.Max(0, rect.Size.X), Mathf.Max(0, rect.Size.Y));
    }

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root == null) return null;
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T result = FindDescendant<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
