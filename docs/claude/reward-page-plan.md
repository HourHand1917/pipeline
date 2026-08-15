# 战利品收集页（RewardPage）实现规划

> 写给 Claude Code 自己的执行文档。
> 生成日期：2026-08-16
> 关联：[tv-ui-and-tooltip-plan.md](tv-ui-and-tooltip-plan.md)、[persistence-system.md](persistence-system.md)

---

## 一、目标与范围

做一个通用的「收集战利品」页面。战斗结束获得战利品、探索中开宝箱等场景触发奖励时，`RewardPage` 在 **TV 右上角小窗**弹出（AnimationPlayer 驱动），展示战利品清单（瓶盖、水龙头、卡牌、道具），点击即可收入背包。

- 全部收集完 → 页面**自动合上**。
- 有**关闭按钮**可主动关闭。
- 未收集完的战利品 → **保留在源上**（宝箱 / 敌人尸体）。
- **可扩展**：未来任务奖励、商店、事件、成就等新来源只需实现一个接口，不改 RewardPage。

---

## 二、现状梳理

**已有基础设施**（`DataManager` Autoload，唯一数据入口）：

| 资源 | API | 信号 |
|---|---|---|
| 卡牌 | `AcquireCard(Resource, count)` | `CardAcquired` / `CardCollectionChanged` |
| 道具（上限 4） | `AddItem(Resource)`（满则 false）、`DiscardItem(idx)`、`GetItem(idx)` | `ItemBagChanged` |
| 货币 | `ModifyCurrency(CurrencyType, amount)`；`BottleCap`（瓶盖）/ `Faucet`（水龙头） | `CurrencyChanged` |

**关键场景层级**（决定引用方式）：

```
exploration_map.tscn
├── MapLayer（宝箱/敌人等交互物）        ← 交互物在这里
└── ExplorationHUD (instance)
    └── PlayerTV (instance)
        └── RewardPage                   ← 藏在两层实例内部，无法直接拖到交互物导出槽
```

所以交互物**不能**直接 `[Export] RewardPage`，只能引用 `ExplorationHUD`（实例根，可直接拖），再由 HUD 路由到 RewardPage。

---

## 三、架构总览（可扩展性核心，不引入新 Autoload）

遵循 skill 的 **Layer Cake** 与 **Signal Up, Call Down**：

```
DATA                    LOGIC/ABSTRACTION              PRESENTATION
┌──────────────┐     ┌──────────────────────┐     ┌──────────────────┐
│ LootTable     │     │ ILootSource           │     │ RewardPage        │
│ (GDScript)    │     │ (接口)                 │────►│ (Control, 场景局部)│
│ 瓶盖/水龙头/   │     │  ChestInteractable     │     │ 渲染槽 + 领取      │
│ 卡牌/道具     │     │  HostileNPC            │     │ RewardOpened 信号 │
└──────────────┘     │  EphemeralLootSource   │     └──────────────────┘
                     │  (未来: 任务/事件/成就) │
                     └──────────────────────┘
```

**关键决策**：

1. **不引入新的 Autoload**。`RewardPage` 是探索场景内的局部 UI。
   - **同场景来源**（宝箱、未来任务/事件）引用 `ExplorationHUD`，调 `Hud.ShowReward(source)`，由 HUD → `PlayerTV.OpenReward` → `RewardPage.Open`（同 `WorkbenchInteractable → [Export] ExplorationHUD Hud` 模式）。
   - **跨场景（战斗）**复用**已存在的** `BattleDirector` autoload，或干脆让尸体可点击、不做跨场景弹出。
2. `RewardPage` 只依赖 `ILootSource` 接口，**不认识**宝箱/敌人的具体类型。新来源 = 实现接口 + 触发 `ShowReward`，RewardPage 零改动。

---

## 四、数据层：`LootTable`（GDScript）

新增 `Script/GD/resource/loot_table.gd`（`extends Resource` + `class_name LootTable`，风格同 card_data.gd/itemdata.gd）：

```gdscript
extends Resource
class_name LootTable

@export var bottle_cap: int = 0
@export var faucet: int = 0
@export var cards: Array[CardData] = []
@export var items: Array[ItemData] = []

func is_empty() -> bool: ...
func take_bottle_cap() -> int: ...
func take_faucet() -> int: ...
func take_card(index: int) -> CardData: ...   # 移除并返回
func take_item(index: int) -> ItemData: ...   # 移除并返回
func to_dict() -> Dictionary: ...             # 存 resource_path，供持久化
func from_dict(data: Dictionary) -> void: ... # GD.Load(path) 重建
```

**skill 硬规则**：
- LootTable 运行时会被**改动**（领取后移除），源必须 `Duplicate(false)` 一份运行时副本（浅拷贝：卡牌/道具引用共享、数组独立），不能改共享的 `.tres`。
- C# 侧通过 `GodotObject` + `Get/Call` + `GDScriptKeys.LootTable.*` 读写。

---

## 五、抽象层：`ILootSource` + `EphemeralLootSource`

新增 `Script/CS/ILootSource.cs`（纯 C# 接口）：

```csharp
public interface ILootSource
{
    Resource RemainingLoot { get; }   // GDScript LootTable 资源（可变引用）
    void OnLootClaimed();             // 每次领取后的持久化回调
}
```

新增 `Script/CS/EphemeralLootSource.cs`（`RefCounted`，一次性奖励用，不持久化）：

```csharp
public partial class EphemeralLootSource : RefCounted, ILootSource
{
    public Resource RemainingLoot { get; }
    public EphemeralLootSource(Resource loot) => RemainingLoot = loot;
    public void OnLootClaimed() { }
}
```

**可扩展点在此**：宝箱、敌人、未来的任务/事件/成就，都是「实现 `ILootSource`」。一次性奖励用 `EphemeralLootSource` 包一个 `LootTable` 即可。

---

## 六、表现层：`RewardPage`

`features/tv_ui/RewardPage.cs` + `PlayerTV.tscn` 里 RewardPage 节点下的子节点（**保持 TV 右上角小窗位置**）：

```
RewardPage (Control, 默认 visible=false)
├── TextureRect2   (背景，已存在)
├── Title          (Label "战利品")
├── LootList       (VBoxContainer)   ← 动态生成奖励槽
├── CloseBtn       (Button)
└── Anim           (AnimationPlayer)
    ├── RESET       → visible=false
    ├── show_reward → 弹出，frame0 method→EnableButtons(true)
    └── hide_reward → 合上，frame0 method→EnableButtons(false)
```

**脚本 API**：

```csharp
[Signal] public delegate void RewardOpenedEventHandler();   // 弹出后（上行）
[Signal] public delegate void AllClaimedEventHandler();     // 全部领取完
[Signal] public delegate void ClosedEventHandler();         // 合上

public void Open(ILootSource source);   // 填充 + show_reward + emit RewardOpened
public void Close();                    // hide_reward
```

**内部逻辑**：
- `RebuildSlots`：把 `RemainingLoot` 展平成槽——瓶盖×N、水龙头×N、每张卡牌一槽、每件道具一槽；字段读取用 `GDScriptKeys`。
- 点击领取：货币 → `ModifyCurrency` + `take_bottle_cap/take_faucet`；卡牌 → `AcquireCard` + `take_card`；道具 → `AddItem` + `take_item`（**背包满 4 → 不移除、槽显示「背包已满」并禁用**）。
- 每次领取后：`OnLootClaimed()` → 重渲染 → `IsEmpty` 则 `Close()` + `AllClaimed`。

---

## 七、TV 集成：弹出时切换到背包界面

`PlayerTV` / `ExplorationHUD` 各加一个转调方法 + 一条连接：

```csharp
// ExplorationHUD
public void ShowReward(ILootSource source) => _playerTV?.OpenReward(source);

// PlayerTV._Ready
_rewardPage.RewardOpened += SwitchToInventory;   // 弹出时切到背包

// PlayerTV
public void OpenReward(ILootSource source) => _rewardPage?.Open(source);
public void SwitchToInventory() { /* 算方向 delta，调 SwitchPanel */ }
```

遵循 **Signal Up / Call Down**：`RewardPage` 只发 `RewardOpened`，父节点决定「切到背包」。

---

## 八、触发源 A：宝箱

`ChestInteractable.cs`（实现 `ILootSource`）：

```csharp
public partial class ChestInteractable : InteractableBase, ILootSource
{
    [Export] public Resource Loot { get; set; }        // GDScript LootTable，设计器配
    [Export] public ExplorationHUD Hud { get; set; }   // 路由到 RewardPage

    public Resource RemainingLoot { get; private set; }
    public void OnLootClaimed() => PersistInteraction(_mapId);

    public override void HandleInteract()
    {
        EnsureRemainingLoot();   // RemainingLoot ??= (Resource)Loot.Duplicate(false)
        if (RemainingLoot == null || IsLootEmpty()) { GD.Print($"「{DisplayName}」已空。"); return; }
        IsOpened = true;
        PersistInteraction(_mapId);
        Hud?.ShowReward(this);
    }
}
```

- 持久化 `SaveState/LoadState`：`opened` + `loot`（`to_dict()`/`from_dict()` 重建）。

---

## 九、触发源 B：敌人尸体 + 战斗胜利

`HostileNPC.cs`（实现 `ILootSource`）：

```csharp
public partial class HostileNPC : NPCBase, ILootSource
{
    [Export] public Resource Loot { get; set; }
    [Export] public ExplorationHUD Hud { get; set; }
    public Resource RemainingLoot { get; private set; }
    public void OnLootClaimed() => GameState.Instance?.SetObjectState(...);
}
```

- `ShowCorpse()` 从「不可交互」改为「可点击领取剩余战利品」（`RemainingLoot` 非空时启用 clickZone）。
- `HandleInteract()`：尸体且有剩余 → `Hud?.ShowReward(this)`。
- 持久化 `_defeated` + `RemainingLoot`。

**MVP 方案（无跨场景弹出）**：战斗胜利 → `BattleDirector.OnBattleWon()` 标记 defeated → 回探索 → 尸体可点击，玩家点尸体开 RewardPage。战利品本来就在尸体上、已持久化，**不需要跨场景传战利品**。

**可选增强（自动弹出）**：复用 `BattleDirector` 的 `NpcPersistenceId`，胜利后记为「待领取」，探索加载后由 `ExplorationManager` 找到 NPC 调 `ShowReward`。仍不新增 autoload。

---

## 十、关键流程时序

```
开宝箱：
点击宝箱 → HandleInteract → RemainingLoot ??= Loot.Duplicate(false)
  → Hud.ShowReward(this) → PlayerTV.OpenReward → RewardPage.Open(this)
  → emit RewardOpened → PlayerTV.SwitchToInventory()
  → 点槽 → 领取 → OnLootClaimed(持久化) → RebuildSlots
  → …直到 IsEmpty → Close + AllClaimed
  （或点 CloseBtn → Close，剩余留在宝箱）
```

---

## 十一、边界与细节

- **背包满（4）**：道具槽领取失败 → 不移除、不关闭，槽显示「背包已满」并禁用。
- **Resource 复制**：`RemainingLoot` 必须 `Loot.Duplicate(false)`。
- **持久化**：`to_dict` 存 `resource_path` 数组，`from_dict` 用 `GD.Load` 重建。
- **GDScriptKeys**：RewardPage 读字段用 `GDScriptKeys.LootTable.*` / `ItemData.Icon` / `ItemData.Description`（已补）。卡牌无 `icon` 贴图，用 `IconText`/`Glyph`。
- **Tooltip**：每个槽复用 `TooltipService.ShowFor`。
- **禁用按钮**：show/hide 动画 method track 调 `EnableButtons`。
- **Signal 安全**：`RewardOpened` 用 named method 连接；PlayerTV `_ExitTree` 断开。

---

## 十二、文件清单

**新增**：

| 文件 | 类型 |
|---|---|
| `Script/GD/resource/loot_table.gd` | GDScript Resource |
| `Script/CS/ILootSource.cs` | C# interface |
| `Script/CS/EphemeralLootSource.cs` | C# RefCounted |

**修改**：

| 文件 | 操作 |
|---|---|
| `features/tv_ui/RewardPage.cs` | 补全逻辑 |
| `features/tv_ui/PlayerTV.tscn` | RewardPage 加 LootList/CloseBtn/Anim |
| `features/tv_ui/PlayerTV.cs` | 加 `OpenReward`/`SwitchToInventory` + 连 `RewardOpened` |
| `features/tv_ui/ExplorationHUD.cs` | 加 `ShowReward` 路由 |
| `features/exploration/scripts/CS/interactables/ChestInteractable.cs` | 实现 ILootSource + `[Export] Hud` |
| `features/exploration/scripts/CS/interactables/HostileNPC.cs` | 实现 ILootSource + 尸体可领 |
| `Script/CS/GDScriptKeys.cs` | ItemData 补 `Icon`/`Description` + 新增 LootTable 组 |
| `Script/CS/BattleDirector.cs` | （可选，仅做自动弹出时） |

---

## 十三、分阶段实施

1. **Phase 1（核心闭环）**：loot_table.gd + ILootSource + EphemeralLootSource + RewardPage + PlayerTV/ExplorationHUD 路由 + **宝箱对接**。✅ 已完成
2. **Phase 2（战斗）**：`HostileNPC` 实现 ILootSource + 尸体可领；（可选）自动弹出。
3. **Phase 3（打磨）**：背包满提示、Tooltip、禁用按钮、动画细节、持久化回归。

---

## 十四、可扩展性验证清单（未来新来源接入步骤）

接入一个新来源（任务奖励 / 地图事件 / 商店满赠）只需：

1. 准备一个 `LootTable`（`.tres` 或代码 `new()`）。
2. 需持久化 → 让类实现 `ILootSource`（`RemainingLoot` + `OnLootClaimed`）。
3. 一次性 → `new EphemeralLootSource(loot)`。
4. 触发展示：持有 `ExplorationHUD` 引用 → `Hud.ShowReward(source)`。

**不改** RewardPage、不改 PlayerTV、不改 DataManager、**不加新 autoload**。
