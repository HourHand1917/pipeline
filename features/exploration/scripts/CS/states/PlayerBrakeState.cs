/// <summary>
/// 刹车状态：移动结束后停止并播放刹车动画（非循环）。
/// 动画播完由状态机切回 idle；期间按方向键可被 move 打断。
/// </summary>
public partial class PlayerBrakeState : PlayerStateBase
{
    public override void Enter()
    {
        player.SetVelocityX(0);
        player.PlayAnimation(PlayerController.AnimStop);
    }

    public override void Exit()
    {
    }

    public override void PhysicsUpdate(double delta)
    {
        // 刹车期间角色已停止，等 AnimationFinished 信号切回 idle
    }
}
