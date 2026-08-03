using Godot;

/// <summary>
/// 键盘输入路由。A/D → MovePressed/MoveReleased 信号。
/// ExplorationManager 只负责接线，不处理输入逻辑。
/// </summary>
[GlobalClass]
public partial class InputRouter : Node
{
    [Signal] public delegate void MovePressedEventHandler(int direction);
    [Signal] public delegate void MoveReleasedEventHandler(int direction);

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Keycode == Key.A && keyEvent.Pressed)
                EmitSignal(SignalName.MovePressed, -1);
            else if (keyEvent.Keycode == Key.D && keyEvent.Pressed)
                EmitSignal(SignalName.MovePressed, 1);
            else if (keyEvent.Keycode == Key.A && !keyEvent.Pressed)
                EmitSignal(SignalName.MoveReleased, -1);
            else if (keyEvent.Keycode == Key.D && !keyEvent.Pressed)
                EmitSignal(SignalName.MoveReleased, 1);
        }
    }
}
