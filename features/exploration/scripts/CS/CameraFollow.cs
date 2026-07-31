using Godot;

/// <summary>
/// 相机平滑跟随。边界由 ExplorationManager 在运行时设置。
/// 作为 Player 的 Camera2D 子节点使用。
/// </summary>
[GlobalClass]
public partial class CameraFollow : Camera2D
{
    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 8.0f;
        LimitSmoothed = true;
        Enabled = true;
        MakeCurrent();
    }
}
