# 朝向系统 & 绕后卡牌 设计文档

> 创建日期：2026-08-03  
> 项目：Godot 4 回合制卡牌战斗原型

---

## 一、概述

在一维地图（7 格）回合制卡牌战斗中加入"朝向"机制，并新增一张位移卡牌"绕后"。

- **朝向**：每个战斗单位有 `Facing` 属性（0=正方向/右，1=负方向/左）
- **射程感知**：卡牌和敌人行动改为朝向感知的射程检查
- **移动绑定**：移动方向以自身朝向为基准；移动后敌人自动重算朝向
- **绕后卡牌**：玩家移动到敌人背后，双方朝向翻转

---

## 二、数据结构

### 2.1 朝向常量（BattleManager）

```csharp
public const int FacingPositive = 0; // 正方向（地图右方 / 格子编号增大）
public const int FacingNegative = 1; // 负方向（地图左方 / 格子编号减小）
```

### 2.2 PlayerBattle / EnemyBattle

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Facing` | `int` | `0` | 当前朝向 |

### 2.3 EnemyBattle.UpdateFacing(int playerPos)

```
Facing = (自身位置 - 玩家位置) > 0 ? 0 : 1
```

- 敌人在玩家右侧 → FacingPositive（面向右/远离玩家）
- 敌人在玩家左侧 → FacingNegative（面向左/远离玩家）

### 2.4 CombatEffectData.Type 枚举

```
SWAP_POSITION  // 新增：绕后换位
```

`type_key()` 返回 `&"swap_position"`，由 EffectResolver 的 switch 分发。

---

## 三、核心算法

### 3.1 IsInRange — 朝向感知射程检查

```csharp
public static bool IsInRange(int selfPos, int targetPos, int facing, int minRange, int maxRange)
```

| facing | 判定逻辑 |
|--------|----------|
| `FacingPositive (0)` | `(targetPos - selfPos)` ∈ `[minRange, maxRange]` |
| `FacingNegative (1)` | `(selfPos - targetPos)` ∈ `[minRange, maxRange]` |

若 `minRange <= 0 && maxRange <= 0`（无射程需求），直接返回 `true`。

### 3.2 移动方向计算

```
direction = Facing == FacingPositive ? 1 : -1
if action == Backward: direction *= -1
```

前进 = 朝向方向；后退 = 朝向反方向。边界由 `IsValidCell` 兜底。

### 3.3 敌人朝向自动更新

以下时机触发所有敌人的 `UpdateFacing(Player.MapPosition)`：

| 触发点 | 方法 |
|--------|------|
| 战斗开始 | `StartBattle` |
| 玩家主动移动 | `TryMove` |
| 效果触发的移动 | `MoveCombatantRelative` |
| 绕后换位 | `SwapPosition` |

---

## 四、绕后卡牌（SwapPosition）

### 4.1 卡牌参数建议

| 参数 | 基础版 | 强化版 |
|------|--------|--------|
| `min_range` | 1 | 1 |
| `max_range` | 1 | 4 |
| `effects[0].type` | SWAP_POSITION | SWAP_POSITION |
| `effects[0].target` | ENEMY | ENEMY |
| `cooldown_turns` | 2 | 3 |

### 4.2 执行流程

```
1. TryPlayCard 阶段
   ├── HasRangeTarget() → true（card_data.gd）
   ├── IsInRange(playerPos, enemyPos, playerFacing, minRange, maxRange)
   │   └── 确保敌人在玩家正前方指定射程内
   └── 通过 → 进入 EffectResolver

2. EffectResolver.ExecuteEffect
   └── case "swap_position" → battleManager.SwapPosition()

3. BattleManager.SwapPosition()
   ├── 取主要敌人
   ├── 计算敌人背后位置：behindEnemy = enemyFacing==0 ? enemyPos-1 : enemyPos+1
   ├── 验证 behindEnemy 合法（IsValidCell）、不与敌人重叠
   ├── 玩家移到 behindEnemy
   ├── player.Facing 翻转
   ├── enemy.Facing 翻转
   ├── 所有敌人 UpdateFacing
   └── 发射 BattleStateChanged
```

### 4.3 失败条件

| 条件 | 日志 |
|------|------|
| 敌人不在正前方 | `射程不足：距离X格，需要A–B格（朝向0/1）` |
| 敌人背后无空间 | `敌人背后无空间，无法绕后。` |
| 绕后位置与敌人重叠 | `绕后位置与敌人重叠，无法执行。` |

---

## 五、改动文件清单

| 文件 | 改动类型 | 要点 |
|------|----------|------|
| `Script/GD/resource/combat_effect_data.gd` | 枚举 + 3 方法 | 新增 SWAP_POSITION |
| `Script/GD/resource/card_data.gd` | +2 方法，改 1 方法 | has_swap_effect / has_range_target |
| `Script/CS/GDScriptKeys.cs` | +2 常量 | HasSwapEffect / HasRangeTarget |
| `Script/CS/PlayerBattle.cs` | +1 属性 | Facing |
| `Script/CS/EnemyBattle.cs` | +1 属性 +1 方法 | Facing / UpdateFacing |
| `Script/CS/BattleManager.cs` | +2 常量 +2 方法，改 6 方法 | IsInRange / SwapPosition / 朝向集成 |
| `Script/CS/EffectResolver.cs` | +1 case | swap_position |

---

## 六、设计约束

- **不引入强类型 GDScript 引用**：C# 侧继续通过 `GodotObject.Get/Set/Call` 动态访问
- **GDScript snake_case，C# PascalCase**：命名规范不变
- **信号驱动 UI**：Facing 改变不单独发信号；距离轨道随 `PositionChanged` 自动刷新
- **框架不变**：BattleManager / BoardManager / EffectResolver / UIManager 职责边界不变

---

## 七、后续扩展方向

- **朝向影响伤害**：背刺伤害加成
- **朝向绑定卡牌**：仅特定朝向可发动的卡牌
- **AI 朝向策略**：敌人主动转身面对玩家
- **多敌人朝向**：当前 UpdateFacing 已支持遍历所有敌人
