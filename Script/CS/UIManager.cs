using Godot;
using System.Collections.Generic;

public partial class UIManager : CanvasLayer
{
    // ============ 信号 ============
    [Signal] public delegate void PlaceCardRequestedEventHandler(StringName cardId, Vector2I anchor, int rotationSteps);
    [Signal] public delegate void RemoveCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void LightCellRequestedEventHandler(Vector2I position);
    [Signal] public delegate void PlayCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void EndTurnRequestedEventHandler();
    [Signal] public delegate void StartBattleRequestedEventHandler();
    [Signal] public delegate void ClearBuildRequestedEventHandler();

    // ============ 引用 ============
    private BoardManager boardManager;
    private BattleManager battleManager;

    // ============ 状态变量 ============
    private StringName selectedCardId = "";
    private int selectedRotation = 0;
    private Vector2I hoveredBuildCell = new(-1, -1);

    // ============ UI 节点引用 ============
    private Control rootControl;
    private Control buildScreen;
    private Control battleScreen;
    private GridContainer buildGrid;
    private GridContainer battleGrid;
    private VBoxContainer inventoryBox;
    private VBoxContainer activationBox;
    private RichTextLabel logLabel;
    private RichTextLabel architectureLabel;
    private Label statusLabel;
    private Label hintLabel;
    private PanelContainer ghostPanel;
    private Label ghostLabel;

    private List<Button> buildButtons = new();
    private List<Button> battleButtons = new();

    // ============ 初始化 ============
    public void Setup(BoardManager board, BattleManager battle)
    {
        boardManager = board;
        battleManager = battle;
        BuildInterface();
    }

    // ============ 主循环 ============
    public override void _Process(double delta)
    {
        if (ghostPanel == null)
            return;

        bool shouldShow = buildScreen != null
            && buildScreen.Visible
            && selectedCardId != "";

        ghostPanel.Visible = shouldShow;
        if (shouldShow)
        {
            ghostPanel.Position = GetViewport().GetMousePosition() + new Vector2(20, 20);
            UpdateGhostText();
        }
    }

    // ============ 输入处理 ============
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent
            && keyEvent.Pressed
            && !keyEvent.Echo
            && keyEvent.Keycode == Key.R
            && selectedCardId != "")
        {
            selectedRotation = (selectedRotation + 1) % 4;
            RefreshAll();
        }
    }

    // ============ UI 构建 ============
    private void BuildInterface()
    {
        // 根控件
        rootControl = new Control();
        rootControl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(rootControl);

        // 背景
        var background = new ColorRect();
        background.Color = new Color("#171717");
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rootControl.AddChild(background);

        // 主边距
        var mainMargin = new MarginContainer();
        mainMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainMargin.AddThemeConstantOverride("margin_left", 18);
        mainMargin.AddThemeConstantOverride("margin_right", 18);
        mainMargin.AddThemeConstantOverride("margin_top", 18);
        mainMargin.AddThemeConstantOverride("margin_bottom", 18);
        rootControl.AddChild(mainMargin);

        // 左右分栏
        var columns = new HSplitContainer();
        columns.SplitOffset = 850;
        mainMargin.AddChild(columns);

        // 游戏面板
        var gamePanel = new PanelContainer();
        columns.AddChild(gamePanel);

        var gameVbox = new VBoxContainer();
        gameVbox.AddThemeConstantOverride("separation", 10);
        gamePanel.AddChild(gameVbox);

        var title = new Label();
        title.Text = "黑匣子｜3×3 构筑战斗核心框架";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 24);
        gameVbox.AddChild(title);

        buildScreen = CreateBuildScreen();
        gameVbox.AddChild(buildScreen);

        battleScreen = CreateBattleScreen();
        battleScreen.Visible = false;
        gameVbox.AddChild(battleScreen);

        // 架构面板
        var architecturePanel = new PanelContainer();
        architecturePanel.CustomMinimumSize = new Vector2(360, 0);
        columns.AddChild(architecturePanel);

        var architectureVbox = new VBoxContainer();
        architectureVbox.AddThemeConstantOverride("separation", 8);
        architecturePanel.AddChild(architectureVbox);

        var archTitle = new Label();
        archTitle.Text = "程序架构";
        archTitle.HorizontalAlignment = HorizontalAlignment.Center;
        archTitle.AddThemeFontSizeOverride("font_size", 22);
        architectureVbox.AddChild(archTitle);

        architectureLabel = new RichTextLabel();
        architectureLabel.BbcodeEnabled = true;
        architectureLabel.FitContent = false;
        architectureLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        architectureVbox.AddChild(architectureLabel);

        // 幽灵面板
        ghostPanel = new PanelContainer();
        ghostPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        ghostPanel.Visible = false;
        ghostPanel.CustomMinimumSize = new Vector2(145, 70);
        rootControl.AddChild(ghostPanel);

        ghostLabel = new Label();
        ghostLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ghostLabel.VerticalAlignment = VerticalAlignment.Center;
        ghostPanel.AddChild(ghostLabel);
    }

    private Control CreateBuildScreen()
    {
        var screen = new HBoxContainer();
        screen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        screen.AddThemeConstantOverride("separation", 16);

        // 左侧物品库
        var left = new VBoxContainer();
        left.CustomMinimumSize = new Vector2(260, 0);
        screen.AddChild(left);

        var inventoryTitle = new Label();
        inventoryTitle.Text = "物品库";
        inventoryTitle.AddThemeFontSizeOverride("font_size", 20);
        left.AddChild(inventoryTitle);

        inventoryBox = new VBoxContainer();
        inventoryBox.AddThemeConstantOverride("separation", 8);
        left.AddChild(inventoryBox);

        // 中间板子
        var center = new VBoxContainer();
        center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        center.Alignment = BoxContainer.AlignmentMode.Center;
        screen.AddChild(center);

        hintLabel = new Label();
        hintLabel.Text = "选择物品，移动鼠标查看占位轮廓。R旋转。";
        hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        center.AddChild(hintLabel);

        buildGrid = new GridContainer();
        buildGrid.Columns = 3;
        buildGrid.AddThemeConstantOverride("h_separation", 8);
        buildGrid.AddThemeConstantOverride("v_separation", 8);
        center.AddChild(buildGrid);

        for (int index = 0; index < 9; index++)
        {
            var button = new Button();
            button.CustomMinimumSize = new Vector2(142, 142);
            button.Text = "";

            int capturedIndex = index;
            button.Pressed += () => OnBuildCellPressed(capturedIndex);
            button.MouseEntered += () => OnBuildCellHovered(capturedIndex);
            button.MouseExited += OnBuildGridMouseExited;

            buildGrid.AddChild(button);
            buildButtons.Add(button);
        }

        // 操作按钮行
        var actions = new HBoxContainer();
        actions.Alignment = BoxContainer.AlignmentMode.Center;
        center.AddChild(actions);

        var rotateButton = new Button();
        rotateButton.Text = "旋转 R";
        rotateButton.Pressed += OnRotatePressed;
        actions.AddChild(rotateButton);

        var exampleButton = new Button();
        exampleButton.Text = "自动示例";
        exampleButton.Pressed += PlaceExampleBuild;
        actions.AddChild(exampleButton);

        var clearButton = new Button();
        clearButton.Text = "清空";
        clearButton.Pressed += () => EmitSignal(SignalName.ClearBuildRequested);
        actions.AddChild(clearButton);

        var startButton = new Button();
        startButton.Text = "进入战斗";
        startButton.Pressed += () => EmitSignal(SignalName.StartBattleRequested);
        actions.AddChild(startButton);

        return screen;
    }

    private Control CreateBattleScreen()
    {
        var screen = new VBoxContainer();
        screen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        statusLabel = new Label();
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        statusLabel.AddThemeFontSizeOverride("font_size", 18);
        screen.AddChild(statusLabel);

        var content = new HBoxContainer();
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.Alignment = BoxContainer.AlignmentMode.Center;
        content.AddThemeConstantOverride("separation", 16);
        screen.AddChild(content);

        // 板子列
        var boardColumn = new VBoxContainer();
        content.AddChild(boardColumn);

        battleGrid = new GridContainer();
        battleGrid.Columns = 3;
        battleGrid.AddThemeConstantOverride("h_separation", 8);
        battleGrid.AddThemeConstantOverride("v_separation", 8);
        boardColumn.AddChild(battleGrid);

        for (int index = 0; index < 9; index++)
        {
            var button = new Button();
            button.CustomMinimumSize = new Vector2(142, 142);

            int capturedIndex = index;
            button.Pressed += () => OnBattleCellPressed(capturedIndex);

            battleGrid.AddChild(button);
            battleButtons.Add(button);
        }

        var endTurnButton = new Button();
        endTurnButton.Text = "结束回合";
        endTurnButton.Pressed += () => EmitSignal(SignalName.EndTurnRequested);
        boardColumn.AddChild(endTurnButton);

        // 侧边栏
        var side = new VBoxContainer();
        side.CustomMinimumSize = new Vector2(250, 0);
        content.AddChild(side);

        var activationTitle = new Label();
        activationTitle.Text = "可发动物品";
        side.AddChild(activationTitle);

        activationBox = new VBoxContainer();
        side.AddChild(activationBox);

        var dummyLabel = new Label();
        dummyLabel.Text = "训练假人：每回合获得1盾并攻击1";
        dummyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        side.AddChild(dummyLabel);

        logLabel = new RichTextLabel();
        logLabel.BbcodeEnabled = false;
        logLabel.CustomMinimumSize = new Vector2(250, 260);
        logLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        side.AddChild(logLabel);

        var backButton = new Button();
        backButton.Text = "返回构筑";
        backButton.Pressed += ShowBuildScreen;
        side.AddChild(backButton);

        return screen;
    }

    // ============ 刷新方法 ============
    public void RefreshAll()
    {
        if (boardManager == null)
            return;

        RefreshInventory();
        RefreshBuildBoard();
        RefreshBattleBoard();
        RefreshStatus();
        RefreshActivationButtons();
        RefreshArchitecture();
    }

    private void RefreshInventory()
    {
        foreach (Node child in inventoryBox.GetChildren())
            child.QueueFree();

        foreach (var card in DataManager.Instance.GetAllCards())
        {
            bool alreadyPlaced = false;
            foreach (var runtime in boardManager.runtime_cards)
            {
                if (runtime.Data.Id == card.Id)
                {
                    alreadyPlaced = true;
                    break;
                }
            }

            var button = new Button();
            button.Disabled = alreadyPlaced;
            button.Text = $"{card.IconText} {card.DisplayName}｜占用{card.ShapeOffsets.Count}格\n{card.Description}";
            button.CustomMinimumSize = new Vector2(0, 74);

            StringName cardId = card.Id;
            button.Pressed += () => OnInventoryCardPressed(cardId);

            inventoryBox.AddChild(button);
        }
    }

    private void RefreshBuildBoard()
    {
        var previewPositions = new List<Vector2I>();
        bool previewValid = false;

        if (selectedCardId != "" && hoveredBuildCell.X >= 0)
        {
            var selectedCard = DataManager.Instance.GetCard(selectedCardId);
            previewPositions = boardManager.GetPreviewCells(selectedCard, hoveredBuildCell, selectedRotation);
            previewValid = boardManager.CanPlace(selectedCard, hoveredBuildCell, selectedRotation);
        }

        for (int index = 0; index < 9; index++)
        {
            var position = IndexToPosition(index);
            var button = buildButtons[index];
            var cell = boardManager.GetCell(position);
            var runtime = boardManager.GetCardByCell(position);

            button.Modulate = Colors.White;
            button.Text = "";

            if (runtime != null)
            {
                button.Text = $"{runtime.Data.IconText}\n{runtime.Data.DisplayName}";
            }

            if (previewPositions.Contains(position))
            {
                button.Text = $"{DataManager.Instance.GetCard(selectedCardId).IconText}\n{(previewValid ? "可放置" : "冲突")}";
                button.Modulate = previewValid ? new Color(1.0f, 1.0f, 1.0f) : new Color(0.85f, 0.45f, 0.45f);
            }
        }
    }

    private void RefreshBattleBoard()
    {
        for (int index = 0; index < 9; index++)
        {
            var position = IndexToPosition(index);
            var button = battleButtons[index];
            var cell = boardManager.GetCell(position);
            var runtime = boardManager.GetCardByCell(position);

            button.Modulate = Colors.White;
            button.Text = "";

            if (runtime == null)
            {
                button.Disabled = true;
                continue;
            }

            button.Disabled = battleManager.CurrentPhase != BattleManager.Phase.PlayerLighting;

            button.Text = $"{runtime.Data.IconText}\n{runtime.Data.DisplayName}\n{(cell.IsLit ? "点亮" : "未点亮")}";

            if (cell.IsLit)
            {
                button.Modulate = new Color(1.0f, 1.0f, 0.65f);
            }

            if (runtime.CooldownRemaining > 0)
            {
                button.Modulate = new Color(0.55f, 0.55f, 0.55f);
                button.Text += $"\n冷却{runtime.CooldownRemaining}";
            }
        }
    }

    private void RefreshStatus()
    {
        if (statusLabel == null)
            return;

        statusLabel.Text = string.Format(
            "回合 {0}　玩家 HP {1}/{2}　能量 {3}　护盾 {4}　｜　假人 HP {5}/{6}　护盾 {7}",
            battleManager.RoundNumber,
            battleManager.PlayerHp,
            battleManager.PlayerMaxHp,
            battleManager.PlayerEnergy,
            battleManager.PlayerShield,
            battleManager.EnemyHp,
            battleManager.EnemyMaxHp,
            battleManager.EnemyShield
        );
    }

    private void RefreshActivationButtons()
    {
        if (activationBox == null)
            return;

        foreach (Node child in activationBox.GetChildren())
            child.QueueFree();

        foreach (var runtime in boardManager.runtime_cards)
        {
            if (!boardManager.CheckCardReady(runtime))
                continue;

            var button = new Button();
            button.Text = $"发动｜{runtime.Data.DisplayName}";

            int instanceId = runtime.InstanceId;
            button.Pressed += () => EmitSignal(SignalName.PlayCardRequested, instanceId);

            activationBox.AddChild(button);
        }
    }

    private void RefreshArchitecture()
    {
        if (architectureLabel == null)
            return;

        string runtimeLines = "";
        foreach (var runtime in boardManager.runtime_cards)
        {
            runtimeLines += string.Format(
                "\n• {0}  cells={1}  ready={2}  cooldown={3}",
                runtime.Data.DisplayName,
                runtime.OccupiedCells.Count,
                runtime.IsReady,
                runtime.CooldownRemaining
            );
        }

        architectureLabel.Text = string.Format(
            "[b]DataManager[/b]\n保存构筑、卡牌数据库\n\n"
            + "[b]BoardManager[/b]\ncells[3][3]\n放置、占位、点亮、Ready判断、冷却\n\n"
            + "[b]BattleManager[/b]\n回合状态、能量验证、发动二次验证、假人行动\n\n"
            + "[b]UIManager[/b]\n鼠标轮廓、格子预览、发动按钮、HUD\n\n"
            + "[b]EffectResolver[/b]\n伤害、护盾、能量、回复\n\n"
            + "[b]当前运行数据[/b]\nphase = {0}\nround = {1}\nplayer_energy = {2}\nruntime_cards = {3}\n{4}",
            BattleManager.PhaseKeys.Keys()[(int)battleManager.CurrentPhase],
            battleManager.RoundNumber,
            battleManager.PlayerEnergy,
            boardManager.runtime_cards.Count,
            runtimeLines
        );
    }

    private void UpdateGhostText()
    {
        var card = DataManager.Instance.GetCard(selectedCardId);
        if (card == null)
            return;

        var shape = card.GetRotatedShape(selectedRotation);
        int maxX = 0;
        int maxY = 0;
        foreach (var point in shape)
        {
            maxX = Mathf.Max(maxX, point.X);
            maxY = Mathf.Max(maxY, point.Y);
        }

        var rows = new List<string>();
        for (int row = 0; row <= maxY; row++)
        {
            string line = "";
            for (int column = 0; column <= maxX; column++)
            {
                line += shape.Contains(new Vector2I(column, row)) ? "■ " : "· ";
            }
            rows.Add(line);
        }

        ghostLabel.Text = string.Format(
            "{0}｜占用{1}格\n{2}",
            card.DisplayName,
            shape.Count,
            string.Join("\n", rows)
        );
    }

    // ============ 事件回调 ============
    private void OnInventoryCardPressed(StringName cardId)
    {
        selectedCardId = cardId;
        selectedRotation = 0;
        RefreshAll();
    }

    private void OnBuildCellPressed(int index)
    {
        var position = IndexToPosition(index);
        var runtime = boardManager.GetCardByCell(position);

        if (runtime != null)
        {
            EmitSignal(SignalName.RemoveCardRequested, runtime.InstanceId);
            return;
        }

        if (selectedCardId == "")
        {
            AppendLog("请先选择一个物品。");
            return;
        }

        EmitSignal(SignalName.PlaceCardRequested, selectedCardId, position, selectedRotation);

        if (boardManager.GetCardByCell(position) != null)
            selectedCardId = "";

        RefreshAll();
    }

    private void OnBuildCellHovered(int index)
    {
        hoveredBuildCell = IndexToPosition(index);
        RefreshAll();
    }

    private void OnBuildGridMouseExited()
    {
        hoveredBuildCell = new Vector2I(-1, -1);
        RefreshAll();
    }

    private void OnBattleCellPressed(int index)
    {
        EmitSignal(SignalName.LightCellRequested, IndexToPosition(index));
    }

    private void OnRotatePressed()
    {
        if (selectedCardId == "")
        {
            AppendLog("请先选择一个物品。");
            return;
        }

        selectedRotation = (selectedRotation + 1) % 4;
        RefreshAll();
    }

    private async void PlaceExampleBuild()
    {
        EmitSignal(SignalName.ClearBuildRequested);
        await ToSignal(GetTree(), "process_frame");

        EmitSignal(SignalName.PlaceCardRequested, "revolver", new Vector2I(0, 0), 0);
        EmitSignal(SignalName.PlaceCardRequested, "shield", new Vector2I(2, 0), 0);
        EmitSignal(SignalName.PlaceCardRequested, "battery", new Vector2I(2, 2), 0);
        EmitSignal(SignalName.PlaceCardRequested, "medkit", new Vector2I(0, 1), 1);
    }

    // ============ 工具方法 ============
    private Vector2I IndexToPosition(int index)
    {
        return new Vector2I(index % 3, index / 3);
    }

    public void ShowBuildScreen()
    {
        buildScreen.Visible = true;
        battleScreen.Visible = false;
        selectedCardId = "";
        RefreshAll();
    }

    public void ShowBattleScreen()
    {
        buildScreen.Visible = false;
        battleScreen.Visible = true;
        selectedCardId = "";
        RefreshAll();
    }

    public void AppendLog(string text)
    {
        if (logLabel == null)
            return;

        logLabel.AppendText(text + "\n");
        logLabel.ScrollToLine(Mathf.Max(0, logLabel.GetLineCount() - 1));
    }

    public void ShowResult(bool playerWon)
    {
        AppendLog(playerWon ? "胜利！" : "失败。");
    }
}