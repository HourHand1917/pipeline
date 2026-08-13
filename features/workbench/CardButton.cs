using Godot;

/// <summary>
/// 卡牌按钮（工作台专用）。带名称、数量、升级花费（含货币图标）。
/// 在编辑器里给 FaucetIcon 拖入货币贴图，代码只填文字。
/// </summary>
[GlobalClass]
public partial class CardButton : Button
{
    [Export] private Label _iconLabel;
    [Export] private Label _nameLabel;
    [Export] private Label _costLabel;

    public void SetCard(string iconText, string name, int count, int cost)
    {
        if (_iconLabel != null) _iconLabel.Text = iconText;
        if (_nameLabel != null) _nameLabel.Text = $"{name} ×{count}";
        if (_costLabel != null) _costLabel.Text = cost.ToString();
    }
}
