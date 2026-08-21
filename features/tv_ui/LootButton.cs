using Godot;

/// <summary>
/// 战利品页的纯文字按钮：悬停时在文字下方画一条下划线。
/// 配合 Flat=true 去掉背景；字体/字号/颜色仍由 RewardPage 通过 theme override 注入。
/// </summary>
[GlobalClass]
public partial class LootButton : Button
{
    /// <summary>下划线粗细（像素）。</summary>
    [Export] public int UnderlineThickness { get; set; } = 2;
    /// <summary>下划线颜色。</summary>
    [Export] public Color UnderlineColor { get; set; } = Colors.Black;

    private bool _hovered;

    public override void _Ready()
    {
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
    }

    public override void _Draw()
    {
        if (!_hovered || Disabled) return;
        if (string.IsNullOrEmpty(Text)) return;

        var font = GetThemeFont("font");
        int fontSize = GetThemeFontSize("font_size");

        float textWidth = font.GetStringSize(Text, HorizontalAlignment.Left, -1, fontSize).X;
        float lineHeight = font.GetHeight(fontSize);

        // 下划线水平对齐跟随文字对齐（默认居中）
        float x = 0f;
        if (Alignment == HorizontalAlignment.Center)
            x = (Size.X - textWidth) * 0.5f;
        else if (Alignment == HorizontalAlignment.Right)
            x = Size.X - textWidth;

        // 文字垂直居中：下划线放在文字底部下方 2px
        float y = (Size.Y - lineHeight) * 0.5f + lineHeight + 2f;
        DrawLine(new Vector2(x, y), new Vector2(x + textWidth, y), UnderlineColor, UnderlineThickness);
    }
}
