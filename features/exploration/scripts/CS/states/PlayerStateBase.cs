using Godot;

/// <summary>
/// 玩家状态基类（RefCounted，不参与场景树）。
/// 遵循 godot-master 专家模式：状态不继承 Node，省开销。
/// </summary>
public abstract partial class PlayerStateBase : RefCounted
{
    protected PlayerController player;

    public void Setup(PlayerController controller)
    {
        player = controller;
    }

    public abstract void Enter();
    public abstract void Exit();
    public abstract void PhysicsUpdate(double delta);
}
