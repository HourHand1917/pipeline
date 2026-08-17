using Godot;

/// <summary>
/// 玩家状态机。管理 Idle / Move / Brake 的切换。
/// 遵循 godot-master Flat FSM 模式：少量互斥状态，无嵌套。
/// 转换关系：
///   Idle  ←(move 输入)  Move
///   Move  ←(松开按键)   Brake
///   Brake ←(动画播完)    Idle
///   Brake ←(move 输入)   Move   （刹车可打断）
/// </summary>
public partial class PlayerStateMachine : Node
{
    private PlayerStateBase currentState;
    private PlayerIdleState idleState;
    private PlayerMoveState moveState;
    private PlayerBrakeState brakeState;

    public override void _Ready()
    {
        idleState = new PlayerIdleState();
        moveState = new PlayerMoveState();
        brakeState = new PlayerBrakeState();
    }

    /// <summary>初始化所有状态的上下文引用</summary>
    public void Setup(PlayerController controller)
    {
        idleState.Setup(controller);
        moveState.Setup(controller);
        brakeState.Setup(controller);

        // 刹车动画（非循环）播完 → 回 idle
        if (controller.AnimSprite != null)
            controller.AnimSprite.AnimationFinished += OnAnimationFinished;

        TransitionTo(idleState);
    }

    /// <summary>切换到闲置状态</summary>
    public void RequestIdle()
    {
        if (currentState != idleState)
            TransitionTo(idleState);
    }

    /// <summary>切换到移动状态（同方向不重复切换；可从 brake 打断进入）。</summary>
    public void RequestMove(int direction)
    {
        if (currentState == moveState && moveState.GetDirection() == direction)
            return;

        moveState.SetDirection(direction);
        TransitionTo(moveState);
    }

    /// <summary>松开移动 → 进入刹车。只有移动中才进刹车。</summary>
    public void RequestBrake()
    {
        if (currentState == moveState)
            TransitionTo(brakeState);
    }

    public override void _PhysicsProcess(double delta)
    {
        currentState?.PhysicsUpdate(delta);
    }

    /// <summary>获取当前状态的名称（调试用）</summary>
    public string CurrentStateName =>
        currentState == idleState ? "Idle" :
        currentState == moveState ? "Move" :
        currentState == brakeState ? "Brake" : "None";

    private void OnAnimationFinished()
    {
        // 只有 brake 是非循环动画，播完会触发这里
        if (currentState == brakeState)
            TransitionTo(idleState);
    }

    private void TransitionTo(PlayerStateBase newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }
}
