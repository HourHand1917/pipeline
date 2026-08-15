using Godot;
using Godot.Collections;
using System.Collections.Generic;

/// <summary>
/// 构筑 Canvas（重构版）。
/// 以覆盖层形式显示在现有 UI 之上，负责卡牌选择与桌面摆放。
/// 构筑结果通过 DataManager 的 SaveBuild 接口持久化，
/// 战斗系统通过 DataManager 的 LoadBuild 读取。
/// </summary>
[GlobalClass]
public partial class BuildScreen : Control
{
    // ============ 信号 ============
    [Signal] public delegate void PlaceCardRequestedEventHandler(StringName cardId, Vector2I anchor, int rotationSteps);
    [Signal] public delegate void RemoveCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void StartBattleRequestedEventHandler();
    [Signal] public delegate void ClearBuildRequestedEventHandler();
    [Signal] public delegate void BoardSizeRequestedEventHandler(Vector2I size);
    [Signal] public delegate void BuildCanvasClosedEventHandler();

    // ============ 外部引用 ============
    private BoardManager boardManager;

    // ============ 状态 ============
    private StringName selectedCardId = "";
    private int selectedRotation = 0;
    private Vector2I hoveredBuildCell = new(-1, -1);
    private int currentGridWidth = 3;
    private int currentGridHeight = 2;

    // ============ 编辑器 Export ============
    [Export] private GridContainer buildgrid;
    [Export] private VBoxContainer inventorybox;
    [Export] private Button rotatebutton;
    [Export] private Button examplebutton;
    [Export] private Button clearbutton;
    [Export] private Button startbattlebutton;
    [Export] private Button grid3x2button;
    [Export] private Button grid3x3button;
    [Export] private Button grid4x3button;

    // 最大格子按钮数（4×3 棋盘的按钮总数，与场景中预创建的按钮数一致）
    private const int MAX_GRID_BUTTONS = 12;

    // ============ 内部数据 ============
    private List<Button> buildButtons = new();
    private Vector2I[] buttonPositions = new Vector2I[MAX_GRID_BUTTONS];

    // ================================================================
    //  Setup
    // ================================================================

    public void Setup(BoardManager board)
    {
        boardManager = board;

        // 收集格子按钮
        foreach (Node child in buildgrid.GetChildren())
        {
            if (child is Button btn)
                buildButtons.Add(btn);
        }

        // 二维坐标映射
        for (int i = 0; i < MAX_GRID_BUTTONS; i++)
            buttonPositions[i] = new Vector2I(i % 4, i / 4);

        // 格子按钮信号
        for (int i = 0; i < buildButtons.Count; i++)
        {
            int index = i;
            buildButtons[i].Pressed += () => OnBuildCellPressed(index);
            buildButtons[i].MouseEntered += () => OnBuildCellHovered(index);
            buildButtons[i].MouseExited += () => { hoveredBuildCell = new(-1, -1); RefreshBoard(); };
        }

        // 操作按钮
        rotatebutton.Pressed += OnRotatePressed;
        examplebutton.Pressed += PlaceExampleBuild;
        clearbutton.Pressed += () => EmitSignal(SignalName.ClearBuildRequested);
        startbattlebutton.Pressed += () => EmitSignal(SignalName.StartBattleRequested);
        grid3x2button.Pressed += () => RequestBoardSize(3, 2);
        grid3x3button.Pressed += () => RequestBoardSize(3, 3);
        grid4x3button.Pressed += () => RequestBoardSize(4, 3);

        UpdateGridState();
        DataManager.Instance.ItemBagChanged += RefreshAll;
    }

    // ================================================================
    //  Overlay 控制
    // ================================================================

    /// <summary>
    /// 以覆盖层形式打开构筑 Canvas。会刷新物品栏与桌面。
    /// </summary>
    public void Open()
    {
        Visible = true;
        RefreshAll();
    }

    /// <summary>
    /// 关闭覆盖层并发出信号。
    /// </summary>
    public void Close()
    {
        Visible = false;
        EmitSignal(SignalName.BuildCanvasClosed);
    }

    /// <summary>
    /// 切换显示/隐藏。
    /// </summary>
    public void Toggle()
    {
        if (Visible) Close(); else Open();
    }

    // ================================================================
    //  公共接口
    // ================================================================

    public void RefreshAll()
    {
        RefreshInventory();
        RefreshBoard();
    }

    public void ShowMessage(string text)
    {
        GD.Print($"[BuildCanvas] {text}");
    }

    public Vector2I GetHoveredCell() => hoveredBuildCell;
    public StringName GetSelectedCardId() => selectedCardId;
    public int GetSelectedRotation() => selectedRotation;
    public int GetGridWidth() => currentGridWidth;
    public int GetGridHeight() => currentGridHeight;
    public Vector2I GetButtonPosition(int index) => buttonPositions[index];

    public void ClearSelection()
    {
        selectedCardId = "";
        selectedRotation = 0;
    }

    // ================================================================
    //  板子尺寸
    // ================================================================

    private void RequestBoardSize(int width, int height)
    {
        if (currentGridWidth == width && currentGridHeight == height) return;
        EmitSignal(SignalName.BoardSizeRequested, new Vector2I(width, height));
    }

    public void ApplyBoardSize(int width, int height)
    {
        currentGridWidth = width;
        currentGridHeight = height;
        ClearSelection();
        UpdateGridState();
        RefreshAll();
    }

    private void UpdateGridState()
    {
        for (int i = 0; i < buildButtons.Count; i++)
        {
            buildButtons[i].Visible = true;
            var pos = buttonPositions[i];
            buildButtons[i].Disabled = pos.X >= currentGridWidth || pos.Y >= currentGridHeight;
        }
    }

    // ================================================================
    //  物品栏刷新 —————— 从 DataManager 读取背包，生成卡牌按钮
    // ================================================================

    private void RefreshInventory()
    {
        foreach (Node child in inventorybox.GetChildren())
            child.QueueFree();

        var cardTitle = new Label { Text = "—— 14张卡牌仓库 ——" };
        inventorybox.AddChild(cardTitle);

        foreach (var card in DataManager.Instance.GetOwnedCards())
        {
            var cardObj = (GodotObject)card;
            StringName cardId = cardObj.Get(GDScriptKeys.CardData.Id).AsStringName();
            int ownedCount = DataManager.Instance.GetCardCount(cardId);
            int placedCount = CountPlacedOnBoard(cardId);
            int remaining = ownedCount - placedCount;

            var button = new Button();
            button.Disabled = remaining <= 0;
            int shapeCount = cardObj.Get(GDScriptKeys.CardData.ShapeOffsets).As<Array<Vector2I>>().Count;
            button.Text = $"{cardObj.Get(GDScriptKeys.CardData.IconText)} {cardObj.Get(GDScriptKeys.CardData.DisplayName)}  x{remaining}｜占用{shapeCount}格\n{cardObj.Get(GDScriptKeys.CardData.Description)}";
            button.CustomMinimumSize = new Vector2(0, 74);
            button.Pressed += () => OnInventoryCardPressed(cardId);
            inventorybox.AddChild(button);
        }
    }

    private int CountPlacedOnBoard(StringName cardId)
    {
        int count = 0;
        foreach (var runtime in boardManager.runtime_cards)
        {
            var data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
            if (data.Get(GDScriptKeys.CardData.Id).AsStringName() == cardId) count++;
        }
        return count;
    }

    // ================================================================
    //  板子刷新 —————— 更新格子按钮文字（含预览）
    // ================================================================

    private void RefreshBoard()
    {
        var previewPositions = new List<Vector2I>();
        bool previewValid = false;

        if (selectedCardId != "" && hoveredBuildCell.X >= 0)
        {
            var card = DataManager.Instance.GetCard(selectedCardId);
            previewPositions = boardManager.GetPreviewCells(card, hoveredBuildCell, selectedRotation);
            previewValid = boardManager.CanPlace(card, hoveredBuildCell, selectedRotation);
        }

        for (int i = 0; i < MAX_GRID_BUTTONS; i++)
        {
            var pos = buttonPositions[i];
            var btn = buildButtons[i];

            if (pos.X >= currentGridWidth || pos.Y >= currentGridHeight)
            {
                btn.Text = "";
                continue;
            }

            var runtime = boardManager.GetCardByCell(pos);
            btn.Modulate = Colors.White;
            btn.Text = "";

            if (runtime != null)
            {
                var data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
                btn.Text = $"{data.Get(GDScriptKeys.CardData.IconText)}\n{data.Get(GDScriptKeys.CardData.DisplayName)}";
            }

            if (previewPositions.Contains(pos))
            {
                var card = DataManager.Instance.GetCard(selectedCardId);
                var cardObj = (GodotObject)card;
                btn.Text = $"{cardObj.Get(GDScriptKeys.CardData.IconText)}\n{(previewValid ? "可放置" : "冲突")}";
                btn.Modulate = previewValid ? Colors.White : new Color(0.85f, 0.45f, 0.45f);
            }
        }
    }

    // ================================================================
    //  事件回调
    // ================================================================

    private void OnInventoryCardPressed(StringName cardId)
    {
        selectedCardId = cardId;
        selectedRotation = 0;
        RefreshBoard();
    }

    private void OnBuildCellPressed(int index)
    {
        var pos = buttonPositions[index];
        if (pos.X >= currentGridWidth || pos.Y >= currentGridHeight) return;

        var runtime = boardManager.GetCardByCell(pos);
        if (runtime != null)
        {
            EmitSignal(SignalName.RemoveCardRequested, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32());
            RefreshAll();
            return;
        }

        if (selectedCardId == "") return;

        EmitSignal(SignalName.PlaceCardRequested, selectedCardId, pos, selectedRotation);
        if (boardManager.GetCardByCell(pos) != null)
            selectedCardId = "";
        RefreshAll();
    }

    private void OnBuildCellHovered(int index)
    {
        hoveredBuildCell = buttonPositions[index];
        RefreshBoard();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent
            && keyEvent.Pressed
            && !keyEvent.Echo
            && keyEvent.Keycode == Key.R
            && selectedCardId != "")
        {
            selectedRotation = (selectedRotation + 1) % 4;
            RefreshBoard();
        }
    }

    private void OnRotatePressed()
    {
        if (selectedCardId == "") return;
        selectedRotation = (selectedRotation + 1) % 4;
        RefreshBoard();
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
}
