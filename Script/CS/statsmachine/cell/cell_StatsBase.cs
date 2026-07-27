using Godot;

public partial class State : Node
{
    /// <summary>
    /// 进入该状态时调用
    /// </summary>
    public virtual void Enter() { }

    /// <summary>
    /// 退出该状态时调用
    /// </summary>
    public virtual void Exit() { }

    /// <summary>
    /// 每帧更新（由状态机驱动）
    /// </summary>
    public virtual void Update(double delta) { }

    /// <summary>
    /// 物理更新（由状态机驱动）
    /// </summary>
    public virtual void PhysicsUpdate(double delta) { }

    /// <summary>
    /// 处理输入（由状态机驱动）
    /// </summary>
    public virtual void HandleInput(InputEvent @event) { }
}