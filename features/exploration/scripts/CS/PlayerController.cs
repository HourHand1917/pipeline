using Godot;

/// <summary>
/// 玩家角色控制器。CharacterBody2D + 状态机驱动的横向移动。
/// 动画（idle / move / stop）由编辑器里的 AnimatedSprite2D + SpriteFrames 提供，
/// 帧率/时长已在 SpriteFrames 里配好，代码只负责按状态播放。
/// </summary>
[GlobalClass]
public partial class PlayerController : CharacterBody2D
{
    // 动画名常量（对应 SpriteFrames 里的动画名，避免魔法字符串）
    public const string AnimIdle = "idle";
    public const string AnimMove = "move";
    public const string AnimStop = "stop";

    [Export] public float MoveSpeed { get; set; } = 300.0f;

    public int MapLeft { get; set; } = -2000;
    public int MapRight { get; set; } = 3500;

    /// <summary>当前朝向：-1 左，1 右</summary>
    public int FacingDirection { get; private set; } = 1;

    /// <summary>当前玩家实例（场景切换时自动更新）。供 RewardPage / MapManager 等禁用移动。</summary>
    public static PlayerController Instance { get; private set; }

    /// <summary>是否允许移动（锁定计数为 0 时才可移动）。</summary>
    public bool MovementEnabled => _movementLockers == 0;

    /// <summary>移动音效数组（由 ExplorationManager 设置）</summary>
    public AudioStream[] WalkSounds { get; set; }

    /// <summary>move 动画中需要播放脚步声的帧</summary>
    [Export] public int[] MoveSoundFrames { get; set; } = { 1, 11 };

    /// <summary>stop 动画中需要播放音效的帧</summary>
    [Export] public int[] StopSoundFrames { get; set; } = { 3 };

    private int _movementLockers;
    private PlayerStateMachine stateMachine;
    private AnimatedSprite2D sprite;
    private Area2D interactionArea;
    private AudioManager _audioManager;

    /// <summary>供状态机连接 AnimationFinished（stop 播完 → 回 idle）。</summary>
    public AnimatedSprite2D AnimSprite => sprite;

    public override void _Ready()
    {
        Instance = this;

        stateMachine = GetNode<PlayerStateMachine>("StateMachine");
        sprite = GetNode<AnimatedSprite2D>("Sprite");
        interactionArea = GetNode<Area2D>("InteractionArea");

        sprite.FlipH = (FacingDirection == 1); // 初始化朝向（默认朝右）

        stateMachine.Setup(this);

        // 获取 AudioManager 实例
        _audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");

        // 连接帧变化信号，用于播放脚步声
        sprite.FrameChanged += OnFrameChanged;

        // 转场入场期间锁定移动：新场景的玩家在 IrisOpen 完成前不能动
        if (MapManager.Instance?.IsTraveling == true)
            LockMovement();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        // 断开信号连接，防止内存泄漏
        if (sprite != null)
        {
            sprite.FrameChanged -= OnFrameChanged;
        }
    }

    /// <summary>
    /// 监听动画帧变化，在特定帧播放脚步声
    /// </summary>
    private void OnFrameChanged()
    {
        if (_audioManager == null) return;
        if (WalkSounds == null || WalkSounds.Length == 0) return;
        if (!MovementEnabled) return;

        // 根据当前动画检查是否需要在当前帧播放音效
        if (sprite.Animation == AnimMove)
        {
            if (IsFrameInList(sprite.Frame, MoveSoundFrames))
            {
                _audioManager.PlayRandomSfx(WalkSounds);
            }
        }
        else if (sprite.Animation == AnimStop)
        {
            if (IsFrameInList(sprite.Frame, StopSoundFrames))
            {
                _audioManager.PlayRandomSfx(WalkSounds);
            }
        }
    }

    /// <summary>
    /// 检查帧是否在指定的帧列表中
    /// </summary>
    private bool IsFrameInList(int frame, int[] frameList)
    {
        if (frameList == null) return false;
        
        foreach (int f in frameList)
        {
            if (f == frame) return true;
        }
        return false;
    }

    /// <summary>设置水平速度</summary>
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

    /// <summary>翻转角色朝向。美术默认朝左，向右移动（direction==1）时翻转。</summary>
    public void FaceDirection(int direction)
    {
        FacingDirection = direction;
        sprite.FlipH = (direction == 1);
    }

    /// <summary>播放指定动画（AnimIdle / AnimMove / AnimStop）。</summary>
    public void PlayAnimation(string name) => sprite.Play(name);

    /// <summary>锁定移动（引用计数）。第一个锁定者会立即停下（位置立刻停 + 播刹车动画）。</summary>
    public void LockMovement()
    {
        _movementLockers++;
        if (_movementLockers == 1)
            stateMachine.RequestBrake(); // 0 → 1 时立刻停下，走刹车动画
    }

    /// <summary>解锁移动（引用计数）。计数归零后恢复可移动。</summary>
    public void UnlockMovement()
    {
        _movementLockers = Mathf.Max(0, _movementLockers - 1);
    }

    /// <summary>外部输入 → 状态机</summary>
    public void OnMovePressed(int direction)
    {
        if (!MovementEnabled) return;
        stateMachine.RequestMove(direction);
    }

    public void OnMoveReleased(int direction)
    {
        if (!MovementEnabled) return;
        stateMachine.RequestBrake();
    }

    /// <summary>获取当前范围内可互动的物体列表</summary>
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
}