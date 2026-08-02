using Godot;

[GlobalClass]
public partial class CameraFollow : Camera2D
{
    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 8.0f;
        Enabled = true;
        MakeCurrent();
    }
}
