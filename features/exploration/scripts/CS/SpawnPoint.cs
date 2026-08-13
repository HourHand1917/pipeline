using Godot;

/// <summary>
/// 地图生成点。挂在每个地图场景里，玩家进入地图时定位到对应生成点。
/// </summary>
[GlobalClass]
public partial class SpawnPoint : Node2D
{
    [Export] public StringName SpawnId { get; set; } = "";
}
