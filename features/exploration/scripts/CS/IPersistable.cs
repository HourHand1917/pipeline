using Godot.Collections;

/// <summary>
/// 跨地图持久化接口。任何需要跨场景保留状态的物体实现此接口。
/// </summary>
public interface IPersistable
{
    /// <summary>此物体的全局唯一 ID</summary>
    string PersistenceId { get; }

    /// <summary>将当前状态序列化为字典</summary>
    Dictionary SaveState();

    /// <summary>从字典恢复状态</summary>
    void LoadState(Dictionary state);

    /// <summary>由 ExplorationManager 注入 mapId 并恢复持久化状态</summary>
    void InitPersistence(string mapId);
}
