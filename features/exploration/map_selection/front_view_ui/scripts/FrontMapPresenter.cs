using Godot;
using System.Collections.Generic;

/// <summary>
/// Isolated painted front-view adapter for MapSelectUI.
/// It only mirrors/configures selection; MapSelectUI remains the sole owner of travel.
/// </summary>
[GlobalClass]
public partial class FrontMapPresenter : Control
{
    public const float DesignWidth = 1920.0f;
    public const float DesignHeight = 1080.0f;

    [ExportGroup("Bridge")]
    [Export] private MapSelectUI _owner;
    [Export] private Control _designCanvas;
    [Export] private Control _entranceLayer;

    [ExportGroup("Map Slots")]
    [Export] private FrontMapChoice _bossChoice;
    [Export] private FrontMapChoice _casinoChoice;
    [Export] private FrontMapChoice _marketChoice;
    [Export] private FrontMapChoice _pipeChoice;
    [Export] private FrontMapChoice _wastelandChoice;

    [ExportGroup("Image Buttons")]
    [Export] private PaintedImageButton _confirmButton;
    [Export] private PaintedImageButton _closeButton;

    public int ChoiceCount => _choices.Count;
    public int TooltipBoundChoiceCount { get; private set; }
    public int OpeningAnimationCount { get; private set; }
    public int SelectionVisualUpdateCount { get; private set; }
    public Vector2 DesignCanvasPosition => _designCanvas?.Position ?? Vector2.Zero;
    public Vector2 DesignCanvasScale => _designCanvas?.Scale ?? Vector2.Zero;
    public PaintedImageButton ConfirmImageButton => _confirmButton;
    public PaintedImageButton CloseImageButton => _closeButton;

    private readonly List<FrontMapChoice> _choices = new();
    private Tween _openTween;

    public override void _Ready()
    {
        _owner ??= GetParentOrNull<MapSelectUI>();
        _choices.AddRange(new[]
        {
            _bossChoice,
            _casinoChoice,
            _marketChoice,
            _pipeChoice,
            _wastelandChoice,
        });
        _choices.RemoveAll(choice => choice == null);

        foreach (FrontMapChoice choice in _choices)
        {
            FrontMapChoice captured = choice;
            choice.Pressed += () => OnChoicePressed(captured);
        }

        if (_owner != null)
        {
            _owner.Opened += OnOwnerOpened;
            _owner.Closed += OnOwnerClosed;
            _owner.DestinationSelectionChanged += OnSelectionChanged;
        }

        Resized += ApplyViewportLayout;
        ApplyViewportLayout();
        BindTooltips();
        SyncSelection();
    }

    public override void _ExitTree()
    {
        _openTween?.Kill();
        Resized -= ApplyViewportLayout;
        if (_owner != null && GodotObject.IsInstanceValid(_owner))
        {
            _owner.Opened -= OnOwnerOpened;
            _owner.Closed -= OnOwnerClosed;
            _owner.DestinationSelectionChanged -= OnSelectionChanged;
        }

        TooltipService service = TooltipService.Instance;
        if (service != null)
        {
            foreach (FrontMapChoice choice in _choices)
                service.HideFor(choice);
            service.HideTooltip();
        }
    }

    /// <summary>
    /// Keep the supplied 1920x1080 composition at viewport-height scale.
    /// At 1440x1080 this yields scale 1 and X=-240: only transparent/outer margins crop.
    /// </summary>
    public void ApplyViewportLayout()
    {
        if (_designCanvas == null)
            return;

        Vector2 viewportSize = Size;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            viewportSize = GetViewportRect().Size;

        ApplyViewportLayoutForSize(viewportSize);
    }

    /// <summary>Deterministic layout entry used by resize handling and smoke coverage.</summary>
    public void ApplyViewportLayoutForSize(Vector2 viewportSize)
    {
        if (_designCanvas == null || viewportSize.X <= 0 || viewportSize.Y <= 0)
            return;

        float scale = viewportSize.Y / DesignHeight;
        _designCanvas.Size = new Vector2(DesignWidth, DesignHeight);
        _designCanvas.Scale = Vector2.One * scale;
        _designCanvas.Position = new Vector2(
            (viewportSize.X - DesignWidth * scale) * 0.5f,
            0.0f);
    }

    public FrontMapChoice GetChoiceByMapId(StringName mapId)
    {
        foreach (FrontMapChoice choice in _choices)
        {
            if (!choice.Locked && choice.DestinationMapId == mapId)
                return choice;
        }
        return null;
    }

    public FrontMapChoice GetChoiceByName(string nodeName)
    {
        foreach (FrontMapChoice choice in _choices)
        {
            if (choice.Name.ToString() == nodeName)
                return choice;
        }
        return null;
    }

    private void OnOwnerOpened()
    {
        BindTooltips();
        SyncSelection();
        PlayOpenAnimation();

        FrontMapChoice selected = FindSelectedChoice();
        (selected ?? _bossChoice)?.GrabFocus();
    }

    private void OnOwnerClosed()
    {
        TooltipService.Instance?.HideTooltip();
    }

    private void OnSelectionChanged(int index)
    {
        SyncSelection();
    }

    private void OnChoicePressed(FrontMapChoice choice)
    {
        if (_owner == null || !_owner.IsOpen || choice == null)
            return;

        if (choice.Locked)
        {
            choice.PlayRejectedFeedback();
            return;
        }

        int index = FindDestinationIndex(choice.DestinationMapId);
        if (index < 0 || !_owner.IsDestinationAvailable(index))
        {
            choice.PlayRejectedFeedback();
            return;
        }

        // Deliberately selection-only. Existing right-side confirm button owns travel.
        _owner.SelectDestinationAt(index);
    }

    private void SyncSelection()
    {
        StringName selectedMapId = "";
        MapSelectionConfig config = _owner?.Configuration;
        int index = _owner?.SelectedIndex ?? -1;
        if (config != null && index >= 0 && index < config.Destinations.Count)
            selectedMapId = config.Destinations[index]?.MapId ?? new StringName("");

        foreach (FrontMapChoice choice in _choices)
        {
            bool selected = !choice.Locked
                && !selectedMapId.IsEmpty
                && choice.DestinationMapId == selectedMapId;
            choice.SetSelected(selected);
        }
        SelectionVisualUpdateCount++;
    }

    private FrontMapChoice FindSelectedChoice()
    {
        foreach (FrontMapChoice choice in _choices)
        {
            if (choice.IsSelected)
                return choice;
        }
        return null;
    }

    private int FindDestinationIndex(StringName mapId)
    {
        MapSelectionConfig config = _owner?.Configuration;
        if (config == null)
            return -1;

        for (int index = 0; index < config.Destinations.Count; index++)
        {
            if (config.Destinations[index]?.MapId == mapId)
                return index;
        }
        return -1;
    }

    private void BindTooltips()
    {
        TooltipService service = TooltipService.Instance;
        if (service == null)
            return;

        TooltipBoundChoiceCount = 0;
        foreach (FrontMapChoice choice in _choices)
        {
            service.HideFor(choice);
            service.ShowFor(choice, BuildTooltip(choice));
            TooltipBoundChoiceCount++;
        }
    }

    private TooltipData BuildTooltip(FrontMapChoice choice)
    {
        int index = choice.Locked ? -1 : FindDestinationIndex(choice.DestinationMapId);
        MapDestinationData destination = index >= 0
            ? _owner.Configuration.Destinations[index]
            : null;

        if (destination == null)
        {
            return new TooltipData
            {
                Title = choice.LockedDisplayName,
                Description = choice.LockedDescription,
                Icon = choice.TooltipIcon,
                Details = new Godot.Collections.Dictionary<string, string>
                {
                    { "状态", "尚未开放" },
                },
            };
        }

        bool available = _owner.IsDestinationAvailable(index);
        return new TooltipData
        {
            Title = destination.Title,
            Description = destination.Description,
            Icon = choice.TooltipIcon,
            Details = new Godot.Collections.Dictionary<string, string>
            {
                { "类型", destination.CategoryText },
                { "风险", destination.RiskText },
                { "奖励", destination.RewardHint },
                { "状态", available ? "可以前往" : destination.LockedHint },
            },
        };
    }

    private void PlayOpenAnimation()
    {
        OpeningAnimationCount++;
        if (_entranceLayer != null)
        {
            _openTween?.Kill();
            _entranceLayer.PivotOffset = new Vector2(DesignWidth * 0.5f, DesignHeight * 0.5f);
            _entranceLayer.Modulate = new Color(1, 1, 1, 0);
            _entranceLayer.Scale = new Vector2(0.975f, 0.975f);
            _openTween = CreateTween().SetParallel(true);
            _openTween.TweenProperty(_entranceLayer, "modulate", Colors.White, 0.18f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            _openTween.TweenProperty(_entranceLayer, "scale", Vector2.One, 0.22f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }

        for (int index = 0; index < _choices.Count; index++)
            _choices[index].PlayEntrance(0.035f * index);
    }
}
