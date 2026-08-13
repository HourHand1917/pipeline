# 地图系统规划

> 最后更新：2026-08-11
> 关联：[persistence-system.md](persistence-system.md)、[tv-ui-and-tooltip-plan.md](tv-ui-and-tooltip-plan.md)

## 一、现状梳理

已有两层「位置」概念，先厘清：

| 层级 | 是什么 | 现有实现 |
|---|---|---|
| **Room（房间）** | 同一场景内的子区域 | `RoomTransitionManager` 用 Tween 淡入淡出切换 `Room_Front`/`Room_Back` |
| **Map（地图）** | 完整的 `.tscn` 场景 | `home.tscn`、`exploration_map.tscn`，各自独立 `ExplorationManager` 根节点 |

**本次做 Map 层**——跨场景穿梭，不动 Room 层。

关键前提（已确认）：
- **战斗是独立场景**，不在探索场景内触发
- **只有血量（HP）在探索/战斗间继承**，其余战斗状态不跨场景

## 二、目标

1. 能在多个地图场景间穿梭（home → 森林 → home…）
2. 门正确连接：从 A 地图「东门」出去，进入 B 地图时出现在 B 的「西门」
3. `PlayerTV` 的 MapPanel 显示地图网络，高亮当前地图

## 三、架构（Layer Cake）

```
PRESENTATION     MapPanel（地图网格，高亮当前块）
						↑ 监听 MapChanged
LOGIC            MapGateInteractable（门）
						↑ 调 TravelTo
INFRASTRUCTURE   MapManager（CanvasLayer Autoload）— 当前地图、场景切换、淡入淡出
						↓ 读
DATA             MapRegistry（Resource）— 全部地图的元数据列表
				 GameState（已有 Autoload）— 跨场景持久化当前地图+生成点
				 DataManager（已有 Autoload）— 跨场景持久化血量
```

### MapManager（CanvasLayer Autoload，新）

继承 `CanvasLayer` 而非 `Node`，内嵌全屏 `ColorRect` 做淡入淡出。这是 scene-management 要求的「Fade wrapping a safe change」。

```csharp
[GlobalClass]
public partial class MapManager : CanvasLayer
{
	[Signal] public delegate void MapChangedEventHandler(StringName mapId);

	public StringName CurrentMapId { get; private set; }
	public StringName CurrentSpawnId { get; private set; }

	[Export] private ColorRect _fadeRect;
	[Export] private float _fadeDuration = 0.5f;

	// 淡入 → 切场景 → 淡出 → 定位玩家
	public async void TravelTo(StringName targetMapId, StringName spawnId);

	// 新场景 ExplorationManager 调用，返回待生成点
	public Vector2 ConsumePendingSpawn(out StringName spawnId);
}
```

流程：
1. `TravelTo` 存 `targetMapId + spawnId` 到 GameState
2. 淡入（ColorRect alpha 0→1）
3. `get_tree().change_scene_to_file(targetScenePath)`
4. 新场景 `ExplorationManager._Ready()` 读 GameState 定位玩家到生成点
5. 淡出（1→0）

### MapRegistry（Resource，新）

全部地图的**显示元数据**列表。MapManager 和 MapPanel 都从这里读，单一数据源。

```gdscript
class_name MapRegistry
extends Resource

@export var maps: Array[MapData] = []
```

### MapData（Resource，新）

单个地图的元数据。**只存显示信息 + 场景路径，不存门连接**（门连接在门节点上，避免数据重复）。

```gdscript
class_name MapData
extends Resource

@export var id: StringName                    # 唯一 id，如 "home"、"forest"
@export var display_name: String              # 显示名
@export var scene_path: String                # res://features/exploration/scenes/home.tscn
@export var map_icon: Texture2D               # MapPanel 里显示的地图块贴图
@export var grid_position: Vector2i           # 在 MapPanel 网格里的位置
```

### SpawnPoint（新节点，挂在每个地图场景里）

`@export var spawn_id: StringName`。命名约定：`home_east`、`forest_west`。玩家进入地图时定位到对应生成点。

### MapGateInteractable（新，与 RoomGate 分工）

```csharp
[GlobalClass]
public partial class MapGateInteractable : InteractableBase
{
	[Export] public StringName TargetMapId;     // 通向的地图
	[Export] public StringName TargetSpawnId;   // 目标地图的生成点

	public override void HandleInteract()
	{
		MapManager.Instance.TravelTo(TargetMapId, TargetSpawnId);
	}
}
```

> `RoomGateInteractable` 保留，管同场景房间切换。RoomGate 管房间，MapGate 管跨场景。

## 四、MapPanel（TV UI）

- 布局：一块块地图砖，按 `MapData.grid_position` 排列
- 数据源：`MapRegistry` + `MapManager.CurrentMapId`
- 高亮：当前地图砖用「已激活」贴图或 modulate 提亮，其余普通贴图
- 交互（探索模式）：点击相邻地图砖 → `TravelTo`；战斗模式只读
- 监听 `MapManager.MapChanged` 自动刷新高亮

```csharp
// MapPanel 新增
[Export] private MapRegistry _mapRegistry;
public void RefreshMap(StringName currentMapId);
```

## 五、持久化

- **当前地图 + 生成点**：存 `GameState`（MapManager 启动恢复）
- **血量**：存 `DataManager`（新增 `PlayerHp` / `MaxHp` 字段），探索/战斗场景都读写
- **地图内对象状态**（宝箱、敌人）：已有 `IPersistable`，MapId 区分

血量继承说明：战斗结束 → 把最终 HP 写回 DataManager → 探索场景读。玩家死亡/回血等逻辑后续再接。

## 六、实施步骤

1. **MapData + MapRegistry**（GDScript Resource）—— 地图元数据 + 注册表
2. **DataManager 加血量字段** —— `PlayerHp` / `MaxHp`
3. **MapManager（CanvasLayer Autoload）** —— 注册 project.godot，实现淡入淡出 + 场景切换
4. **SpawnPoint 节点** —— 挂到各地图场景
5. **MapGateInteractable** —— 跨场景门组件
6. **ExplorationManager 集成** —— 场景加载定位玩家
7. **MapPanel 改造** —— 显示地图网络 + 高亮
8. **持久化** —— 当前位置存 GameState

## 七、NEVER 清单

- **NEVER** 用 `change_scene_to_file` 丢跨场景状态 → 状态放 MapManager/GameState/DataManager
- **NEVER** 硬编码 `get_node("../../Map/...")` → `%UniqueName` 或 `@export`
- **NEVER** 同步 `load()` 大地图场景 → `ResourceLoader.load_threaded_request`
- **NEVER** 把 Room 层和 Map 层混一起 → RoomGate 管房间，MapGate 管地图
- **NEVER** 门连接存两份 → 连接只在门节点，MapData 只做显示
- **NEVER** MapManager 用 `Node` → 淡入淡出需要 `CanvasLayer` + ColorRect
