using Godot;

[GlobalClass]
public partial class BuffShow : Control
{
    [Export] private TextureRect iconRect;
    [Export] private Label stacksLabel;

    public void Setup(Texture2D icon, int stacks, string buffName, string description)
    {
        if (iconRect != null)
            iconRect.Texture = icon;

        if (stacksLabel != null)
            stacksLabel.Text = stacks > 1 ? stacks.ToString() : "";

        // 悬浮提示：名字 + 层数 + 描述
        var data = new TooltipData
        {
            Title = $"{buffName} ×{stacks}",
            Description = description,
            Icon = icon,
        };
        TooltipService.Instance.ShowFor(this, data);
    }

    public override void _ExitTree()
    {
        // 槽位被重建（RefreshPlayerBuffs 清空再重加）时解除绑定，避免残留失效引用
        if (TooltipService.Instance != null && GodotObject.IsInstanceValid(TooltipService.Instance))
            TooltipService.Instance.HideFor(this);
    }
}
