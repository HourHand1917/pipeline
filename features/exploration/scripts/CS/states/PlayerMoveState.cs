/// <summary>
/// 移动状态：根据方向持续移动，更新朝向翻转。
/// </summary>
public partial class PlayerMoveState : PlayerStateBase
{
    private int direction; // -1 左, 1 右

    public void SetDirection(int dir)
    {
        direction = dir;
    }

    public int GetDirection()
    {
        return direction;
    }

    public override void Enter()
    {
        player.SetVelocityX(direction * player.MoveSpeed);
        player.FaceDirection(direction);
    }

    public override void Exit()
    {
    }

    public override void PhysicsUpdate(double delta)
    {
        // 持续施加速度
        player.SetVelocityX(direction * player.MoveSpeed);
        player.FaceDirection(direction);
    }
}
