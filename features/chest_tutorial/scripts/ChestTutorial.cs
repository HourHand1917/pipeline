using Godot;
using Godot.Collections;

/// <summary>
/// Three-step chest tutorial: click the chest, claim loot into the backpack,
/// then close the RewardPage. Only the real chest click area, the real
/// RewardPage list and its close button remain interactive.
/// </summary>
[GlobalClass]
public partial class ChestTutorial : CanvasLayer
{
    [Signal] public delegate void TutorialStepChangedEventHandler(int step, string instruction);
    [Signal] public delegate void TutorialCompletedEventHandler();

    public enum Step { ClickChest, CollectLoot, CloseChest, Complete }

    [ExportGroup("Activation")]
    [Export] public bool TutorialEnabled { get; set; } = true;
    [Export] public bool ShowOnlyOnce { get; set; } = true;
    [Export] public string TargetChestPersistenceId { get; set; } = "chest_f1_1";
    [Export] public string TutorialStateMapId { get; set; } = "__tutorials__";
    [Export] public string TutorialStateId { get; set; } = "chest_controls";
    [Export] public ChestInteractable Chest { get; set; }
    [Export] public RewardPage RewardPage { get; set; }

    [ExportGroup("Text")]
    [Export] public string ClickTitle { get; set; } = "打开宝箱";
    [Export] public string ClickInstruction { get; set; } = "靠近宝箱后，用鼠标左键点击宝箱。";
    [Export] public string CollectTitle { get; set; } = "获取物资";
    [Export] public string CollectInstruction { get; set; } = "点击清单里的物资，把它们收进背包。";
    [Export] public string CloseTitle { get; set; } = "关闭宝箱";
    [Export] public string CloseInstruction { get; set; } = "查看奖励后，点击关闭按钮回到探索。";

    [ExportGroup("Presentation")]
    [Export(PropertyHint.Range, "0,40,1")] public float TargetPadding { get; set; } = 12f;
    [Export(PropertyHint.Range, "0,0.9,0.01")] public float DimOpacity { get; set; } = 0.66f;

    public bool IsTutorialActive => _active;
    public Step CurrentTutorialStep => _step;
    public Rect2 CurrentInputRect => _inputRect;
    public Node CurrentTargetNode => _targetNode;

    private readonly ColorRect[] _blockers = new ColorRect[4];
    private Control _root;
    private Panel _highlight;
    private PanelContainer _guideBubble;
    private Label _stepLabel;
    private Label _titleLabel;
    private Label _bodyLabel;
    private Label _arrowLabel;
    private Node _targetNode;
    private Rect2 _targetRect;
    private Rect2 _inputRect;
    private Step _step;
    private bool _active;
    private bool _bound;
    private bool _movementLocked;

    public override void _Ready()
    {
        Layer = 91;
        ProcessMode = ProcessModeEnum.Always;
        CreateOverlay();
        Visible = false;
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _ExitTree()
    {
        Unbind();
        UnlockMovement();
    }

    public override void _Process(double delta)
    {
        if (!TutorialEnabled) return;
        if (!_bound) TryBind();
        if (!_bound || !_active) return;
        if (_step == Step.ClickChest && Chest.IsOpened)
        {
            EnterStep(Step.CollectLoot);
        }
        else if (_step == Step.CollectLoot)
        {
            // 第二步锁定返回键，逼玩家先领取物资；开页动画会重新启用按钮，这里每帧重锁。
            SetCloseButtonLocked(true);
            if (HasCollectedLoot()) EnterStep(Step.CloseChest);
        }
        ResolveTarget();
        LayoutOverlay();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_active || !Visible) return;

        // ESC：直接退出教学（ShowOnlyOnce 时记入已完成，不再出现）
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            FinishTutorial();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton mouse)
        {
            if (!_inputRect.HasPoint(mouse.Position)) GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventScreenTouch touch)
        {
            if (!_inputRect.HasPoint(touch.Position)) GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventKey key2 && key2.Pressed)
            GetViewport().SetInputAsHandled();
    }

    public void ForceStartForTesting()
    {
        if (!_bound) TryBind();
        StartTutorial();
    }

    private void TryBind()
    {
        Node scope = GetParent() ?? GetTree().CurrentScene;
        Chest ??= FindChest(scope);
        RewardPage ??= FindDescendant<RewardPage>(scope);
        if (Chest == null || RewardPage == null) return;
        Chest.PlayerEnteredRange += OnPlayerEnteredRange;
        RewardPage.Closed += OnRewardClosed;
        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;
        if (GodotObject.IsInstanceValid(Chest)) Chest.PlayerEnteredRange -= OnPlayerEnteredRange;
        if (GodotObject.IsInstanceValid(RewardPage)) RewardPage.Closed -= OnRewardClosed;
        _bound = false;
    }

    private void OnPlayerEnteredRange()
    {
        if (TutorialEnabled && Chest != null && !Chest.IsOpened && !IsCompleted()) StartTutorial();
    }

    private void StartTutorial()
    {
        if (_active || Chest == null || RewardPage == null || IsCompleted()) return;
        _active = true;
        Visible = true;
        LockMovement();
        EnterStep(Step.ClickChest);
    }

    private void OnRewardClosed()
    {
        if (!_active) return;
        // 第三步：玩家点击返回键，结束教程。
        if (_step == Step.CloseChest) FinishTutorial();
        // 第二步清单就合上 = 物资已全部领完自动合上（返回键此时是锁住的），直接结束。
        else if (_step == Step.CollectLoot) FinishTutorial();
    }

    private void EnterStep(Step next)
    {
        _step = next;
        (string title, string instruction) = next switch
        {
            Step.ClickChest => (ClickTitle, ClickInstruction),
            Step.CollectLoot => (CollectTitle, CollectInstruction),
            _ => (CloseTitle, CloseInstruction),
        };
        if (next == Step.CollectLoot) SnapshotBackpack();
        if (next == Step.CloseChest) SetCloseButtonLocked(false);
        _stepLabel.Text = $"宝箱教学  {(int)next + 1}/3";
        _titleLabel.Text = title;
        _bodyLabel.Text = instruction;
        EmitSignal(SignalName.TutorialStepChanged, (int)next, instruction);
        ResolveTarget();
    }

    private void FinishTutorial()
    {
        if (!_active) return;
        _active = false;
        _step = Step.Complete;
        Visible = false;
        _targetNode = null;
        _targetRect = new Rect2();
        _inputRect = new Rect2();
        SetCloseButtonLocked(false); // 释放第二步的返回键锁，不影响 RewardPage 其他复用
        UnlockMovement();
        GameState.Instance?.SetObjectState(TutorialStateMapId, TutorialStateId,
            new Dictionary { { "completed", true } });
        EmitSignal(SignalName.TutorialCompleted);
    }

    private bool IsCompleted()
    {
        if (!ShowOnlyOnce) return false;
        Dictionary state = GameState.Instance?.GetObjectState(TutorialStateMapId, TutorialStateId);
        return state != null && state.TryGetValue("completed", out Variant value) && value.AsBool();
    }

    private void LockMovement()
    {
        if (_movementLocked) return;
        PlayerController.Instance?.LockMovement();
        _movementLocked = true;
    }

    private void UnlockMovement()
    {
        if (!_movementLocked) return;
        PlayerController.Instance?.UnlockMovement();
        _movementLocked = false;
    }

    private void ResolveTarget()
    {
        if (_step == Step.ClickChest)
        {
            CollisionShape2D shape = Chest?.GetNodeOrNull<CollisionShape2D>("ClickZone/ClickShape");
            _targetNode = shape;
            _inputRect = GetShapeScreenRect(shape);
        }
        else if (_step == Step.CollectLoot)
        {
            // 第二步：高亮清单本体（标题 + 战利品列表），槽位按钮都在洞内可点击。
            // RewardPage 整个控件的矩形比绘制出来的 UI 大一圈，直接用会聚光过多。
            _targetNode = RewardPage;
            _inputRect = GetListUiRect();
        }
        else if (_step == Step.CloseChest)
        {
            Control close = RewardPage?.GetNodeOrNull<Control>("CloseBtn");
            _targetNode = close;
            _inputRect = close?.IsVisibleInTree() == true ? ClampRect(close.GetGlobalRect()) : new Rect2();
        }
        else
        {
            _targetNode = null;
            _inputRect = new Rect2();
        }
        _targetRect = ExpandAndClamp(_inputRect, TargetPadding);
    }

    // ================================================================
    //  第二步：领取物资检测（DataManager 全局背包/存档）
    // ================================================================

    private int _capSnapshot;
    private int _faucetSnapshot;
    private int _itemSnapshot;
    private int _cardSnapshot;

    /// <summary>进入第二步时给背包拍快照，之后任何数值上涨都视为“领取了物资”。</summary>
    private void SnapshotBackpack()
    {
        DataManager dm = DataManager.Instance;
        _capSnapshot = dm?.BottleCap ?? 0;
        _faucetSnapshot = dm?.Faucet ?? 0;
        _itemSnapshot = dm?.ItemBag.Count ?? 0;
        _cardSnapshot = dm == null ? 0 : TotalCardCount(dm);
    }

    /// <summary>瓶盖/水龙头/道具/卡牌任意一项比快照多，即玩家已把物资收入背包。</summary>
    private bool HasCollectedLoot()
    {
        DataManager dm = DataManager.Instance;
        return dm != null && (dm.BottleCap > _capSnapshot
            || dm.Faucet > _faucetSnapshot
            || dm.ItemBag.Count > _itemSnapshot
            || TotalCardCount(dm) > _cardSnapshot);
    }

    private static int TotalCardCount(DataManager dm)
    {
        int total = 0;
        foreach (int count in dm.CardCounts.Values) total += count;
        return total;
    }

    /// <summary>清单本体在屏幕上的矩形（标题与战利品列表的并集，裁到视口内）。</summary>
    private Rect2 GetListUiRect()
    {
        if (RewardPage == null || !RewardPage.IsVisibleInTree()) return new Rect2();
        Rect2 rect = new();
        Control title = RewardPage.GetNodeOrNull<Control>("Title");
        Control list = RewardPage.GetNodeOrNull<Control>("LootList");
        if (title != null) rect = rect.Merge(title.GetGlobalRect());
        if (list != null) rect = rect.Merge(list.GetGlobalRect());
        return rect.HasArea() ? ClampRect(rect) : rect;
    }

    private void SetCloseButtonLocked(bool locked)
    {
        if (RewardPage?.GetNodeOrNull("CloseBtn") is BaseButton close)
            close.Disabled = locked;
    }

    private Rect2 GetShapeScreenRect(CollisionShape2D shapeNode)
    {
        if (shapeNode?.Shape == null || !shapeNode.IsVisibleInTree()) return new Rect2();
        Vector2 half = shapeNode.Shape switch
        {
            CircleShape2D circle => Vector2.One * circle.Radius,
            RectangleShape2D rectangle => rectangle.Size * 0.5f,
            CapsuleShape2D capsule => new Vector2(capsule.Radius, capsule.Height * 0.5f),
            _ => new Vector2(48, 48),
        };
        Transform2D transform = shapeNode.GetGlobalTransformWithCanvas();
        Vector2[] points = {
            transform * new Vector2(-half.X, -half.Y), transform * new Vector2(half.X, -half.Y),
            transform * new Vector2(half.X, half.Y), transform * new Vector2(-half.X, half.Y),
        };
        float left = points[0].X, right = points[0].X, top = points[0].Y, bottom = points[0].Y;
        foreach (Vector2 point in points)
        {
            left = Mathf.Min(left, point.X); right = Mathf.Max(right, point.X);
            top = Mathf.Min(top, point.Y); bottom = Mathf.Max(bottom, point.Y);
        }
        return ClampRect(new Rect2(left, top, right - left, bottom - top));
    }

    private ChestInteractable FindChest(Node root)
    {
        if (root is ChestInteractable chest && (string.IsNullOrWhiteSpace(TargetChestPersistenceId)
            || chest.PersistenceId == TargetChestPersistenceId)) return chest;
        foreach (Node child in root.GetChildren())
        {
            ChestInteractable found = FindChest(child);
            if (found != null) return found;
        }
        return null;
    }

    private void CreateOverlay()
    {
        _root = new Control { Name = "ChestTutorialOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Color dim = new(0.015f, 0.025f, 0.035f, DimOpacity);
        for (int i = 0; i < _blockers.Length; i++)
        {
            _blockers[i] = new ColorRect { Name = $"InputBlocker{i}", Color = dim,
                MouseFilter = Control.MouseFilterEnum.Stop };
            _root.AddChild(_blockers[i]);
        }
        _highlight = new Panel { Name = "HighlightFrame", MouseFilter = Control.MouseFilterEnum.Ignore };
        var frame = new StyleBoxFlat { BgColor = new Color(0.2f, 0.9f, 0.95f, 0.06f),
            BorderColor = new Color("#74e8ec"), BorderWidthLeft = 5, BorderWidthTop = 5,
            BorderWidthRight = 5, BorderWidthBottom = 5, CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10 };
        _highlight.AddThemeStyleboxOverride("panel", frame);
        _root.AddChild(_highlight);
        _arrowLabel = NewLabel(34, new Color("#f1c453"));
        _arrowLabel.Name = "GuideArrow";
        _arrowLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(_arrowLabel);
        _guideBubble = new PanelContainer { Name = "GuideBubble", CustomMinimumSize = new Vector2(470, 146),
            MouseFilter = Control.MouseFilterEnum.Ignore };
        var bubble = new StyleBoxFlat { BgColor = new Color("#101b20f2"), BorderColor = new Color("#74e8ec"),
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12, ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 18, ContentMarginBottom = 18 };
        _guideBubble.AddThemeStyleboxOverride("panel", bubble);
        _root.AddChild(_guideBubble);
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 6);
        _guideBubble.AddChild(stack);
        _stepLabel = NewLabel(16, new Color("#f1c453"));
        _titleLabel = NewLabel(23, new Color("#74e8ec"));
        _bodyLabel = NewLabel(21, Colors.White);
        _bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(_stepLabel); stack.AddChild(_titleLabel); stack.AddChild(_bodyLabel);
    }

    private static Label NewLabel(int size, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color("#071014"));
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    private void LayoutOverlay()
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (_inputRect.Size.X <= 0 || _inputRect.Size.Y <= 0)
        {
            SetRect(_blockers[0], new Rect2(Vector2.Zero, viewport));
            for (int i = 1; i < 4; i++) SetRect(_blockers[i], new Rect2());
            _highlight.Visible = false; _arrowLabel.Visible = false; return;
        }
        _highlight.Visible = true; _arrowLabel.Visible = true;
        SetRect(_blockers[0], new Rect2(0, 0, viewport.X, _inputRect.Position.Y));
        SetRect(_blockers[1], new Rect2(0, _inputRect.Position.Y, _inputRect.Position.X, _inputRect.Size.Y));
        SetRect(_blockers[2], new Rect2(_inputRect.End.X, _inputRect.Position.Y,
            Mathf.Max(0, viewport.X - _inputRect.End.X), _inputRect.Size.Y));
        SetRect(_blockers[3], new Rect2(0, _inputRect.End.Y, viewport.X,
            Mathf.Max(0, viewport.Y - _inputRect.End.Y)));
        SetRect(_highlight, _targetRect);
        _highlight.SelfModulate = new Color(1, 1, 1, 0.78f + Mathf.Sin(Time.GetTicksMsec() * 0.006f) * 0.22f);
        const float width = 520, height = 154, gap = 34;
        bool below = _targetRect.End.Y + gap + height <= viewport.Y - 24;
        float y = below ? _targetRect.End.Y + gap : _targetRect.Position.Y - gap - height;
        float x = Mathf.Clamp(_targetRect.GetCenter().X - width * 0.5f, 24, viewport.X - width - 24);
        SetRect(_guideBubble, new Rect2(x, Mathf.Clamp(y, 24, viewport.Y - height - 24), width, height));
        _arrowLabel.Text = below ? "▼" : "▲";
        SetRect(_arrowLabel, new Rect2(_targetRect.GetCenter().X - 24,
            below ? _targetRect.End.Y + 2 : _targetRect.Position.Y - 34, 48, 32));
    }

    private Rect2 ClampRect(Rect2 rect) => ExpandAndClamp(rect, 0);
    private Rect2 ExpandAndClamp(Rect2 source, float padding)
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float l = Mathf.Clamp(source.Position.X - padding, 0, viewport.X);
        float t = Mathf.Clamp(source.Position.Y - padding, 0, viewport.Y);
        float r = Mathf.Clamp(source.End.X + padding, 0, viewport.X);
        float b = Mathf.Clamp(source.End.Y + padding, 0, viewport.Y);
        return new Rect2(l, t, Mathf.Max(0, r - l), Mathf.Max(0, b - t));
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
            T found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
