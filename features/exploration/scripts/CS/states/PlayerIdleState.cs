/// <summary>
/// 闲置状态：角色静止不动。进入时清零速度。
/// </summary>
public partial class PlayerIdleState : PlayerStateBase
{
    public override void Enter()
    {
        player.SetVelocityX(0);
    }

    public override void Exit()
    {
    }

    public override void PhysicsUpdate(double delta)
    {
        // 闲置状态不做任何物理更新
    }
}
