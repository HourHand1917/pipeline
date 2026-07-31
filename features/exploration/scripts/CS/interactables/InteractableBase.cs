using Godot;

/// <summary>
/// 可互动物体基类。两个检测范围：
///   DetectionRange — 大圆，玩家靠近检测（触发亮度闪烁）
///   ClickZone/ClickShape — 小圆，鼠标悬停变亮 + 点击互动
/// 闪烁用 Tween（从当前值平滑过渡，无突变）。
/// </summary>
[GlobalClass]
public abstract partial class InteractableBase : Area2D, IPersistable
{
    [Signal] public delegate void PlayerEnteredRangeEventHandler();
    [Signal] public delegate void PlayerExitedRangeEventHandler();

    [Export] public string DisplayName { get; set; } = "物体";
    [Export] public float DetectionRadius { get; set; } = 120.0f;
    [Export] public float ClickRadius { get; set; } = 48.0f;
    /// <summary>跨地图持久化 ID（同一地图内唯一）</summary>
    [Export] public string PersistenceId { get; set; } = "";

    public bool IsPlayerInRange { get; private set; }

    protected Sprite2D sprite;
    protected CollisionShape2D detectionShape;
    protected Area2D clickZone;
    protected CollisionShape2D clickShape;
    private Color baseColor;
    private bool isHovered;
    private Tween blinkTween;
    protected string _mapId;

    public override void _Ready()
    {
        sprite = GetNode<Sprite2D>("Sprite");
        detectionShape = GetNode<CollisionShape2D>("DetectionRange");
        clickZone = GetNode<Area2D>("ClickZone");
        clickShape = clickZone.GetNode<CollisionShape2D>("ClickShape");

        if (detectionShape.Shape is CircleShape2D circle)
            circle.Radius = DetectionRadius;
        if (clickShape.Shape is CircleShape2D circle2)
            circle2.Radius = ClickRadius;

        // 父 Area2D：只负责检测玩家靠近
        CollisionLayer = 0;
        CollisionMask = 1;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // 子 Area2D：负责鼠标悬停 + 点击
        clickZone.MouseEntered += () => { isHovered = true; OnHoverChanged(); };
        clickZone.MouseExited += () => { isHovered = false; OnHoverChanged(); };
        clickZone.InputEvent += (v, e, i) => OnClickInput(v, e, i);

        SetupPlaceholder();

        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        image.Fill(sprite.Modulate);
        sprite.Texture = ImageTexture.CreateFromImage(image);
        baseColor = sprite.Modulate;
    }

    /// <summary>由 ExplorationManager 注入，保存 mapId 并恢复持久化状态</summary>
    public void InitPersistence(string mapId)
    {
        _mapId = mapId;
        if (string.IsNullOrEmpty(PersistenceId) || string.IsNullOrEmpty(mapId)) return;
        var saved = GameState.Instance?.GetObjectState(mapId, PersistenceId);
        if (saved != null) LoadState(saved);
    }

    public virtual void HandleInteract()
    {
        GD.Print($"与「{DisplayName}」互动。");
        PersistInteraction(_mapId);
    }

    // ================================================================
    //  IPersistable — 子类可 override 扩展状态字段
    // ================================================================

    public virtual Godot.Collections.Dictionary SaveState()
    {
        return new Godot.Collections.Dictionary { { "interacted", true } };
    }

    public virtual void LoadState(Godot.Collections.Dictionary state)
    {
        if (state.TryGetValue("interacted", out var v) && v.AsBool())
        {
            // 已互动过 → 隐藏或变灰
            QueueFree();
        }
    }

    /// <summary>子类在状态改变时调用此方法持久化</summary>
    protected void PersistInteraction(string mapId)
    {
        if (string.IsNullOrEmpty(PersistenceId) || string.IsNullOrEmpty(mapId)) return;
        GameState.Instance?.SetObjectState(mapId, PersistenceId, SaveState());
    }

    protected virtual void SetupPlaceholder() { }

    private void OnBodyEntered(Node2D body)
    {
        if (body is PlayerController)
        {
            IsPlayerInRange = true;
            StartBlinking();
            EmitSignal(SignalName.PlayerEnteredRange);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is PlayerController)
        {
            IsPlayerInRange = false;
            StopBlinking();
            EmitSignal(SignalName.PlayerExitedRange);
        }
    }

    private void OnClickInput(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && mouseButton.Pressed
            && IsPlayerInRange)
        {
            HandleInteract();
        }
    }

    private void OnHoverChanged()
    {
        if (isHovered && IsPlayerInRange)
        {
            // 暂停闪烁，固定高亮
            blinkTween?.Kill();
            sprite.Modulate = new Color(baseColor.R * 1.5f, baseColor.G * 1.5f, baseColor.B * 1.5f, 1.0f);
        }
        else if (!isHovered && IsPlayerInRange)
        {
            // 恢复闪烁
            StartBlinking();
        }
    }

    private void StartBlinking()
    {
        blinkTween?.Kill();
        blinkTween = CreateTween();
        blinkTween.SetLoops(0);
        var dim = new Color(baseColor.R * 0.5f, baseColor.G * 0.5f, baseColor.B * 0.5f, 1.0f);
        blinkTween.TweenProperty(sprite, "modulate", dim, 0.6f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        blinkTween.TweenProperty(sprite, "modulate", baseColor, 0.6f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private void StopBlinking()
    {
        blinkTween?.Kill();
        sprite.Modulate = baseColor;
    }
}
