using Godot;

[GlobalClass]
public partial class ExplorationManager : Node2D
{
    [Export] public PlayerController Player { get; set; }
    [Export] public string MapId { get; set; } = "";

    [ExportGroup("玩家边界")]
    [Export] public int PlayerLeft { get; set; } = -2000;
    [Export] public int PlayerRight { get; set; } = 3500;

    [ExportGroup("相机边界")]
    [Export] public int CamLeft { get; set; } = -2100;
    [Export] public int CamRight { get; set; } = 3600;

    public override void _Ready()
    {
        if (Player != null)
        {
            Player.MapLeft = PlayerLeft;
            Player.MapRight = PlayerRight;
        }

        var cam = Player?.GetNode<Camera2D>("Camera");
        if (cam != null)
        {
            cam.LimitLeft = CamLeft;
            cam.LimitRight = CamRight;
            cam.LimitSmoothed = true;
        }

        if (!string.IsNullOrEmpty(MapId))
            InitPersistence(this, MapId);

        GD.Print("探索场景已就绪。A/D 移动，鼠标靠近交互物变亮。");
    }

    private static void InitPersistence(Node node, string mapId)
    {
        if (node is IPersistable p && !string.IsNullOrEmpty(p.PersistenceId))
            p.InitPersistence(mapId);
        foreach (Node child in node.GetChildren())
            InitPersistence(child, mapId);
    }

    public override void _Input(InputEvent @event)
    {
        if (Engine.IsEditorHint()) return;

        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Keycode == Key.A && keyEvent.Pressed)
                Player?.OnMovePressed(-1);
            else if (keyEvent.Keycode == Key.D && keyEvent.Pressed)
                Player?.OnMovePressed(1);
            else if ((keyEvent.Keycode == Key.A || keyEvent.Keycode == Key.D) && !keyEvent.Pressed)
                Player?.OnMoveReleased(0);
        }
    }
}
