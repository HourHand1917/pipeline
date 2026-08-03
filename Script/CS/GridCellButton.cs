using Godot;

public enum CellState { Normal, Charged, Cooldown, Disabled }

[GlobalClass]
public partial class GridCellButton : Button
{
    [Export] private TextureRect normalTexture;
    [Export] private TextureRect chargedTexture;
    [Export] private TextureRect cooldownTexture;
    [Export] private TextureRect disabledTexture;
    [Export] private Label textLabel;

    private CellState _state = CellState.Normal;

    public void SetState(CellState state)
    {
        _state = state;
        normalTexture.Visible = state == CellState.Normal;
        chargedTexture.Visible = state == CellState.Charged;
        cooldownTexture.Visible = state == CellState.Cooldown;
        disabledTexture.Visible = state == CellState.Disabled;
    }

    public void SetText(string text)
    {
        if (textLabel != null)
            textLabel.Text = text;
    }
}