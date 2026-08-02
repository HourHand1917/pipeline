using Godot;

/// <summary>
/// 玩家角色控制器。CharacterBody2D + 状态机驱动的横向移动。
/// 预留 Sprite 翻转接口，美术资源到位后直接替换。
/// </summary>
[GlobalClass]
public partial class PlayerController : CharacterBody2D
{
    [Export] public float MoveSpeed { get; set; } = 300.0f;

    public int MapLeft { get; set; } = -2000;
    public int MapRight { get; set; } = 3500;

    /// <summary>
    /// 当前朝向：-1 左，1 右
    /// </summary>
    public int FacingDirection { get; private set; } = 1;

    private PlayerStateMachine stateMachine;
    private Sprite2D sprite;
    private Area2D interactionArea;

    public override void _Ready()
    {
        stateMachine = GetNode<PlayerStateMachine>("StateMachine");
        sprite = GetNode<Sprite2D>("Sprite");
        interactionArea = GetNode<Area2D>("InteractionArea");

        stateMachine.Setup(this);
        SetupPlaceholderSprite();
    }

    /// <summary>
    /// 设置水平速度
    /// </summary>
    public void SetVelocityX(float x)
    {
        Velocity = new Vector2(x, 0);
        MoveAndSlide();
        // 地图左右边界约束
        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, MapLeft, MapRight),
            GlobalPosition.Y
        );
    }

    /// <summary>
    /// 翻转角色朝向。预留：后续替换为 Sprite2D.FlipH
    /// </summary>
    public void FaceDirection(int direction)
    {
        if (direction == FacingDirection)
            return;

        FacingDirection = direction;
        sprite.FlipH = (direction == -1);
    }

    /// <summary>
    /// 外部输入 → 状态机
    /// </summary>
    public void OnMovePressed(int direction)
    {
        stateMachine.RequestMove(direction);
    }

    public void OnMoveReleased(int direction)
    {
        stateMachine.RequestIdle();
    }

    /// <summary>
    /// 获取当前范围内可互动的物体列表
    /// </summary>
    public InteractableBase GetNearestInteractable()
    {
        var bodies = interactionArea.GetOverlappingBodies();
        foreach (var body in bodies)
        {
            if (body is InteractableBase interactable && interactable.IsPlayerInRange)
                return interactable;
        }
        return null;
    }

    /// <summary>
    /// 占位方块：64x64 彩色矩形（美术到位后删除此方法并替换 Sprite 贴图）
    /// </summary>
    private void SetupPlaceholderSprite()
    {
        // 设置碰撞体
        var rect = new RectangleShape2D();
        rect.Size = new Vector2(64, 64);
        var collision = GetNode<CollisionShape2D>("CollisionShape");
        collision.Shape = rect;

        // 生成 64x64 占位贴图
        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        image.Fill(new Color(0.3f, 0.8f, 0.3f)); // 绿色方块
        sprite.Texture = ImageTexture.CreateFromImage(image);
    }
}
