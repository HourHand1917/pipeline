using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class BoardManager : Node
{
    [Signal] public delegate void BoardChangedEventHandler();
    [Signal] public delegate void CardPlacedEventHandler(GodotObject runtimeCard);
    [Signal] public delegate void CardRemovedEventHandler(int instanceId);
    [Signal] public delegate void CardReadyChangedEventHandler(int instanceId, bool isReady);
    [Signal] public delegate void CooldownChangedEventHandler(int instanceId, int remaining);

    public int columns { get; private set; } = 3;
    public int rows { get; private set; } = 2;
    public int TotalCells => columns * rows;

    private List<List<GodotObject>> cells = new();
    public Array<GodotObject> runtime_cards { get; private set; } = new();
    private int _nextInstanceId = 1;

    public override void _Ready()
    {
        ConfigureBoard(new Vector2I(columns, rows), false);
    }

    // ================================================================
    //  板子尺寸管理
    // ================================================================

    /// <summary>
    /// 重新配置板子尺寸。preserveCards=true 时保留仍在边界内的卡牌。
    /// 返回被移除的卡牌数量。
    /// </summary>
    public int ConfigureBoard(Vector2I size, bool preserveCards = true)
    {
        if (size.X <= 0 || size.Y <= 0)
        {
            GD.PushError($"棋盘尺寸必须大于零：{size}");
            return 0;
        }

        var oldCells = new List<List<GodotObject>>(cells);
        int oldColumns = columns;
        int oldRows = rows;
        var keptCards = new Array<GodotObject>();
        var removedCards = new Array<GodotObject>();

        if (preserveCards)
        {
            foreach (var runtime in runtime_cards)
            {
                bool fits = true;
                var occupiedCells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
                foreach (var pos in occupiedCells)
                {
                    if (!IsInsideSize(pos, size.X, size.Y))
                    {
                        fits = false;
                        break;
                    }
                }
                if (fits)
                    keptCards.Add(runtime);
                else
                    removedCards.Add(runtime);
            }
        }
        else
        {
            removedCards = runtime_cards.Duplicate();
        }

        columns = size.X;
        rows = size.Y;
        cells = MakeCells(columns, rows);
        runtime_cards = keptCards;

        // 恢复保留卡牌的格子状态
        foreach (var runtime in runtime_cards)
        {
            var occupiedCells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
            for (int index = 0; index < occupiedCells.Count; index++)
            {
                var pos = occupiedCells[index];
                var cell = GetCell(pos);
                cell.Set(GDScriptKeys.CellRuntime.CardInstanceId, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32());
                cell.Set(GDScriptKeys.CellRuntime.LocalShapeIndex, index);

                // 尝试恢复点亮状态
                if (pos.X < oldColumns && pos.Y < oldRows
                    && pos.Y < oldCells.Count && pos.X < oldCells[pos.Y].Count)
                {
                    var oldCell = oldCells[pos.Y][pos.X];
                    if ((int)oldCell.Get("card_instance_id") == (int)runtime.Get(GDScriptKeys.CardRuntime.InstanceId))
                    {
                        cell.Set(GDScriptKeys.CellRuntime.IsLit, oldCell.Get("is_lit").AsBool());
                    }
                }
            }
            UpdateCardReady(runtime);
        }

        // 发出移除信号
        foreach (var runtime in removedCards)
        {
            if ((bool)runtime.Get(GDScriptKeys.CardRuntime.IsReady))
            {
                runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
                EmitSignal(SignalName.CardReadyChanged, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32(), false);
            }
            EmitSignal(SignalName.CardRemoved, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32());
        }

        if (!preserveCards)
            _nextInstanceId = 1;

        EmitSignal(SignalName.BoardChanged);
        return removedCards.Count;
    }

    public void ResetBoard()
    {
        ConfigureBoard(new Vector2I(columns, rows), false);
    }

    // ================================================================
    //  格子访问
    // ================================================================

    public GodotObject GetCell(Vector2I position)
    {
        if (!IsInside(position))
            return null;
        return cells[position.Y][position.X];
    }

    public bool IsInside(Vector2I position)
    {
        return IsInsideSize(position, columns, rows);
    }

    private bool IsInsideSize(Vector2I position, int width, int height)
    {
        return position.X >= 0
            && position.X < width
            && position.Y >= 0
            && position.Y < height;
    }

    // ================================================================
    //  放置逻辑
    // ================================================================

    public bool CanPlace(GodotObject cardData, Vector2I anchor, int rotationSteps, int ignoreInstanceId = -1)
    {
        if (cardData == null) return false;

        var shape = cardData.Call("get_rotated_shape", rotationSteps).As<Array<Vector2I>>();
        foreach (Vector2I offset in shape)
        {
            var target = anchor + offset;
            if (!IsInside(target)) return false;

            var cell = GetCell(target);
            int cellCardId = cell.Get(GDScriptKeys.CellRuntime.CardInstanceId).AsInt32();
            if (cellCardId != -1 && cellCardId != ignoreInstanceId) return false;
        }
        return true;
    }

    public List<Vector2I> GetPreviewCells(GodotObject cardData, Vector2I anchor, int rotationSteps)
    {
        var result = new List<Vector2I>();
        if (cardData == null) return result;

        var shape = cardData.Call("get_rotated_shape", rotationSteps).As<Array<Vector2I>>();
        foreach (Vector2I offset in shape)
            result.Add(anchor + offset);
        return result;
    }

    public GodotObject PlaceCard(GodotObject cardData, Vector2I anchor, int rotationSteps)
    {
        if (!CanPlace(cardData, anchor, rotationSteps)) return null;

        int normalizedRotation = Mathf.PosMod(rotationSteps, 4);
        var occupied = GetPreviewCells(cardData, anchor, normalizedRotation);

        var occupiedArray = new Array<Vector2I>();
        foreach (var pos in occupied) occupiedArray.Add(pos);

        var runtime = GD.Load<GDScript>("res://Script/GD/refcounted/card_runtime.gd")
            .New(_nextInstanceId, cardData, anchor, normalizedRotation, occupiedArray)
            .As<GodotObject>();

        _nextInstanceId++;
        runtime_cards.Add(runtime);

        for (int index = 0; index < occupied.Count; index++)
        {
            var cell = GetCell(occupied[index]);
            cell.Set(GDScriptKeys.CellRuntime.CardInstanceId, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32());
            cell.Set(GDScriptKeys.CellRuntime.LocalShapeIndex, index);
        }

        EmitSignal(SignalName.CardPlaced, runtime);
        EmitSignal(SignalName.BoardChanged);
        return runtime;
    }

    public bool RemoveCard(int instanceId)
    {
        var runtime = GetRuntimeCard(instanceId);
        if (runtime == null) return false;

        var occupiedCells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
        foreach (var pos in occupiedCells)
        {
            var cell = GetCell(pos);
            if (cell != null && cell.Get(GDScriptKeys.CellRuntime.CardInstanceId).AsInt32() == instanceId)
            {
                cell.Set(GDScriptKeys.CellRuntime.CardInstanceId, -1);
                cell.Set(GDScriptKeys.CellRuntime.LocalShapeIndex, -1);
                cell.Set(GDScriptKeys.CellRuntime.IsLit, false);
            }
        }

        if ((bool)runtime.Get(GDScriptKeys.CardRuntime.IsReady))
        {
            runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
            EmitSignal(SignalName.CardReadyChanged, instanceId, false);
        }

        runtime_cards.Remove(runtime);
        EmitSignal(SignalName.CardRemoved, instanceId);
        EmitSignal(SignalName.BoardChanged);
        return true;
    }

    // ================================================================
    //  查询
    // ================================================================

    public GodotObject GetRuntimeCard(int instanceId)
    {
        foreach (var runtime in runtime_cards)
            if (runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32() == instanceId)
                return runtime;
        return null;
    }

    public GodotObject GetCardByCell(Vector2I position)
    {
        var cell = GetCell(position);
        if (cell == null || cell.Get(GDScriptKeys.CellRuntime.CardInstanceId).AsInt32() == -1) return null;
        return GetRuntimeCard(cell.Get(GDScriptKeys.CellRuntime.CardInstanceId).AsInt32());
    }

    // ================================================================
    //  点亮 / 就绪
    // ================================================================

    public GodotObject SetCellLit(Vector2I position, bool value)
    {
        var cell = GetCell(position);
        if (cell == null) return null;

        var runtime = GetCardByCell(position);
        if ((bool)cell.Get(GDScriptKeys.CellRuntime.IsLit) == value) return runtime;

        cell.Set(GDScriptKeys.CellRuntime.IsLit, value);
        if (runtime != null)
            UpdateCardReady(runtime);

        EmitSignal(SignalName.BoardChanged);
        return runtime;
    }

    public bool CheckCardReady(GodotObject runtime)
    {
        if (runtime == null) return false;

        var occupiedCells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
        if (occupiedCells.Count == 0) return false;

        // 保留冷却检查
        if (runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32() > 0) return false;

        foreach (var pos in occupiedCells)
        {
            var cell = GetCell(pos);
            if (cell == null || !(bool)cell.Get(GDScriptKeys.CellRuntime.IsLit)) return false;
        }
        return true;
    }

    private void UpdateCardReady(GodotObject runtime)
    {
        bool isNowReady = CheckCardReady(runtime);
        if (isNowReady == (bool)runtime.Get(GDScriptKeys.CardRuntime.IsReady)) return;
        runtime.Set(GDScriptKeys.CardRuntime.IsReady, isNowReady);
        EmitSignal(SignalName.CardReadyChanged, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32(), isNowReady);
    }

    // ================================================================
    //  点亮清除
    // ================================================================

    public void ClearCardLights(GodotObject runtime)
    {
        if (runtime == null) return;

        bool changed = false;
        var occupiedCells = runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>();
        foreach (var pos in occupiedCells)
        {
            var cell = GetCell(pos);
            if (cell != null && (bool)cell.Get(GDScriptKeys.CellRuntime.IsLit))
            {
                cell.Set(GDScriptKeys.CellRuntime.IsLit, false);
                changed = true;
            }
        }

        bool wasReady = (bool)runtime.Get(GDScriptKeys.CardRuntime.IsReady);
        runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
        if (wasReady)
        {
            EmitSignal(SignalName.CardReadyChanged, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32(), false);
            changed = true;
        }

        if (changed) EmitSignal(SignalName.BoardChanged);
    }

    public void ClearAllLights()
    {
        bool changed = false;
        foreach (var row in cells)
        {
            foreach (var cell in row)
            {
                if ((bool)cell.Get(GDScriptKeys.CellRuntime.IsLit))
                {
                    cell.Set(GDScriptKeys.CellRuntime.IsLit, false);
                    changed = true;
                }
            }
        }

        foreach (var runtime in runtime_cards)
        {
            if ((bool)runtime.Get(GDScriptKeys.CardRuntime.IsReady))
            {
                runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
                EmitSignal(SignalName.CardReadyChanged, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32(), false);
                changed = true;
            }
        }

        if (changed) EmitSignal(SignalName.BoardChanged);
    }

    // ================================================================
    //  状态重置（战斗开始/返回构筑时调用）
    // ================================================================

    /// <summary>
    /// 重置所有卡牌状态：冷却清零、就绪取消、点亮清除。
    /// 战斗开始时由 BattleManager 调用。
    /// </summary>
    public void ResetAllCardStates()
    {
        foreach (var runtime in runtime_cards)
        {
            runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, 0);
            runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
        }
        ClearAllLights();
    }

    // ================================================================
    //  冷却
    // ================================================================

    public void TickCooldowns()
    {
        foreach (var runtime in runtime_cards)
        {
            int currentCooldown = runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32();
            int newCooldown = Mathf.Max(0, currentCooldown - 1);
            runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, newCooldown);

            bool newReady = CheckCardReady(runtime);
            runtime.Set(GDScriptKeys.CardRuntime.IsReady, newReady);

            EmitSignal(SignalName.CooldownChanged, runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32(), newCooldown);
        }
        EmitSignal(SignalName.BoardChanged);
    }

public void TickCellBuffs()
{
    foreach (var row in cells)
    {
        foreach (var cell in row)
        {
            var stats = cell.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>();
            if (stats != null)
            {
                // 调试：检查格子 (1,1) 的蒙尘状态
                var pos = cell.Get(GDScriptKeys.CellRuntime.Position).AsVector2I();
                if (pos.X == 1 && pos.Y == 1)
                {
                    bool hasDust = stats.Call(GDScriptKeys.Stats.HasBuff, "dust").AsBool();
                    int dustStacks = stats.Call(GDScriptKeys.Stats.GetBuffStacks, "dust").AsInt32();
                    GD.Print($"[调试] 格子(1,1) 蒙尘：has={hasDust}, stacks={dustStacks}");
                }
                stats.Call(GDScriptKeys.Stats.TickTurnStart);
            }
        }
    }
}

    // ================================================================
    //  存档
    // ================================================================

    public void BuildFromSavedData(Array<Dictionary> savedData)
    {
        ResetBoard();

        foreach (var entry in savedData)
        {
            StringName cardId = entry["card_id"].AsStringName();
            var card = DataManager.Instance.GetCard(cardId);
            if (card == null) continue;

            PlaceCard(
                (GodotObject)card,
                entry["anchor"].AsVector2I(),
                entry["rotation"].AsInt32()
            );
        }
    }

    // ================================================================
    //  内部工具
    // ================================================================

    private List<List<GodotObject>> MakeCells(int width, int height)
    {
        var result = new List<List<GodotObject>>();
        for (int row = 0; row < height; row++)
        {
            var rowCells = new List<GodotObject>();
            for (int column = 0; column < width; column++)
            {
                var cell = GD.Load<GDScript>("res://Script/GD/refcounted/cell_runtime.gd")
                    .New(new Vector2I(column, row)).As<GodotObject>();
                rowCells.Add(cell);
            }
            result.Add(rowCells);
        }
        return result;
    }
}