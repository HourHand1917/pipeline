using Godot;
using Godot.Collections;

/// <summary>
/// 全局跨地图状态管理（Autoload）。
/// scene_path → object_id → state_dict
/// 场景切换时内存保留，存档时序列化到磁盘。
/// </summary>
[GlobalClass]
public partial class GameState : Node
{
    public static GameState Instance { get; private set; }

    /// <summary>scene_path → (object_id → state_dict)</summary>
    private Dictionary<string, Dictionary<string, Dictionary>> _snapshots = new();

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("GameState 重复实例化"); return; }
        Instance = this;
    }

    /// <summary>获取某个对象的持久化状态，没有则返回 null</summary>
    public Dictionary GetObjectState(string mapId, string objectId)
    {
        if (!_snapshots.TryGetValue(mapId, out var mapData))
            return null;
        return mapData.TryGetValue(objectId, out var state) ? state : null;
    }

    /// <summary>保存某个对象的持久化状态</summary>
    public void SetObjectState(string mapId, string objectId, Dictionary state)
    {
        if (!_snapshots.ContainsKey(mapId))
            _snapshots[mapId] = new Dictionary<string, Dictionary>();
        _snapshots[mapId][objectId] = state;
    }

    /// <summary>清除某个对象的状态（比如敌人复活）</summary>
    public void ClearObjectState(string mapId, string objectId)
    {
        if (_snapshots.TryGetValue(mapId, out var mapData))
            mapData.Remove(objectId);
    }

    /// <summary>整张地图的所有状态</summary>
    public Dictionary<string, Dictionary> GetMapSnapshot(string mapId)
    {
        _snapshots.TryGetValue(mapId, out var data);
        return data;
    }

    // ================================================================
    //  存档（后续扩展）
    // ================================================================

    public Dictionary<string, Dictionary<string, Dictionary>> DumpAll()
    {
        return _snapshots;
    }

    public void LoadAll(Dictionary<string, Dictionary<string, Dictionary>> data)
    {
        _snapshots = data;
    }
}
