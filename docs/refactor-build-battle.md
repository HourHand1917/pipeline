# 构筑 & 战斗系统重构说明

> **日期**: 2026-08-01  
> **分支**: `距离系统可视化+buff设计`  
> **重构范围**: BuildScreen、BattleScreen、BattleManager、BoardManager、EffectResolver  
> **未变动**: DataManager（全局 Autoload）、所有 .tres 资源、所有 GDScript 脚本、探索系统、主菜单

---

## 1. 架构变更总览

```
重构前                              重构后
─────────                          ─────────
MainScene                          MainScene
├─ BuildScreen (UI + 逻辑混杂)      ├─ BuildScreen (构筑Canvas覆盖层)
├─ BattleScreen (UI + 逻辑混杂)      ├─ BattleScreen (协调层)
├─ BoardManager                    │  └─ UIManager (新增：UI管理器)
├─ BattleManager                   ├─ BoardManager
└─ EffectResolver                  ├─ BattleManager (主循环纯净)
                                   └─ EffectResolver
```

### Layer Cake 分层

```
PRESENTATION  ← UIManager + BuildScreen 按钮
LOGIC         ← BattleManager + BoardManager
DATA          ← CardData / Buff / GameRules (.tres, GDScript)
INFRASTRUCTURE ← DataManager (Autoload)
```

---

## 2. 文件变更清单

### 新增文件

| 文件 | 说明 |
|------|------|
| `Script/CS/UIManager.cs` | 战斗 UI 管理器，从 BattleScreen 分离出所有视觉更新逻辑 |
| `Script/CS/GDScriptKeys.cs` | C# ↔ GDScript 跨语言字符串常量（6 个嵌套类，50+ 常量） |

### 修改文件

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Script/CS/BuildScreen.cs` | 重写 | 构筑 Canvas 覆盖层模式（`Open()`/`Close()`/`Toggle()`） |
| `Script/CS/BattleScreen.cs` | 重写 | 简化为协调层，UI 委托给 UIManager |
| `Script/CS/BattleManager.cs` | 重构 | 主循环职责清晰化，板子操作委托 BoardManager |
| `Script/CS/BoardManager.cs` | 微调 | 新增 `ResetAllCardStates()`，GDScriptKeys 替换裸字符串 |
| `Script/CS/MainScene.cs` | 重写 | 信号连接更新，架构文档注释 |

### 未修改（保持原样）

| 文件/目录 | 原因 |
|-----------|------|
| `Script/CS/DataManager.cs` | 全局 Autoload，与其他协作者统一，不可更改 |
| `Script/GD/resource/*.gd` | 资源定义脚本无需变动 |
| `Script/GD/refcounted/*.gd` | RefCounted 数据类无需变动 |
| `Resource/card/*.tres` | 8 张卡牌资源完整保留 |
| `Resource/effect/**/*.tres` | 所有效果资源完整保留 |
| `Resource/characterdata/**/*.tres` | 角色数据完整保留 |
| `Scenes/game_scene/*.tscn` | 场景文件无需修改（脚本路径未变） |
| `features/exploration/**` | 探索系统不在此次重构范围内 |

---

## 3. 关键设计决策

### 3.1 UIManager 分离

**为什么**：原 BattleScreen 既管理 UI 节点引用，又执行业务逻辑判断。按架构导图，UI 更新应该是一个独立层。

**怎么做**：UIManager 拥有所有 UI 节点引用（通过属性注入），负责刷新板子按钮、状态文字、距离轨道、发动按钮、日志。BattleScreen 只做协调——把场景中的节点引用注入 UIManager，转发信号。

**信号流**：
```
用户点击 → UIManager 信号 → BattleScreen(Facade) → MainScene → BattleManager
```

### 3.2 构筑 Canvas 覆盖层

**为什么**：架构要求 "无需加载场景，可以唤出覆盖当前 UI"。

**怎么做**：BuildScreen 新增 `Open()` / `Close()` / `Toggle()` 方法，通过 `Visible` 属性控制显示。不需要 `change_scene`。

### 3.3 DataManager 桥接

**为什么**：DataManager 不可修改，但需要跨系统传递卡牌桌数据。

**怎么做**：使用已有的 `SaveBuildWithSize()` 和 `LoadBuild()` 接口。构筑完成时保存，战斗开始时读取。BoardManager 负责实际的卡牌放置/移除逻辑。

### 3.4 GDScriptKeys 常量

**为什么**：C# 调用 GDScript 对象属性和方法时使用裸字符串（如 `data.Get("display_name")`），拼写错误在编译期无提示，运行时静默失败。

**怎么做**：所有跨语言键名集中到 `GDScriptKeys` 静态类，编译期即可发现拼写错误。

```csharp
// 之前（运行时拼写错误无提示）
data.Get("display_name")

// 之后（编译期检查）
data.Get(GDScriptKeys.CardData.DisplayName)
```

### 3.5 距离轨道增量更新

**为什么**：每次 `BattleStateChanged` 信号触发时全量销毁并重建 distancetrack 节点树，频繁 GC 分配。

**怎么做**：缓存 `_lastCellCount`，仅在格数变化时重建 DOM。其余情况只遍历现有子节点更新文字和颜色。

---

## 4. 信号连接图

```
                    MainScene
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   BuildScreen    BattleScreen    BoardManager
   (构筑Canvas)   (协调层)        (桌面管理)
        │               │               │
        │               ▼               │
        │          UIManager            │
        │          (UI管理)             │
        │               │               │
        ▼               ▼               ▼
   ─────────── DataManager (Autoload) ───────────
        │                                    │
        ▼                                    ▼
   SaveBuildWithSize                  LoadBuild
```

### 关键信号

| 信号 | 发送方 | 接收方 | 用途 |
|------|--------|--------|------|
| `PlaceCardRequested` | BuildScreen | MainScene | 放置卡牌 |
| `StartBattleRequested` | BuildScreen | MainScene | 开始战斗 |
| `LightCellRequested` | UIManager | MainScene | 点亮格子 |
| `PlayCardRequested` | UIManager | MainScene | 发动卡牌 |
| `MoveRequested` | UIManager | MainScene | 移动 |
| `EndTurnRequested` | UIManager | MainScene | 结束回合 |
| `BackToBuildRequested` | UIManager | MainScene | 返回构筑 |
| `BattleStateChanged` | BattleManager | MainScene | 刷新全部 UI |
| `BoardChanged` | BoardManager | MainScene | 板子变化 |
| `LogMessage` | BattleManager | BattleScreen→UIManager | 战斗日志 |

---

## 5. 卡牌数据保留

所有 8 张卡牌的 `.tres` 资源文件未做任何修改：

| 文件 | ID | 名称 |
|------|-----|------|
| `Resource/card/battery.tres` | `battery` | 发条电池 |
| `Resource/card/battery_up.tres` | `battery_up` | 发条电池+ |
| `Resource/card/medkit.tres` | `medkit` | 医疗包 |
| `Resource/card/medkit_up.tres` | `medkit_up` | 医疗包+ |
| `Resource/card/revolver.tres` | `revolver` | 转轮手枪 |
| `Resource/card/revolver_up.tres` | `revolver_up` | 转轮手枪+ |
| `Resource/card/shield.tres` | `shield` | 护盾 |
| `Resource/card/shield_up.tres` | `shield_up` | 护盾+ |

---

## 6. Git 合并注意事项

如果将此分支合并到 `main`，冲突范围仅限于以下 5 个 C# 文件：

- `Script/CS/BuildScreen.cs`
- `Script/CS/BattleScreen.cs`
- `Script/CS/BattleManager.cs`
- `Script/CS/BoardManager.cs`
- `Script/CS/MainScene.cs`

**建议合并策略**：对这 5 个文件选择 `accept theirs`（接受重构版本）。

**无冲突文件**：
- `Script/CS/DataManager.cs` — 未修改
- 所有 `.tres` / `.gd` / `.tscn` — 未修改
- `Script/CS/UIManager.cs` — 新文件，不会冲突
- `Script/CS/GDScriptKeys.cs` — 新文件，不会冲突
