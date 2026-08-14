using Godot;

[GlobalClass]
public partial class BuffShow : Control
{
    [Export] private TextureRect iconRect;
    [Export] private Label stacksLabel;

    public void Setup(Texture2D icon, int stacks)
    {
        if (iconRect != null)
            iconRect.Texture = icon;

        if (stacksLabel != null)
            stacksLabel.Text = stacks > 1 ? stacks.ToString() : "";
    }
}