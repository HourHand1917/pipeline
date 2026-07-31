using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class BuildScreen : Control
{
    // ============ 信号 ============
    [Signal] public delegate void PlaceCardRequestedEventHandler(StringName cardId, Vector2I anchor, int rotationSteps);
    [Signal] public delegate void RemoveCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void StartBattleRequestedEventHandler();
    [Signal] public delegate void ClearBuildRequestedEventHandler();
    [Signal] public delegate void BoardSizeRequestedEventHandler(Vector2I size);

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

    // ============ 内部数据 ============
    private List<Button> buildButtons = new();
    private Vector2I[] buttonPositions = new Vector2I[12];

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
        for (int i = 0; i < 12; i++)
            buttonPositions[i] = new Vector2I(i % 4, i / 4);

        // 格子按钮信号
        for (int i = 0; i < buildButtons.Count; i++)
        {
            int index = i;
            buildButtons[i].Pressed += () => OnBuildCellPressed(index);
            buildButtons[i].MouseEntered += () => OnBuildCellHovered(index);
            buildButtons[i].MouseExited += () => { hoveredBuildCell = new(-1, -1); RefreshBoard(); };
        }

        // 操作按钮信号
        rotatebutton.Pressed += OnRotatePressed;
        examplebutton.Pressed += PlaceExampleBuild;
        clearbutton.Pressed += () => EmitSignal(SignalName.ClearBuildRequested);
        startbattlebutton.Pressed += () => EmitSignal(SignalName.StartBattleRequested);
        grid3x2button.Pressed += () => RequestBoardSize(3, 2);
        grid3x3button.Pressed += () => RequestBoardSize(3, 3);
        grid4x3button.Pressed += () => RequestBoardSize(4, 3);

        UpdateGridState();
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
        GD.Print($"[BuildScreen] {text}");
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
    //  物品栏刷新
    // ================================================================

    private void RefreshInventory()
    {
        foreach (Node child in inventorybox.GetChildren())
            child.QueueFree();

        foreach (var card in DataManager.Instance.GetOwnedCards())
        {
            StringName cardId = ((GodotObject)card).Get("id").AsStringName();
            int ownedCount = DataManager.Instance.GetCardCount(cardId);
            int placedCount = CountPlacedOnBoard(cardId);
            int remaining = ownedCount - placedCount;

            var button = new Button();
            button.Disabled = remaining <= 0;
            int shapeCount = ((GodotObject)card).Get("shape_offsets").As<Array<Vector2I>>().Count;
            button.Text = $"{((GodotObject)card).Get("icon_text")} {((GodotObject)card).Get("display_name")}  x{remaining}｜占用{shapeCount}格\n{((GodotObject)card).Get("description")}";
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
            var data = runtime.Get("data").As<GodotObject>();
            if (data.Get("id").AsStringName() == cardId) count++;
        }
        return count;
    }

    // ================================================================
    //  板子刷新
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

        for (int i = 0; i < 12; i++)
        {
            var pos = buttonPositions[i];
            var btn = buildButtons[i];

            if (pos.X >= currentGridWidth || pos.Y >= currentGridHeight)
            {
                btn.Text = "";
                continue;
            }

            var cell = boardManager.GetCell(pos);
            var runtime = boardManager.GetCardByCell(pos);
            btn.Modulate = Colors.White;
            btn.Text = "";

            if (runtime != null)
            {
                var data = runtime.Get("data").As<GodotObject>();
                btn.Text = $"{data.Get("icon_text")}\n{data.Get("display_name")}";
            }

            if (previewPositions.Contains(pos))
            {
                var card = DataManager.Instance.GetCard(selectedCardId);
                btn.Text = $"{((GodotObject)card).Get("icon_text")}\n{(previewValid ? "可放置" : "冲突")}";
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
            EmitSignal(SignalName.RemoveCardRequested, runtime.Get("instance_id").AsInt32());
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