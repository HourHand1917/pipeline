using Godot;
using System.Collections.Generic;

public partial class BuildScreen : Control
{
    [Signal] public delegate void PlaceCardRequestedEventHandler(StringName cardId, Vector2I anchor, int rotationSteps);
    [Signal] public delegate void RemoveCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void StartBattleRequestedEventHandler();
    [Signal] public delegate void ClearBuildRequestedEventHandler();
    [Signal] public delegate void GridSizeChangedEventHandler(int width, int height);

    private BoardManager boardManager;

    private StringName selectedCardId = "";
    private int selectedRotation = 0;
    private Vector2I hoveredBuildCell = new(-1, -1);
    private int currentGridWidth = 3;
    private int currentGridHeight = 2;

    [Export] private GridContainer buildgrid;
    [Export] private VBoxContainer inventorybox;
    [Export] private Button rotatebutton;
    [Export] private Button examplebutton;
    [Export] private Button clearbutton;
    [Export] private Button startbattlebutton;
    [Export] private Button grid3x2button;
    [Export] private Button grid3x3button;
    [Export] private Button grid4x3button;

    private List<Button> buildButtons = new();
    private Vector2I[] buttonPositions = new Vector2I[12];

    public void Setup(BoardManager board)
    {
        boardManager = board;

        foreach (Node child in buildgrid.GetChildren())
        {
            if (child is Button btn)
                buildButtons.Add(btn);
        }

        for (int i = 0; i < 12; i++)
            buttonPositions[i] = new Vector2I(i % 4, i / 4);

        for (int i = 0; i < buildButtons.Count; i++)
        {
            int index = i;
            buildButtons[i].Pressed += () => OnBuildCellPressed(index);
            buildButtons[i].MouseEntered += () => OnBuildCellHovered(index);
            buildButtons[i].MouseExited += () => { hoveredBuildCell = new(-1, -1); RefreshBoard(); };
        }

        rotatebutton.Pressed += OnRotatePressed;
        examplebutton.Pressed += PlaceExampleBuild;
        clearbutton.Pressed += () => EmitSignal(SignalName.ClearBuildRequested);
        startbattlebutton.Pressed += () => EmitSignal(SignalName.StartBattleRequested);
        grid3x2button.Pressed += () => SwitchGrid(3, 2);
        grid3x3button.Pressed += () => SwitchGrid(3, 3);
        grid4x3button.Pressed += () => SwitchGrid(4, 3);

        UpdateGridState();
    }

    public void RefreshAll()
    {
        RefreshInventory();
        RefreshBoard();
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

    private void SwitchGrid(int width, int height)
    {
        if (currentGridWidth == width && currentGridHeight == height) return;
        currentGridWidth = width;
        currentGridHeight = height;
        EmitSignal(SignalName.GridSizeChanged, width, height);
        ClearSelection();
        UpdateGridState();
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
                if ((StringName)data.Get("id") == cardId) { alreadyPlaced = true; break; }
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
                btn.Text = $"{card.Get("icon_text")}\n{(previewValid ? "可放置" : "冲突")}";
                btn.Modulate = previewValid ? Colors.White : new Color(0.85f, 0.45f, 0.45f);
            }
        }
    }

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
            EmitSignal(SignalName.RemoveCardRequested, (int)runtime.Get("instance_id"));
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