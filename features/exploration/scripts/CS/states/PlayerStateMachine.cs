using Godot;

/// <summary>
/// 玩家状态机。管理 Idle ↔ Move 的切换。
/// 遵循 godot-master Flat FSM 模式：少量互斥状态，无嵌套。
/// </summary>
public partial class PlayerStateMachine : Node
{
    private PlayerStateBase currentState;
    private PlayerIdleState idleState;
    private PlayerMoveState moveState;

    public override void _Ready()
    {
        idleState = new PlayerIdleState();
        moveState = new PlayerMoveState();
    }

    /// <summary>
    /// 初始化所有状态的上下文引用
    /// </summary>
    public void Setup(PlayerController controller)
    {
        idleState.Setup(controller);
        moveState.Setup(controller);
        TransitionTo(idleState);
    }

    /// <summary>
    /// 切换到闲置状态
    /// </summary>
    public void RequestIdle()
    {
        if (currentState != idleState)
            TransitionTo(idleState);
    }

    /// <summary>
    /// 切换到移动状态
    /// </summary>
    public void RequestMove(int direction)
    {
        // 同方向不重复切换
        if (currentState == moveState && moveState.GetDirection() == direction)
            return;

        moveState.SetDirection(direction);
        TransitionTo(moveState);
    }

    public override void _PhysicsProcess(double delta)
    {
        currentState?.PhysicsUpdate(delta);
    }

    /// <summary>
    /// 获取当前状态的名称（调试用）
    /// </summary>
    public string CurrentStateName =>
        currentState == idleState ? "Idle" :
        currentState == moveState ? "Move" : "None";

    private void TransitionTo(PlayerStateBase newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }
}
