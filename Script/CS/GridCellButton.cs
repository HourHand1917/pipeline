using Godot;
using Godot.Collections;

public enum CellState { Normal, Charged, Cooldown, Disabled, Ready }

[GlobalClass]
public partial class GridCellButton : Button
{
    [Export] private TextureRect normalTexture;
    [Export] private TextureRect chargedTexture;
    [Export] private TextureRect cooldownTexture;
    [Export] private TextureRect disabledTexture;
    [Export] private Label textLabel;

    private CellState _state = CellState.Normal;

    // Buff 染色
    private static readonly Color DustTint = new("#d5b45a");      // 蒙尘：沙黄
    private static readonly Color DisabledTint = new("#7a4a4a"); // 失效：暗红
    private StyleBoxFlat _normalStyle;
    private StyleBoxFlat _buffStyle;

    public void SetState(CellState state)
    {
        _state = state;
        normalTexture.Visible = state == CellState.Normal;
        chargedTexture.Visible = state == CellState.Charged || state == CellState.Ready;
        cooldownTexture.Visible = state == CellState.Cooldown;
        disabledTexture.Visible = state == CellState.Disabled;

        if (state == CellState.Ready)
        {
            chargedTexture.Modulate = new Color(1f, 0.85f, 0.3f);
            Disabled = false;
        }
        else
        {
            chargedTexture.Modulate = Colors.White;
        }
    }

    /// <summary>
    /// 根据单元格 Stats 中的 Buff 覆盖 StyleBox。
    /// </summary>
    public void ApplyBuffTint(GodotObject cellStats)
    {
    if (cellStats == null)
        {
            RemoveThemeStyleboxOverride("normal");
            return;
        }

        var buffs = cellStats.Get("buffs").As<Array>();
        if (buffs == null || buffs.Count == 0)
        {
            RemoveThemeStyleboxOverride("normal");
            return;
    }

        Color tint = new Color(1f, 1f, 1f, 0f);
        string buffName = "";

        foreach (var bi in buffs)
        {
            if (bi.Obj == null) continue;
            var instance = bi.As<GodotObject>();
            var buff = instance.Get("buff").As<GodotObject>();
            string id = buff.Get(GDScriptKeys.Buff.Id).AsString();

            switch (id)
            {
                case "dust":
                    tint = DustTint;
                    buffName = "蒙尘";
                    break;
                case "disabled":
                    tint = DisabledTint;
                    buffName = "失效";
                    break;
            }
        }

        _buffStyle ??= new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.35f),
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
        };

        _buffStyle.BgColor = new Color(tint.R, tint.G, tint.B, 0.45f);
        _buffStyle.BorderColor = tint;
        _buffStyle.BorderWidthLeft = 3;
        _buffStyle.BorderWidthTop = 3;
        _buffStyle.BorderWidthRight = 3;
        _buffStyle.BorderWidthBottom = 3;

        AddThemeStyleboxOverride("normal", _buffStyle);
        TooltipText = buffName;
    }

    public void SetText(string text)
    {
        if (textLabel != null)
            textLabel.Text = text;
    }
}