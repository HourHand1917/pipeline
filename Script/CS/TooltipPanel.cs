using Godot;

/// <summary>
/// 悬浮提示面板。接收 TooltipData 渲染到各节点。
/// 动画（RESET / fade_in / fade_out）由你在编辑器的 AnimationPlayer 里制作。
/// </summary>
[GlobalClass]
public partial class TooltipPanel : PanelContainer
{
    [Export] public TextureRect IconRect { get; set; }
    [Export] public Label TitleLabel { get; set; }
    [Export] public Label DescLabel { get; set; }
    [Export] public GridContainer DetailsGrid { get; set; }
    [Export] public AnimationPlayer Anim { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.93f);
        style.BorderWidthLeft = style.BorderWidthRight = 2;
        style.BorderWidthTop = style.BorderWidthBottom = 2;
        style.BorderColor = new Color(0.35f, 0.35f, 0.5f, 1f);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 6;
        style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 6;
        style.ContentMarginLeft = style.ContentMarginRight = 10;
        style.ContentMarginTop = style.ContentMarginBottom = 8;
        AddThemeStyleboxOverride("panel", style);
    }

    public void Render(TooltipData data)
    {
        if (data == null) return;

        IconRect.Visible = data.Icon != null;
        if (data.Icon != null) IconRect.Texture = data.Icon;

        TitleLabel.Visible = !string.IsNullOrEmpty(data.Title);
        if (!string.IsNullOrEmpty(data.Title)) TitleLabel.Text = data.Title;

        DescLabel.Visible = !string.IsNullOrEmpty(data.Description);
        if (!string.IsNullOrEmpty(data.Description)) DescLabel.Text = data.Description;

        foreach (Node child in DetailsGrid.GetChildren())
            child.QueueFree();

        bool hasDetails = data.Details != null && data.Details.Count > 0;
        DetailsGrid.Visible = hasDetails;
        if (hasDetails)
        {
            foreach (var kv in data.Details)
            {
                var keyLabel = new Label { Text = kv.Key };
                keyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
                keyLabel.AddThemeFontSizeOverride("font_size", 13);
                DetailsGrid.AddChild(keyLabel);

                var valLabel = new Label { Text = kv.Value };
                valLabel.AddThemeFontSizeOverride("font_size", 13);
                DetailsGrid.AddChild(valLabel);
            }
        }
    }
}
