using Godot;

/// <summary>
/// 可互动物体基类。Area2D 检测玩家靠近 + 接收点击事件。
/// 闪烁行为由可选的 BlinkComponent 子节点提供，不挂在基类上。
/// 遵循 Composition over Inheritance。
/// </summary>
[GlobalClass]
public abstract partial class InteractableBase : Area2D, IPersistable
{
    [Signal] public delegate void PlayerEnteredRangeEventHandler();
    [Signal] public delegate void PlayerExitedRangeEventHandler();

    [Export] public string DisplayName { get; set; } = "物体";
    /// <summary>跨地图持久化 ID（同一地图内唯一）</summary>
    [Export] public string PersistenceId { get; set; } = "";

    public bool IsPlayerInRange { get; private set; }

    protected Node2D sprite;
    protected CollisionShape2D detectionShape;
    protected Area2D clickZone;
    protected CollisionShape2D clickShape;
    protected string _mapId;

    [Export] private BlinkComponent _blink;

    public override void _Ready()
    {
        sprite = GetNode<Node2D>("Sprite");
        detectionShape = GetNode<CollisionShape2D>("DetectionRange");
        clickZone = GetNode<Area2D>("ClickZone");
        clickShape = clickZone.GetNode<CollisionShape2D>("ClickShape");

        // 父 Area2D：只负责检测玩家靠近
        CollisionLayer = 0;
        CollisionMask = 1;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // 子 Area2D：点击事件
        clickZone.InputEvent += (v, e, i) => OnClickInput(v, e, i);
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
            QueueFree();
    }

    protected void SetBlinkEnabled(bool enabled)
    {
        if (_blink != null) _blink.Enabled = enabled;
    }

    protected void PersistInteraction(string mapId)
    {
        if (string.IsNullOrEmpty(PersistenceId) || string.IsNullOrEmpty(mapId)) return;
        GameState.Instance?.SetObjectState(mapId, PersistenceId, SaveState());
    }



    private void OnBodyEntered(Node2D body)
    {
        if (body is PlayerController)
        {
            IsPlayerInRange = true;
            if (_blink != null) _blink.IsPlayerInRange = true;
            EmitSignal(SignalName.PlayerEnteredRange);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is PlayerController)
        {
            IsPlayerInRange = false;
            if (_blink != null) _blink.IsPlayerInRange = false;
            EmitSignal(SignalName.PlayerExitedRange);
        }
    }

    private bool CanInteract =>
        IsPlayerInRange && (_blink == null || _blink.IsHovered);

    private void OnClickInput(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            && CanInteract)
        {
            HandleInteract();
        }
    }
}
