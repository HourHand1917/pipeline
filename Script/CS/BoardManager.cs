using Godot;
using System.Collections.Generic;

public partial class BoardManager : Node
{
    [Signal] public delegate void BoardChangedEventHandler();
    [Signal] public delegate void CardPlacedEventHandler(GodotObject runtimeCard);
    [Signal] public delegate void CardRemovedEventHandler(int instanceId);
    [Signal] public delegate void CardReadyChangedEventHandler(int instanceId, bool isReady);
    [Signal] public delegate void CooldownChangedEventHandler(int instanceId, int remaining);

    public const int BoardSize = 3;

    private List<List<GodotObject>> cells = new();
    public List<GodotObject> runtime_cards { get; private set; } = new();
    private int _nextInstanceId = 1;

    public override void _Ready()
    {
        ResetBoard();
    }

    /// <summary>
    /// 重置整个板子
    /// </summary>
    public void ResetBoard()
    {
        cells.Clear();
        runtime_cards.Clear();
        _nextInstanceId = 1;

        for (int row = 0; row < BoardSize; row++)
        {
            var rowCells = new List<GodotObject>();
            for (int column = 0; column < BoardSize; column++)
            {
                var cell = ClassDB.Instantiate("CellRuntime").As<GodotObject>();
                cell.Call("_init", new Vector2I(column, row));
                rowCells.Add(cell);
            }
            cells.Add(rowCells);
        }

        EmitSignal(SignalName.BoardChanged);
    }

    /// <summary>
    /// 获取指定位置的格子，超出边界返回 null
    /// </summary>
    public GodotObject GetCell(Vector2I position)
    {
        if (!IsInside(position))
            return null;
        return cells[position.Y][position.X];
    }

    /// <summary>
    /// 判断坐标是否在板子内
    /// </summary>
    public bool IsInside(Vector2I position)
    {
        return position.X >= 0
            && position.X < BoardSize
            && position.Y >= 0
            && position.Y < BoardSize;
    }

    /// <summary>
    /// 判断卡片能否放置在指定位置
    /// </summary>
    public bool CanPlace(GodotObject cardData, Vector2I anchor, int rotationSteps)
    {
        var shape = cardData.Call("get_rotated_shape", rotationSteps)
            .As<Godot.Collections.Array<Vector2I>>();

        foreach (Vector2I offset in shape)
        {
            var target = anchor + offset;
            if (!IsInside(target))
                return false;

            var cell = GetCell(target);
            if ((int)cell.Get("card_instance_id") != -1)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 获取预览占用的格子坐标列表
    /// </summary>
    public List<Vector2I> GetPreviewCells(GodotObject cardData, Vector2I anchor, int rotationSteps)
    {
        var result = new List<Vector2I>();
        var shape = cardData.Call("get_rotated_shape", rotationSteps)
            .As<Godot.Collections.Array<Vector2I>>();

        foreach (Vector2I offset in shape)
        {
            result.Add(anchor + offset);
        }
        return result;
    }

    /// <summary>
    /// 放置卡片到板子上
    /// </summary>
    public GodotObject PlaceCard(GodotObject cardData, Vector2I anchor, int rotationSteps)
    {
        if (!CanPlace(cardData, anchor, rotationSteps))
            return null;

        var occupied = GetPreviewCells(cardData, anchor, rotationSteps);

        // 创建 CardRuntime 实例
        var runtime = ClassDB.Instantiate("CardRuntime").As<GodotObject>();
        runtime.Set("instance_id", _nextInstanceId);
        runtime.Set("data", cardData);
        runtime.Set("anchor_position", anchor);
        runtime.Set("rotation_steps", rotationSteps);

        var occupiedArray = new Godot.Collections.Array<Vector2I>();
        foreach (var pos in occupied)
            occupiedArray.Add(pos);
        runtime.Set("occupied_cells", occupiedArray);

        runtime.Set("is_ready", false);
        runtime.Set("cooldown_remaining", 0);

        _nextInstanceId++;
        runtime_cards.Add(runtime);

        for (int index = 0; index < occupied.Count; index++)
        {
            var cell = GetCell(occupied[index]);
            cell.Set("card_instance_id", (int)runtime.Get("instance_id"));
            cell.Set("local_shape_index", index);
        }

        EmitSignal(SignalName.CardPlaced, runtime);
        EmitSignal(SignalName.BoardChanged);
        return runtime;
    }

    /// <summary>
    /// 移除指定 ID 的卡片
    /// </summary>
public void RemoveCard(int instanceId)  // 原来是 public bool RemoveCard
{
    var runtime = GetRuntimeCard(instanceId);
    if (runtime == null)
        return;  // 原来是 return false

    var occupiedCells = runtime.Get("occupied_cells")
        .As<Godot.Collections.Array<Vector2I>>();

    foreach (var position in occupiedCells)
    {
        var cell = GetCell(position);
        cell.Set("card_instance_id", -1);
        cell.Set("local_shape_index", -1);
        cell.Set("is_lit", false);
    }

    runtime_cards.Remove(runtime);
    EmitSignal(SignalName.CardRemoved, instanceId);
    EmitSignal(SignalName.BoardChanged);
    // 原来是 return true
}

    /// <summary>
    /// 根据实例 ID 获取卡片运行时数据
    /// </summary>
    public GodotObject GetRuntimeCard(int instanceId)
    {
        foreach (var runtime in runtime_cards)
        {
            if ((int)runtime.Get("instance_id") == instanceId)
                return runtime;
        }
        return null;
    }

    /// <summary>
    /// 根据板子坐标获取该格子的卡片
    /// </summary>
    public GodotObject GetCardByCell(Vector2I position)
    {
        var cell = GetCell(position);
        if (cell == null || (int)cell.Get("card_instance_id") == -1)
            return null;
        return GetRuntimeCard((int)cell.Get("card_instance_id"));
    }

    /// <summary>
    /// 设置格子的点亮状态
    /// </summary>
    public GodotObject SetCellLit(Vector2I position, bool value)
    {
        var cell = GetCell(position);
        if (cell == null)
            return null;

        cell.Set("is_lit", value);
        var runtime = GetCardByCell(position);

        if (runtime != null)
        {
            bool oldReady = (bool)runtime.Get("is_ready");
            bool newReady = CheckCardReady(runtime);
            runtime.Set("is_ready", newReady);

            if (oldReady != newReady)
                EmitSignal(SignalName.CardReadyChanged, (int)runtime.Get("instance_id"), newReady);
        }

        EmitSignal(SignalName.BoardChanged);
        return runtime;
    }

    /// <summary>
    /// 检查卡片是否满足发动条件
    /// </summary>
    public bool CheckCardReady(GodotObject runtime)
    {
        if ((int)runtime.Get("cooldown_remaining") > 0)
            return false;

        var occupiedCells = runtime.Get("occupied_cells")
            .As<Godot.Collections.Array<Vector2I>>();

        foreach (var position in occupiedCells)
        {
            var cell = GetCell(position);
            if (cell == null || !(bool)cell.Get("is_lit"))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 清除某张卡片的所有点亮格子
    /// </summary>
    public void ClearCardLights(GodotObject runtime)
    {
        var occupiedCells = runtime.Get("occupied_cells")
            .As<Godot.Collections.Array<Vector2I>>();

        foreach (var position in occupiedCells)
        {
            var cell = GetCell(position);
            if (cell != null)
                cell.Set("is_lit", false);
        }

        if ((bool)runtime.Get("is_ready"))
        {
            runtime.Set("is_ready", false);
            EmitSignal(SignalName.CardReadyChanged, (int)runtime.Get("instance_id"), false);
        }

        EmitSignal(SignalName.BoardChanged);
    }

    /// <summary>
    /// 清除所有格子的点亮状态
    /// </summary>
    public void ClearAllLights()
    {
        foreach (var row in cells)
        {
            foreach (var cell in row)
            {
                cell.Set("is_lit", false);
            }
        }

        foreach (var runtime in runtime_cards)
        {
            if ((bool)runtime.Get("is_ready"))
            {
                runtime.Set("is_ready", false);
                EmitSignal(SignalName.CardReadyChanged, (int)runtime.Get("instance_id"), false);
            }
        }

        EmitSignal(SignalName.BoardChanged);
    }

    /// <summary>
    /// 所有卡片冷却 -1
    /// </summary>
    public void TickCooldowns()
    {
        foreach (var runtime in runtime_cards)
        {
            int currentCooldown = (int)runtime.Get("cooldown_remaining");
            int newCooldown = Mathf.Max(0, currentCooldown - 1);
            runtime.Set("cooldown_remaining", newCooldown);

            bool newReady = CheckCardReady(runtime);
            runtime.Set("is_ready", newReady);

            EmitSignal(SignalName.CooldownChanged, (int)runtime.Get("instance_id"), newCooldown);
        }

        EmitSignal(SignalName.BoardChanged);
    }

    /// <summary>
    /// 从保存数据重建板子
    /// </summary>
    public void BuildFromSavedData(Godot.Collections.Array<Godot.Collections.Dictionary> savedData)
    {
        ResetBoard();

        foreach (var entry in savedData)
        {
            StringName cardId = entry["card_id"].AsStringName();
            var card = DataManager.Instance.GetCard(cardId);
            if (card == null)
                continue;

            PlaceCard(
                card,
                (Vector2I)entry["anchor"],
                (int)entry["rotation"]
            );
        }
    }
}