using Godot;

/// <summary>
/// Presentation-only bridge for the exploration backdrop captured by
/// BattleDirector. It inserts one mouse-transparent TextureRect behind the
/// existing distance track and never moves it after installation. The upper
/// stage keeps the exploration-view proportion; everything below is black.
/// </summary>
[GlobalClass]
public partial class BattleBackdropPresenter : Node
{
    [Export] public bool Enabled { get; set; } = true;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BackdropOpacity { get; set; } = 0.30f;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ShadeOpacity { get; set; } = 0.28f;
    [Export(PropertyHint.Range, "0.45,0.8,0.01")]
    public float BackdropBottomRatio { get; set; } = 0.65f;
    [Export(PropertyHint.Range, "0,0.4,0.01")]
    public float StageTopRatio { get; set; } = 0.17f;
    [Export(PropertyHint.Range, "0,160,1")]
    public float TrackBottomClearancePixels { get; set; } = 76.0f;
    [Export] public Vector2 TrackOffset { get; set; } = Vector2.Zero;

    public bool IsInstalled => GodotObject.IsInstanceValid(_backdrop);
    public Texture2D DisplayedSourceTexture => _sourceTexture;

    private BattleScreen _screen;
    private ColorRect _blackFill;
    private Control _backdropMount;
    private TextureRect _backdrop;
    private Texture2D _sourceTexture;

    public override void _Process(double delta)
    {
        if (!Enabled || IsInstalled)
            return;

        // Scene transitions and contract tests can free a previously found
        // BattleScreen between frames. Clear the stale C# wrapper before any
        // GetNode call so presentation teardown never logs an exception.
        if (!GodotObject.IsInstanceValid(_screen))
            _screen = null;
        _screen ??= FindInAncestorScopes<BattleScreen>(this);
        if (_screen == null)
            return;

        PanelContainer stagePanel = _screen.GetNodeOrNull<PanelContainer>("stagepanel");
        if (stagePanel == null)
            return;

        // Track layout and its Inspector offset do not depend on an
        // exploration screenshot. Direct/test battles therefore receive the
        // same authored foot line and adjustable frame position.
        InstallStageGeometry(stagePanel);

        BattleDirector director = BattleDirector.Instance;
        if (director?.PendingBattleBackdropTexture == null)
            return;

        _sourceTexture = director.PendingBattleBackdropTexture;
        Texture2D displayed = _sourceTexture;
        Rect2 region = director.PendingBattleBackdropRegion;
        if (region.Size.X > 1.0f && region.Size.Y > 1.0f)
        {
            displayed = new AtlasTexture
            {
                Atlas = _sourceTexture,
                Region = region,
                FilterClip = true,
            };
        }

        _blackFill = new ColorRect
        {
            Name = "BattleLowerBlackFill",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -100,
        };
        _screen.AddChild(_blackFill);
        _screen.MoveChild(_blackFill, 0);
        _blackFill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _backdropMount = new Control
        {
            Name = "ExplorationBattleStage",
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -90,
        };
        _screen.AddChild(_backdropMount);
        _screen.MoveChild(_backdropMount, 1);
        _backdropMount.AnchorLeft = 0.0f;
        _backdropMount.AnchorTop = 0.0f;
        _backdropMount.AnchorRight = 1.0f;
        _backdropMount.AnchorBottom = BackdropBottomRatio;
        _backdropMount.OffsetLeft = 0.0f;
        _backdropMount.OffsetTop = 0.0f;
        _backdropMount.OffsetRight = 0.0f;
        _backdropMount.OffsetBottom = 0.0f;

        _backdrop = new TextureRect
        {
            Name = "ExplorationBattleBackdrop",
            Texture = displayed,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, BackdropOpacity),
        };
        _backdropMount.AddChild(_backdrop);
        _backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Name = "ReadabilityShade",
            Color = new Color(0.025f, 0.045f, 0.055f, ShadeOpacity),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _backdrop.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private void InstallStageGeometry(PanelContainer stagePanel)
    {
        // The panel is only a layout host. A default Panel style would cover
        // the exploration image with grey, so make it fully transparent.
        stagePanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        stagePanel.AnchorLeft = 0.0f;
        stagePanel.AnchorTop = StageTopRatio;
        stagePanel.AnchorRight = 1.0f;
        stagePanel.AnchorBottom = BackdropBottomRatio;
        stagePanel.OffsetLeft = TrackOffset.X;
        stagePanel.OffsetTop = TrackOffset.Y;
        stagePanel.OffsetRight = TrackOffset.X;
        // 76 px is approximately 2 cm at the project's 96-DPI reference.
        // Lift the whole track, including combatant anchors, without changing
        // the exploration backdrop or any of the lower battle UI.
        stagePanel.OffsetBottom = TrackOffset.Y - TrackBottomClearancePixels;

        // End alignment puts the 144px TrackSlot row at the exploration floor.
        // Its occupant ends immediately above the cell-number label, so the
        // existing profile offsets place every sprite's feet on that line.
        VBoxContainer stage = stagePanel.GetNodeOrNull<VBoxContainer>("margin/stageVbox");
        if (stage != null)
            stage.Alignment = BoxContainer.AlignmentMode.End;
    }

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root == null) return null;
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static T FindInAncestorScopes<T>(Node start) where T : Node
    {
        Node scope = start;
        while (scope != null)
        {
            T found = FindDescendant<T>(scope);
            if (found != null) return found;
            scope = scope.GetParent();
        }
        return null;
    }
}
