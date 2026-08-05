using Godot;

[GlobalClass]
public partial class TrackSlot : PanelContainer
{
    [Signal] public delegate void SlotClickedEventHandler(int cellNumber, GodotObject combatant);

    [Export] private Control occupant;
    [Export] private AnimatedSprite2D animatedSprite;
    [Export] private Label glyphLabel;
    [Export] private Label slotLabel;

    public int CellNumber { get; private set; }
    public GodotObject OccupantRef { get; private set; } // PlayerBattle 或 EnemyBattle

    public void Configure(int cellNum, string glyph, Color tint, GodotObject combatant)
    {
        CellNumber = cellNum;
        OccupantRef = combatant;
        slotLabel.Text = $"{cellNum}";

        if (string.IsNullOrEmpty(glyph))
        {
            glyphLabel.Text = "";
            occupant.Modulate = Colors.White;
            OccupantRef = null;
        }
        else
        {
            glyphLabel.Text = glyph;
            glyphLabel.Modulate = tint;
            occupant.Modulate = new Color(tint.R, tint.G, tint.B, 0.3f);
        }
    }

    public override void _Ready()
    {
        GuiInput += OnGuiInput;
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            EmitSignal(SignalName.SlotClicked, CellNumber, OccupantRef);
        }
    }

    /// <summary>后期切换动画用</summary>
    public void PlayAnimation(string animName)
    {
        animatedSprite?.Play(animName);
    }
}