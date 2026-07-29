using Godot;

/// <summary>
/// 可互动物体基类。Area2D 检测玩家靠近 + 接收点击事件。
/// 子类重写 HandleInteract() 实现具体互动逻辑。
/// </summary>
[GlobalClass]
public abstract partial class InteractableBase : Area2D
{
    [Signal] public delegate void PlayerEnteredRangeEventHandler();
    [Signal] public delegate void PlayerExitedRangeEventHandler();

    [Export] public string DisplayName { get; set; } = "物体";
    [Export] public float InteractionRadius { get; set; } = 120.0f;

    public bool IsPlayerInRange { get; private set; }

    protected Sprite2D sprite;
    protected CollisionShape2D rangeShape;
    private Color baseColor;
    private bool isHovered;

    public override void _Ready()
    {
        sprite = GetNode<Sprite2D>("Sprite");
        rangeShape = GetNode<CollisionShape2D>("InteractionRange");

        if (rangeShape.Shape is CircleShape2D circle)
            circle.Radius = InteractionRadius;

        // Layer 2: 交互物层（必须非零，input_event 拾取才生效）
        CollisionLayer = 2;
        CollisionMask = 1;

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        MouseEntered += () => { isHovered = true; UpdateVisual(); };
        MouseExited += () => { isHovered = false; UpdateVisual(); };
        // 用 lambda 避开 C# InputEvent 类型名冲突
        ((Area2D)this).InputEvent += (v, e, i) => OnInputEvent(v, e, i);

        SetupPlaceholder();

        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        image.Fill(sprite.Modulate);
        sprite.Texture = ImageTexture.CreateFromImage(image);
        baseColor = sprite.Modulate;
    }

    public virtual void HandleInteract()
    {
        GD.Print($"与「{DisplayName}」互动。");
    }

    protected virtual void SetupPlaceholder() { }

    private void OnBodyEntered(Node2D body)
    {
        if (body is PlayerController)
        {
            IsPlayerInRange = true;
            UpdateVisual();
            EmitSignal(SignalName.PlayerEnteredRange);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is PlayerController)
        {
            IsPlayerInRange = false;
            UpdateVisual();
            EmitSignal(SignalName.PlayerExitedRange);
        }
    }

    private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && mouseButton.Pressed
            && IsPlayerInRange)
        {
            HandleInteract();
        }
    }

    private void UpdateVisual()
    {
        // 鼠标悬停 + 在范围内 = 变亮
        sprite.Modulate = (isHovered && IsPlayerInRange) ? baseColor * 1.5f : baseColor;
    }
}
