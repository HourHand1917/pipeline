using Godot;

/// <summary>
/// 地图选择 UI。大门点击后弹出，三个按钮分别传送到 f2/f3/f4 第 0 房间的左边。
/// </summary>
[GlobalClass]
public partial class MapSelectUI : Control
{
    private const string DefaultConfigurationPath =
        "res://features/exploration/map_selection/resources/default_home_destinations.tres";
    [Signal] public delegate void ClosedEventHandler();

    [Export] private Button _f2Button;
    [Export] private Button _f3Button;
    [Export] private Button _f4Button;
    [Export] private Button _closeButton;

    public override void _Ready()
    {
        Visible = false;
<<<<<<< Updated upstream
        if (_f2Button != null) _f2Button.Pressed += () => Travel("f2_1", "f2_1left");
        if (_f3Button != null) _f3Button.Pressed += () => Travel("f3_0", "f3_0left");
        if (_f4Button != null) _f4Button.Pressed += () => Travel("f4", "f4entry");
        if (_closeButton != null) _closeButton.Pressed += Close;
=======
        MouseFilter = MouseFilterEnum.Stop;

        if (_closeButton != null)
            _closeButton.Pressed += Close;
        if (_confirmButton != null)
            _confirmButton.Pressed += ConfirmSelection;

        EnsureConfiguration();
        ApplyConfigurationText();
        RebuildDestinationList();
>>>>>>> Stashed changes
    }

    public void Open()
    {
<<<<<<< Updated upstream
=======
        if (_isOpen || _travelPending)
            return;

        EnsureConfiguration();
        _isOpen = true;
>>>>>>> Stashed changes
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        EmitSignal(SignalName.Closed);
    }

    private void Travel(string mapId, string spawnId)
    {
<<<<<<< Updated upstream
        MapManager.Instance?.TravelTo(new StringName(mapId), new StringName(spawnId));
=======
        if (_configuration == null)
        {
            GD.PushWarning("MapSelectUI: default MapSelectionConfig could not be loaded; using an empty safe configuration.");
            return;
        }

        if (_titleLabel != null) _titleLabel.Text = _configuration.Title;
        if (_subtitleLabel != null) _subtitleLabel.Text = _configuration.Subtitle;
        if (_confirmButton is PaintedImageButton)
        {
            // The supplied painted arrow already communicates the action visually.
            // Keep localized copy in the shared Godot tooltip instead of drawing over it.
            _confirmButton.Text = "";
            _confirmButton.TooltipText = _configuration.ConfirmText;
        }
        else if (_confirmButton != null)
        {
            _confirmButton.Text = _configuration.ConfirmText;
        }
        if (_closeButton != null) _closeButton.TooltipText = _configuration.CloseText;
    }

    private void EnsureConfiguration()
    {
        if (_configuration != null) return;
        _configuration = ResourceLoader.Load<MapSelectionConfig>(DefaultConfigurationPath)
            ?? new MapSelectionConfig();
    }

    private void RebuildDestinationList()
    {
        if (_destinationList == null || _configuration == null)
            return;

        foreach (Node child in _destinationList.GetChildren())
        {
            _destinationList.RemoveChild(child);
            child.QueueFree();
        }

        _destinationButtons.Clear();
        _destinationAvailable.Clear();
        _destinationUnavailableReasons.Clear();
        _selectedIndex = -1;

        for (int index = 0; index < _configuration.Destinations.Count; index++)
        {
            MapDestinationData destination = _configuration.Destinations[index];
            bool valid = MapSelectionValidator.Validate(
                destination,
                _configuration.MapRegistryPath,
                out string validationReason);
            bool levelMet = destination != null
                && (DataManager.Instance?.Lv ?? 0) >= destination.RequiredPlayerLevel;
            bool available = destination != null && destination.Unlocked && levelMet && valid;
            string unavailableReason = !valid
                ? validationReason
                : !levelMet
                    ? $"需要玩家等级 {destination?.RequiredPlayerLevel ?? 0}。{destination?.LockedHint}"
                    : destination?.LockedHint ?? "目的地已锁定";

            Button button = CreateDestinationButton(destination, available);
            int capturedIndex = index;
            button.Pressed += () => SelectDestination(capturedIndex);
            _destinationList.AddChild(button);
            _destinationButtons.Add(button);
            _destinationAvailable.Add(available);
            _destinationUnavailableReasons.Add(unavailableReason);
        }

        int firstAvailable = _destinationAvailable.FindIndex(value => value);
        if (firstAvailable >= 0)
            SelectDestination(firstAvailable);
        else if (_destinationButtons.Count > 0)
            SelectDestination(0);
        else
            ShowEmptyState();
    }

    private Button CreateDestinationButton(MapDestinationData destination, bool available)
    {
        string title = destination?.Title ?? "配置缺失";
        string category = destination?.CategoryText ?? "未知";
        string reward = destination?.RewardHint ?? "无";
        string risk = destination?.RiskText ?? "未知";
        string state = available ? "可前往" : "已封锁";

        var button = new Button
        {
            Text = $"{title}   [{category}]   {state}\n奖励：{reward}    风险：{risk}",
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(620, 116),
            ToggleMode = true,
            ButtonGroup = _destinationButtonGroup,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };

        button.AddThemeFontSizeOverride("font_size", 23);
        button.AddThemeColorOverride("font_color", available ? new Color("f2e7d4") : new Color("978b98"));
        button.AddThemeColorOverride("font_hover_color", new Color("fff1ca"));
        button.AddThemeColorOverride("font_pressed_color", new Color("2a2031"));
        button.AddThemeStyleboxOverride("normal", MakeDestinationStyle(new Color("171120e8"), new Color("62546b"), 3));
        button.AddThemeStyleboxOverride("hover", MakeDestinationStyle(new Color("2b2035f2"), new Color("d4a557"), 4));
        button.AddThemeStyleboxOverride("focus", MakeDestinationStyle(new Color("2b2035f2"), new Color("e7c778"), 4));
        button.AddThemeStyleboxOverride("pressed", MakeDestinationStyle(new Color("d2a853f2"), new Color("ffe19a"), 5));
        return button;
    }

    private static StyleBoxFlat MakeDestinationStyle(Color background, Color border, int borderWidth)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 24,
            ContentMarginRight = 20,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
        style.SetBorderWidthAll(borderWidth);
        return style;
    }

    private void SelectDestination(int index)
    {
        if (_configuration == null
            || index < 0
            || index >= _configuration.Destinations.Count
            || index >= _destinationButtons.Count)
            return;

        _selectedIndex = index;
        _destinationButtons[index].SetPressedNoSignal(true);
        MapDestinationData destination = _configuration.Destinations[index];
        bool available = _destinationAvailable[index];

        if (_detailTitleLabel != null)
            _detailTitleLabel.Text = destination?.Title ?? "配置缺失";
        if (_detailMetaLabel != null)
        {
            _detailMetaLabel.Text = destination == null
                ? "地图：--    出生点：--"
                : $"地图：{destination.MapId}    出生点：{destination.SpawnId}    类别：{destination.CategoryText}";
        }
        if (_detailDescriptionLabel != null)
            _detailDescriptionLabel.Text = destination?.Description ?? "没有目的地说明。";
        if (_statusLabel != null)
        {
            _statusLabel.Text = available
                ? $"奖励提示：{destination.RewardHint}    风险：{destination.RiskText}"
                : $"无法前往：{_destinationUnavailableReasons[index]}";
            _statusLabel.Modulate = available ? new Color("efc56f") : new Color("d98686");
        }
        if (_confirmButton != null)
            _confirmButton.Disabled = !available || _travelPending;

        EmitSignal(SignalName.DestinationSelectionChanged, index);
    }

    private void ConfirmSelection()
    {
        if (_travelPending
            || !_isOpen
            || _configuration == null
            || _selectedIndex < 0
            || _selectedIndex >= _configuration.Destinations.Count
            || !_destinationAvailable[_selectedIndex])
            return;

        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || mapManager.IsTraveling)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = "暂时无法出发，请稍后再试。";
                _statusLabel.Modulate = new Color("d98686");
            }
            return;
        }

        _travelPending = true;
        if (_confirmButton != null) _confirmButton.Disabled = true;
        if (_closeButton != null) _closeButton.Disabled = true;
        foreach (Button button in _destinationButtons) button.Disabled = true;

        MapDestinationData destination = _configuration.Destinations[_selectedIndex];
        mapManager.TravelTo(destination.MapId, destination.SpawnId);

        // MapManager acquires its own lock synchronously before its first await.
        // Release only this modal's lock and leave the HUD hidden during the iris transition.
        _isOpen = false;
        Visible = false;
        ReleaseMovementLock();
    }

    private void AcquireMovementLock()
    {
        if (_lockedPlayer != null && GodotObject.IsInstanceValid(_lockedPlayer))
            return;

        _lockedPlayer = PlayerController.Instance;
        _lockedPlayer?.LockMovement();
    }

    private void ReleaseMovementLock()
    {
        if (_lockedPlayer != null && GodotObject.IsInstanceValid(_lockedPlayer))
            _lockedPlayer.UnlockMovement();
        _lockedPlayer = null;
    }

    private Button FindFirstAvailableButton()
    {
        int index = _destinationAvailable.FindIndex(value => value);
        return index >= 0 && index < _destinationButtons.Count
            ? _destinationButtons[index]
            : null;
    }

    private void ShowEmptyState()
    {
        if (_detailTitleLabel != null) _detailTitleLabel.Text = "没有可用目的地";
        if (_detailMetaLabel != null) _detailMetaLabel.Text = "请在配置资源中添加目的地。";
        if (_detailDescriptionLabel != null) _detailDescriptionLabel.Text = "";
        if (_statusLabel != null) _statusLabel.Text = "";
        if (_confirmButton != null) _confirmButton.Disabled = true;
>>>>>>> Stashed changes
    }
}
