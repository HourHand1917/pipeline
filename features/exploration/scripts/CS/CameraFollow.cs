using Godot;

[GlobalClass]
public partial class CameraFollow : Camera2D
{
    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        // 8.0 太慢，相机明显落后主角，画面产生动态模糊感；调高后几乎不可见。
        PositionSmoothingSpeed = 25.0f;
        Enabled = true;
        MakeCurrent();
    }
}
