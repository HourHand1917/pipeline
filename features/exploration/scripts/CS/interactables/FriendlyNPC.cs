using Godot;

/// <summary>
/// 友好 NPC。玩家靠近后可点击互动（对话、交易等）。
/// </summary>
[GlobalClass]
public partial class FriendlyNPC : NPCBase
{
    protected override void SetupPlaceholder()
    {
        sprite.Modulate = new Color(0.3f, 0.7f, 1.0f); // 蓝色
    }

    public override void HandleInteract()
    {
        GD.Print($"与「{NpcName}」对话：{DefaultDialog}");
    }
}
