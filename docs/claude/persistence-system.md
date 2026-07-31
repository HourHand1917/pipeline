# 跨地图持久化系统

## 概述

场景切换时保留游戏状态（宝箱开闭、敌人击杀、对话进度等），回到旧地图时自动恢复。

## 架构

```
GameState (Autoload)
  _snapshots: {
    "forest_01": {
      "chest_01":    {"opened": true, "items_remaining": 2},
      "enemy_boss":  {"killed": true},
      "npc_merchant":{"dialog_stage": 3}
    },
    "desert_02": { ... }
  }
       ↑ SetObjectState / GetObjectState
       │
ExplorationManager._Ready()
  → 遍历子节点，找所有 IPersistable
  → 调用 p.InitPersistence(MapId)
  → 有存档 → LoadState() 恢复
  → 无存档 → 保持默认

互动时:
  HandleInteract()
    → 修改自身状态
    → PersistInteraction(_mapId)
      → SaveState() → GameState.SetObjectState()
```

## 核心接口

### IPersistable

```csharp
public interface IPersistable
{
    string PersistenceId { get; }            // 全局唯一 ID（同一地图内）
    Dictionary SaveState();                  // 序列化当前状态
    void LoadState(Dictionary state);        // 从字典恢复状态
    void InitPersistence(string mapId);      // 由 ExplorationManager 注入调用
}
```

任何需要持久化的物体实现此接口即可。`InteractableBase` 已内置实现，普通交互物只需在 Inspector 填 `PersistenceId`。

## 数据流

### 进入地图

```
ExplorationManager._Ready()
  → MapId = "forest_01"
  → 递归遍历自身子节点
  → 找到 Chest(PersistenceId="chest_01")
  → chest.InitPersistence("forest_01")
    → GameState.GetObjectState("forest_01", "chest_01")
    → 返回 {"opened": true, "items_remaining": 2}
    → chest.LoadState(...) → 设为已开启，剩余 2 件
```

### 互动改变状态

```
玩家点击宝箱 → HandleInteract()
  → IsOpened = true, ItemsRemaining--
  → PersistInteraction("forest_01")
    → SaveState() → {"opened": true, "items_remaining": 1}
    → GameState.SetObjectState("forest_01", "chest_01", ...)
```

### 离开再回来

```
玩家去 desert_02 → forest_01 场景 unload
GameState 中 "forest_01" 数据仍在内存中

玩家返回 forest_01 → 场景重新 load
ExplorationManager._Ready()
  → chest.InitPersistence("forest_01")
  → GameState 中找到 {"opened": true, "items_remaining": 1}
  → LoadState() 恢复为已开启、剩余 1 件
```

## 扩展示例：自定义状态字段

子类 override `SaveState()` / `LoadState()` 即可：

```csharp
// 宝箱：除了"是否开过"，还要存"剩多少物品"
public override Dictionary SaveState()
{
    return new Dictionary
    {
        { "opened", IsOpened },
        { "items_remaining", ItemsRemaining }
    };
}

public override void LoadState(Dictionary state)
{
    IsOpened = state["opened"].AsBool();
    ItemsRemaining = state["items_remaining"].AsInt32();
}

// 敌人：被杀就消失
public override Dictionary SaveState()
{
    return new Dictionary { { "killed", IsDead } };
}

public override void LoadState(Dictionary state)
{
    if (state["killed"].AsBool()) QueueFree();
}

// NPC：对话阶段
public override Dictionary SaveState()
{
    return new Dictionary { { "dialog_stage", CurrentStage } };
}
```

Base 类（InteractableBase）默认存 `{"interacted": true}`，子类完全替换为自己的字段。

## 新建可持久化物体的步骤

1. 实现 `IPersistable` 或继承 `InteractableBase`
2. 在 Inspector 设 `PersistenceId`（如 `"chest_forest_02"`）
3. 如需要自定义字段，override `SaveState()` / `LoadState()`
4. 状态变更时调用 `PersistInteraction(_mapId)`（InteractableBase 在 `HandleInteract()` 中自动调用）

## 设计原则

遵循 godot-master 持久化规则：

- **不保存 Node 引用**：只存 `Dictionary<string, Variant>` 原始数据
- **不爬树找 MapId**：由 ExplorationManager 在 `_Ready()` 中自动注入
- **不用 CallDeferred 等初始化顺序**：注入即恢复，时序明确
- **MapId + PersistenceId 双键定位**：`GameState.GetObjectState(mapId, objectId)`
- **内存优先，磁盘后加**：当前只存内存 Autoload，后续在 GameState 加 `SaveToFile()` / `LoadFromFile()` 即可持久化到 `user://`
