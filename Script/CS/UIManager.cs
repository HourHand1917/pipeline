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

    // ============ 外部引用 ============
    private BoardManager boardManager;
    private BattleManager battleManager;

    // ============ 状态 ============
    private StringName selectedCardId = "";
    private int selectedRotation = 0;
    private Vector2I hoveredBuildCell = new(-1, -1);
    private int currentGridWidth = 3;
    private int currentGridHeight = 2;

    // ============ 编辑器导出的节点引用 ============
    [Export] private Control buildscreen;
    [Export] private Control battlescreen;
    [Export] private GridContainer buildgrid;
    [Export] private GridContainer battlegrid;
    [Export] private VBoxContainer inventorybox;
    [Export] private VBoxContainer activationbox;
    [Export] private RichTextLabel loglabel;
    [Export] private Label statuslabel;
    [Export] private Panel ghostpanel;
    [Export] private Label ghostlabel;
    [Export] private Button rotatebutton;
    [Export] private Button examplebutton;
    [Export] private Button clearbutton;
    [Export] private Button startbattlebutton;
    [Export] private Button endturnbutton;
    [Export] private Button backbutton;
    [Export] private Button grid3x2button;
    [Export] private Button grid3x3button;
    [Export] private Button grid4x3button;


    private List<Button> buildButtons = new();
    private List<Button> battleButtons = new();

    // ============ 初始化 ============
    public void Setup(BoardManager board, BattleManager battle)
    {
        boardManager = board;
        battleManager = battle;

        // 收集格子按钮引用
        foreach (Node child in buildgrid.GetChildren())
        {
            if (child is Button btn)
                buildButtons.Add(btn);
        }
        foreach (Node child in battlegrid.GetChildren())
        {
            if (child is Button btn)
                battleButtons.Add(btn);
        }

        // 连接构筑格子按钮信号
        for (int i = 0; i < buildButtons.Count; i++)
        {
            int index = i;
            buildButtons[i].Pressed += () => OnBuildCellPressed(index);
            buildButtons[i].MouseEntered += () => OnBuildCellHovered(index);
            buildButtons[i].MouseExited += OnBuildGridMouseExited;
        }

        // 连接战斗格子按钮信号
        for (int i = 0; i < battleButtons.Count; i++)
        {
            int index = i;
            battleButtons[i].Pressed += () => OnBattleCellPressed(index);
        }

        // 连接操作按钮信号
        rotatebutton.Pressed += OnRotatePressed;
        examplebutton.Pressed += PlaceExampleBuild;
        clearbutton.Pressed += () => EmitSignal(SignalName.ClearBuildRequested);
        startbattlebutton.Pressed += () => EmitSignal(SignalName.StartBattleRequested);
        endturnbutton.Pressed += () => EmitSignal(SignalName.EndTurnRequested);
        backbutton.Pressed += ShowBuildScreen;
        grid3x2button.Pressed += () => SwitchGrid(3, 2);
        grid3x3button.Pressed += () => SwitchGrid(3, 3);
        grid4x3button.Pressed += () => SwitchGrid(4, 3);

        BuildPositionMap();
        UpdateGridState();
    }

    // ============ 主循环 ============
    public override void _Process(double delta)
    {
        if (ghostpanel == null) return;

        bool shouldShow = buildscreen != null
            && buildscreen.Visible
            && selectedCardId != "";

        ghostpanel.Visible = shouldShow;
        if (shouldShow)
        {
            ghostpanel.Position = GetViewport().GetMousePosition() + new Vector2(20, 20);
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

    // ============ 公共方法 ============
    public void RefreshAll()
    {
        if (boardManager == null) return;
        RefreshInventory();
        RefreshBuildBoard();
        RefreshBattleBoard();
        RefreshStatus();
        RefreshActivationButtons();
        //RefreshArchitecture();
    }

    public void ShowBuildScreen()
    {
        buildscreen.Visible = true;
        battlescreen.Visible = false;
        selectedCardId = "";
        RefreshAll();
    }

    public void ShowBattleScreen()
    {
        buildscreen.Visible = false;
        battlescreen.Visible = true;
        selectedCardId = "";
        RefreshAll();
    }

    public void AppendLog(string text)
    {
        if (loglabel == null) return;
        loglabel.AppendText(text + "\n");
        loglabel.ScrollToLine(Mathf.Max(0, loglabel.GetLineCount() - 1));
    }

    public void ShowResult(bool playerWon)
    {
        AppendLog(playerWon ? "胜利！" : "失败。");
    }

    // ============ 私有刷新方法 ============
    private void RefreshInventory()
    {
        foreach (Node child in inventorybox.GetChildren())
            child.QueueFree();

        foreach (var card in DataManager.Instance.GetAllCards())
        {
            StringName cardId = (StringName)card.Get("id");

            bool alreadyPlaced = false;
            foreach (var runtime in boardManager.runtime_cards)
            {
                var data = runtime.Get("data").As<GodotObject>();
                if ((StringName)data.Get("id") == cardId)
                {
                    alreadyPlaced = true;
                    break;
                }
            }

            var button = new Button();
            button.Disabled = alreadyPlaced;
            int count = card.Get("shape_offsets").As<Godot.Collections.Array<Vector2I>>().Count;
            button.Text = $"{card.Get("icon_text")} {card.Get("display_name")}｜占用{count}格\n{card.Get("description")}";
            button.CustomMinimumSize = new Vector2(0, 74);
            button.Pressed += () => OnInventoryCardPressed(cardId);
            inventorybox.AddChild(button);
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

        for (int index = 0; index < 12; index++)
        {
            var position = buttonPositions[index];
            var button = buildButtons[index];

            // 跳过不可用的按钮
            if (!IsCellInCurrentGrid(index))
            {
                button.Text = "";
                continue;
            }

            var cell = boardManager.GetCell(position);
            var runtime = boardManager.GetCardByCell(position);

            button.Modulate = Colors.White;
            button.Text = "";

            if (runtime != null)
            {
                var data = runtime.Get("data").As<GodotObject>();
                button.Text = $"{data.Get("icon_text")}\n{data.Get("display_name")}";
            }

            if (previewPositions.Contains(position))
            {
                var card = DataManager.Instance.GetCard(selectedCardId);
                button.Text = $"{card.Get("icon_text")}\n{(previewValid ? "可放置" : "冲突")}";
                button.Modulate = previewValid ? Colors.White : new Color(0.85f, 0.45f, 0.45f);
            }
        }
    }

        private void RefreshBattleBoard()
    {
        for (int index = 0; index < 12; index++)
        {
            var position = buttonPositions[index];
            var button = battleButtons[index];

            if (!IsCellInCurrentGrid(index))
            {
                button.Text = "";
                button.Disabled = true;
                continue;
            }

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

            var data = runtime.Get("data").As<GodotObject>();
            bool isLit = (bool)cell.Get("is_lit");
            int cooldown = (int)runtime.Get("cooldown_remaining");

            button.Text = $"{data.Get("icon_text")}\n{data.Get("display_name")}\n{(isLit ? "点亮" : "未点亮")}";

            if (isLit)
                button.Modulate = new Color(1.0f, 1.0f, 0.65f);

            if (cooldown > 0)
            {
                button.Modulate = new Color(0.55f, 0.55f, 0.55f);
                button.Text += $"\n冷却{cooldown}";
            }
        }
    }

    private void RefreshStatus()
    {
        if (statuslabel == null) return;
        statuslabel.Text = string.Format(
            "回合 {0}　玩家 HP {1}/{2}　能量 {3}　护盾 {4}　｜　假人 HP {5}/{6}　护盾 {7}",
            battleManager.RoundNumber, battleManager.PlayerHp, battleManager.PlayerMaxHp,
            battleManager.PlayerEnergy, battleManager.PlayerShield,
            battleManager.EnemyHp, battleManager.EnemyMaxHp, battleManager.EnemyShield);
    }

    private void SwitchGrid(int width, int height)
    {
        if (currentGridWidth == width && currentGridHeight == height)
            return;

        currentGridWidth = width;
        currentGridHeight = height;

        boardManager.ResetBoard(width, height);
        UpdateGridState();

        selectedCardId = "";
        selectedRotation = 0;
        hoveredBuildCell = new Vector2I(-1, -1);
        RefreshAll();
    }

    private void UpdateGridState()
    {
        buildgrid.Columns = 4;
        battlegrid.Columns = 4;

        for (int i = 0; i < buildButtons.Count; i++)
        {
            buildButtons[i].Visible = true;
            buildButtons[i].Disabled = !IsCellInCurrentGrid(i);
        }

        for (int i = 0; i < battleButtons.Count; i++)
        {
            battleButtons[i].Visible = true;
            battleButtons[i].Disabled = !IsCellInCurrentGrid(i);
        }
    }

    private bool IsCellInCurrentGrid(int index)
    {
        int row = index / 4;
        int col = index % 4;
        return col < currentGridWidth && row < currentGridHeight;
    }
    private void RefreshActivationButtons()
    {
        if (activationbox == null) return;
        foreach (Node child in activationbox.GetChildren())
            child.QueueFree();

        foreach (var runtime in boardManager.runtime_cards)
        {
            if (!boardManager.CheckCardReady(runtime)) continue;

            var data = runtime.Get("data").As<GodotObject>();
            var button = new Button();
            button.Text = $"发动｜{data.Get("display_name")}";
            int id = (int)runtime.Get("instance_id");
            button.Pressed += () => EmitSignal(SignalName.PlayCardRequested, id);
            activationbox.AddChild(button);
        }
    }

//

    private void UpdateGhostText()
    {
        var card = DataManager.Instance.GetCard(selectedCardId);
        if (card == null) return;

        var shape = card.Call("get_rotated_shape", selectedRotation)
            .As<Godot.Collections.Array<Vector2I>>();
        int maxX = 0, maxY = 0;
        foreach (var p in shape) { maxX = Mathf.Max(maxX, p.X); maxY = Mathf.Max(maxY, p.Y); }

        var rows = new List<string>();
        for (int y = 0; y <= maxY; y++)
        {
            string line = "";
            for (int x = 0; x <= maxX; x++)
                line += shape.Contains(new Vector2I(x, y)) ? "■ " : "· ";
            rows.Add(line);
        }

        ghostlabel.Text = string.Format("{0}｜占用{1}格\n{2}",
            card.Get("display_name"), shape.Count, string.Join("\n", rows));
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
        var position = buttonPositions[index];
        var runtime = boardManager.GetCardByCell(position);

        if (runtime != null)
        {
            EmitSignal(SignalName.RemoveCardRequested, (int)runtime.Get("instance_id"));
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
        hoveredBuildCell = buttonPositions[index];
        RefreshAll();
    }

    private void OnBuildGridMouseExited()
    {
        hoveredBuildCell = new Vector2I(-1, -1);
        RefreshAll();
    }

    private void OnBattleCellPressed(int index)
    {
        EmitSignal(SignalName.LightCellRequested, buttonPositions[index]);
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

// 按钮索引 → 二维坐标的映射表（始终 4 列）
    private Vector2I[] buttonPositions = new Vector2I[12];

    private void BuildPositionMap()
    {
        for (int i = 0; i < 12; i++)
        {
            buttonPositions[i] = new Vector2I(i % 4, i / 4);
        }
    }
}