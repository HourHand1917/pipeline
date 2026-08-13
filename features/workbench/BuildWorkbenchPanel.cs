using Godot;
using System.Collections.Generic;

/// <summary>
/// 工作台构筑面板。从背包选卡牌放入棋盘，R 键 / 按钮旋转。
/// BoardManager 作为子节点，BoardChanged 信号驱动刷新。
/// 构筑结果通过 DataManager.SaveBuild 持久化。
/// </summary>
[GlobalClass]
public partial class BuildWorkbenchPanel : Control
{
    [Export] private BoardManager _boardManager;
    [Export] private GridContainer _buildGrid;
    [Export] private GridContainer _inventoryBox;
    [Export] private Texture2D _cardBgNormalTex;
    [Export] private Texture2D _cardBgDisabledTex;
    [Export] private Vector2 _cardCellSize = new(120, 88);

    private StringName _selectedCardId = "";
    private int _selectedRotation = 0;
    private Vector2I _hoveredCell = new(-1, -1);

    private const int MAX_BUTTONS = 12;
    private List<GridCellButton> _buildButtons = new();
    private Vector2I[] _buttonPositions = new Vector2I[MAX_BUTTONS];

    public override void _Ready()
    {
        for (int i = 0; i < _buildGrid.GetChildCount() && i < MAX_BUTTONS; i++)
        {
            if (_buildGrid.GetChild(i) is GridCellButton cell)
            {
                _buildButtons.Add(cell);
                int idx = i;
                cell.Pressed += () => OnCellPressed(idx);
                cell.MouseEntered += () => OnCellHovered(idx);
                cell.MouseExited += () => { _hoveredCell = new(-1, -1); RefreshBoard(); };
            }
        }

        for (int i = 0; i < MAX_BUTTONS; i++)
            _buttonPositions[i] = new Vector2I(i % 4, i / 4);

        _boardManager.BoardChanged += RefreshBoard;
        DataManager.Instance.CardCollectionChanged += RefreshAll;
        RefreshBoard();
    }

    public override void _ExitTree()
    {
        DataManager.Instance.CardCollectionChanged -= RefreshAll;
    }

    // ================================================================
    //  公共
    // ================================================================

    public void RefreshAll()
    {
        RefreshInventory();
        RefreshBoard();
    }

    // ================================================================
    //  背包卡牌
    // ================================================================

    private StyleBoxTexture _cardBgNormal;
    private StyleBoxTexture _cardBgHover;
    private StyleBoxTexture _cardBgDisabled;

    private void RefreshInventory()
    {
        if (_cardBgNormal == null && _cardBgNormalTex != null)
        {
            _cardBgNormal = new StyleBoxTexture { Texture = _cardBgNormalTex };
            _cardBgNormal.ContentMarginLeft = 4;
            _cardBgNormal.ContentMarginRight = 4;
            _cardBgNormal.ContentMarginTop = 4;
            _cardBgNormal.ContentMarginBottom = 4;

            _cardBgHover = new StyleBoxTexture { Texture = _cardBgNormalTex, ModulateColor = new Color(1.3f, 1.3f, 1.3f) };
            _cardBgHover.ContentMarginLeft = 4;
            _cardBgHover.ContentMarginRight = 4;
            _cardBgHover.ContentMarginTop = 4;
            _cardBgHover.ContentMarginBottom = 4;
        }
        if (_cardBgDisabled == null && _cardBgDisabledTex != null)
        {
            _cardBgDisabled = new StyleBoxTexture { Texture = _cardBgDisabledTex };
            _cardBgDisabled.ContentMarginLeft = 4;
            _cardBgDisabled.ContentMarginRight = 4;
            _cardBgDisabled.ContentMarginTop = 4;
            _cardBgDisabled.ContentMarginBottom = 4;
        }

        foreach (Node child in _inventoryBox.GetChildren())
            child.QueueFree();

        foreach (var card in DataManager.Instance.GetOwnedCards())
        {
            var obj = (GodotObject)card;
            var cardId = obj.Get(GDScriptKeys.CardData.Id).AsStringName();
            int owned = DataManager.Instance.GetCardCount(cardId);
            int placed = CountPlaced(cardId);
            int remaining = owned - placed;
            int shapeCount = GetShapeCount(obj);

            var btn = new Button();
            btn.Disabled = remaining <= 0;
            btn.Text = $"{obj.Get(GDScriptKeys.CardData.IconText)}\n{obj.Get(GDScriptKeys.CardData.DisplayName)}\n×{remaining}｜{shapeCount}格";
            btn.CustomMinimumSize = _cardCellSize;

            if (remaining > 0 && _cardBgNormal != null)
            {
                btn.AddThemeStyleboxOverride("normal", _cardBgNormal);
                btn.AddThemeStyleboxOverride("hover", _cardBgHover);
            }
            else if (remaining <= 0 && _cardBgDisabled != null)
            {
                btn.AddThemeStyleboxOverride("disabled", _cardBgDisabled);
            }

            btn.Pressed += () => SelectCard(cardId);
            _inventoryBox.AddChild(btn);

            var data = new TooltipData
            {
                Title = obj.Get(GDScriptKeys.CardData.DisplayName).AsString(),
                Description = obj.Get(GDScriptKeys.CardData.Description).AsString(),
            };
            string range = obj.Call("range_text").AsString();
            if (!string.IsNullOrEmpty(range)) data.Details["射程"] = range;
            int cd = obj.Get(GDScriptKeys.CardData.CooldownTurns).AsInt32();
            if (cd > 0) data.Details["冷却"] = $"{cd} 回合";
            TooltipService.Instance.ShowFor(btn, data);
        }
    }

    private int CountPlaced(StringName cardId)
    {
        int count = 0;
        foreach (var rt in _boardManager.runtime_cards)
        {
            var data = rt.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
            if (data.Get(GDScriptKeys.CardData.Id).AsStringName() == cardId) count++;
        }
        return count;
    }

    private static int GetShapeCount(GodotObject cardObj)
    {
        var arr = cardObj.Call("get_rotated_shape", 0).As<GodotObject>();
        return arr != null ? arr.Call("size").AsInt32() : 1;
    }

    private void AttachBoardTooltip(GridCellButton cell, GodotObject cardObj)
    {
        var data = new TooltipData
        {
            Title = cardObj.Get(GDScriptKeys.CardData.DisplayName).AsString(),
            Description = cardObj.Get(GDScriptKeys.CardData.Description).AsString(),
        };
        string range = cardObj.Call("range_text").AsString();
        if (!string.IsNullOrEmpty(range)) data.Details["射程"] = range;
        int cd = cardObj.Get(GDScriptKeys.CardData.CooldownTurns).AsInt32();
        if (cd > 0) data.Details["冷却"] = $"{cd} 回合";

        TooltipService.Instance.ShowFor(cell, data);
    }

    // ================================================================
    //  棋盘
    // ================================================================

    private void RefreshBoard()
    {
        var previewCells = new List<Vector2I>();
        bool previewValid = false;

        if (_selectedCardId != "" && _hoveredCell.X >= 0)
        {
            var card = DataManager.Instance.GetCard(_selectedCardId);
            if (card != null)
            {
                previewCells = _boardManager.GetPreviewCells(card, _hoveredCell, _selectedRotation);
                previewValid = _boardManager.CanPlace(card, _hoveredCell, _selectedRotation);
            }
        }

        int w = _boardManager.columns;
        int h = _boardManager.rows;
        // 不修改 _buildGrid.Columns——保持编辑器里设的 4 列，和 battlescreen 一致。
        // 超出逻辑棋盘范围的格子用 Disabled 状态而非隐藏。

        for (int i = 0; i < _buildButtons.Count && i < MAX_BUTTONS; i++)
        {
            var pos = _buttonPositions[i];
            var cell = _buildButtons[i];

            // 超出棋盘范围 → 禁用而非隐藏（和 battlescreen 一致）
            if (pos.X >= w || pos.Y >= h)
            {
                cell.SetState(CellState.Disabled);
                cell.SetText("");
                continue;
            }

            var rt = _boardManager.GetCardByCell(pos);
            if (rt != null)
            {
                var data = rt.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
                cell.SetText($"{data.Get(GDScriptKeys.CardData.IconText)}\n{data.Get(GDScriptKeys.CardData.DisplayName)}");
                cell.SetState(CellState.Normal);
                AttachBoardTooltip(cell, data);
            }
            else if (previewCells.Contains(pos))
            {
                var cardObj = (GodotObject)DataManager.Instance.GetCard(_selectedCardId);
                cell.SetText($"{cardObj.Get(GDScriptKeys.CardData.IconText)}\n{(previewValid ? "可放置" : "冲突")}");
                cell.SetState(previewValid ? CellState.Charged : CellState.Cooldown);
                TooltipService.Instance.HideFor(cell);
            }
            else
            {
                cell.SetText("");
                cell.SetState(CellState.Normal);
                TooltipService.Instance.HideFor(cell);
            }
        }
    }

    // ================================================================
    //  交互
    // ================================================================

    private void SelectCard(StringName cardId)
    {
        _selectedCardId = cardId;
        _selectedRotation = 0;
        RefreshBoard();
    }

    private void OnCellPressed(int index)
    {
        var pos = _buttonPositions[index];
        int w = _boardManager.columns, h = _boardManager.rows;
        if (pos.X >= w || pos.Y >= h) return;

        // 已有卡 → 移除
        var rt = _boardManager.GetCardByCell(pos);
        if (rt != null)
        {
            _boardManager.RemoveCard(rt.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32());
            Save();
            RefreshAll();
            return;
        }

        if (_selectedCardId == "") return;

        // 放置
        var card = DataManager.Instance.GetCard(_selectedCardId);
        if (card == null) return;
        if (_boardManager.PlaceCard((GodotObject)card, pos, _selectedRotation) != null)
        {
            _selectedCardId = "";
            _selectedRotation = 0;
            Save();
            RefreshAll();
        }
    }

    private void OnCellHovered(int index)
    {
        _hoveredCell = _buttonPositions[index];
        RefreshBoard();
    }

    private void OnRotate()
    {
        if (_selectedCardId == "") return;
        _selectedRotation = (_selectedRotation + 1) % 4;
        RefreshBoard();
    }

    public override void _Input(InputEvent @event)
    {
        // 右键取消选中（必须在 _Input 里，_UnhandledInput 拿不到按钮消费过的事件）
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }
            && _selectedCardId != "")
        {
            _selectedCardId = "";
            _selectedRotation = 0;
            RefreshBoard();
            AcceptEvent();
        }

        // R 键旋转
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R }
            && _selectedCardId != "")
        {
            _selectedRotation = (_selectedRotation + 1) % 4;
            RefreshBoard();
        }
    }

    private void Save()
    {
        DataManager.Instance.SaveBuild(_boardManager.runtime_cards);
    }
}
