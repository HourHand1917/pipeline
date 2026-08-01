# 战斗实例化重构说明

> **日期**: 2026-08-01  
> **重构目标**: 玩家/敌人从 BattleManager 的裸 int 变量提升为独立场景实例  
> **输出文件**: PlayerBattle.cs, EnemyBattle.cs, EnemyManager.cs, BattleManager.cs, BattleScreen.cs, MainScene.cs

---

## 1. 核心变更：stats 归属

```
重构前                              重构后
─────────                          ─────────
BattleManager                      BattleManager (纯主循环)
├─ int PlayerHp        ❌           │
├─ int PlayerMaxHp     ❌           ├─ PlayerBattle Player        ✅ 引用
├─ int PlayerShield    ❌           ├─ EnemyManager EnemyManager  ✅ 引用
├─ int PlayerEnergy    ❌           ├─ BoardManager
├─ int EnemyHp         ❌           └─ EffectResolver
├─ int EnemyMaxHp      ❌
├─ int EnemyShield     ❌           PlayerBattle (场景实例)
└─ int PlayerMapPos    ❌           ├─ CurrentHp, MaxHp, Shield
                                    ├─ Energy, MaxEnergy
    敌人不存在为节点       ❌           ├─ MapPosition
                                    ├─ DisplayName, Glyph, Tint
                                    ├─ 信号: HealthChanged ↑
                                    ├─       ShieldChanged ↑
                                    ├─       EnergyChanged ↑
                                    ├─       PositionChanged ↑
                                    └─       Died ↑

                                    EnemyManager (场景实例)
                                    ├─ EnemyBattle[] Enemies
                                    ├─ SpawnEnemy(EnemyData)
                                    ├─ GetPrimaryEnemy()
                                    ├─ GetActionForDistance()
                                    └─ 信号: EnemySpawned ↑
                                            EnemyDied ↑
                                            AllEnemiesDefeated ↑
```

## 2. PlayerBattle — 玩家战斗实例

**文件**: `Script/CS/PlayerBattle.cs`

### 属性
| 属性 | 类型 | 来源 | 说明 |
|------|------|------|------|
| `MaxHp` | int | PlayerData.max_hp | 最大生命 |
| `CurrentHp` | int | = MaxHp 初始 | 当前生命 |
| `Shield` | int | PlayerData.initial_shield | 当前护盾 |
| `Energy` | int | GameRules.energy_per_turn | 当前能量 |
| `MaxEnergy` | int | GameRules.energy_per_turn | 最大能量 |
| `MapPosition` | int | BattleMap.player_start_cell | 地图位置 |
| `DisplayName` | string | PlayerData.display_name | "rubber" |
| `Glyph` | string | PlayerData.glyph | "旅" |
| `Tint` | Color | PlayerData.tint | #f4cf61 |

### 方法
| 方法 | 说明 |
|------|------|
| `LoadFromData(playerData, rules)` | 从 PlayerData + GameRules 资源加载初始值 |
| `TakeDamage(amount)` | 先扣护盾再扣血，≤0 时触发 `Died` 信号 |
| `AddShield(amount)` | 增加护盾，触发 `ShieldChanged` |
| `Heal(amount)` | 回复生命（不超过 MaxHp），触发 `HealthChanged` |
| `SpendEnergy(amount)` | 消耗能量，返回是否成功，触发 `EnergyChanged` |
| `AddEnergy(amount)` | 增加能量，触发 `EnergyChanged` |
| `ResetEnergy()` | 能量回满（回合开始时调用） |
| `SetMapPosition(pos)` | 设置位置，触发 `PositionChanged` |
| `ApplyBuff(buff, stacks)` | 触发 `BuffApplied` 信号（UI 更新 buff 条） |
| `RemoveBuff(buffId)` | 触发 `BuffRemoved` 信号 |

### 信号（UI 订阅）
```
HealthChanged(int current, int max)   → 更新血条
ShieldChanged(int current)           → 更新护盾显示
EnergyChanged(int current, int max)  → 更新能量灯
PositionChanged(int newPosition)     → 重绘距离轨道
BuffApplied(GodotObject, int)        → 更新 buff 条
BuffRemoved(string)                  → 移除 buff 图标
Died()                               → 显示阵亡
```

---

## 3. EnemyBattle — 敌人战斗实例

**文件**: `Script/CS/EnemyBattle.cs`

与 PlayerBattle 结构对称，额外包含：
- `GetEnemyData()` — 返回原始 EnemyData 资源（含 actions 配置，供 EnemyManager 行为选择）
- `EnemyId` — 敌人标识符

---

## 4. EnemyManager — 敌人管理器

**文件**: `Script/CS/EnemyManager.cs`

### 职责
- 持有一个或多个 `EnemyBattle` 实例
- 提供生成/移除/查询接口
- 负责敌方回合的行为选择

### 关键方法
| 方法 | 说明 |
|------|------|
| `SetEnemyConfigs(GodotObject[])` | 设置敌人数据配置数组（预留多敌人接口） |
| `SpawnAllFromConfigs(rules)` | 批量生成。若无 config，回退到 rules.enemy_data 单敌人生成 |
| `SpawnEnemy(enemyData)` | 生成单个 EnemyBattle，绑定 Died 信号 |
| `GetPrimaryEnemy()` | 获取第一个存活敌人（当前单敌人场景的默认目标） |
| `GetAliveEnemies()` | 返回所有存活敌人 |
| `HasAliveEnemies()` | 是否还有存活敌人 |
| `GetActionForDistance(distance)` | 根据距离从敌人数据中选最佳行动 |
| `GetTargetEnemy(index)` | 获取效果目标的敌人实例 |

### 信号
```
EnemySpawned(EnemyBattle)     → UIManager 自动绑定
EnemyDied(EnemyBattle)        → 刷新距离轨道
AllEnemiesDefeated()           → 战斗胜利
```

---

## 5. BattleManager — 纯主循环

**文件**: `Script/CS/BattleManager.cs`

### 变更
- 删除了所有 `int PlayerHp/PlayerShield/...` 属性定义
- 改为 `public PlayerBattle Player { get; set; }` 引用
- 改为 `public EnemyManager EnemyManager { get; set; }` 引用
- 保留 `public int PlayerHp => Player?.CurrentHp ?? 0` 作为计算属性（兼容 EffectResolver 的旧 API）
- `DamagePlayer/AddPlayerShield/HealPlayer` 等方法内部委托给 `Player.TakeDamage/AddShield/Heal`
- `CheckBattleEnd()` 改为检查 `!EnemyManager.HasAliveEnemies()` 和 `!Player.IsAlive`

---

## 6. BattleScreen + UIManager — 信号驱动 UI

**文件**: `Script/CS/BattleScreen.cs`, `Script/CS/UIManager.cs`

### 关键新增

`BattleScreen.BindBattleInstances(player, enemyManager)` → 调用 UIManager：

```
UIManager.BindPlayer(player)
├─ player.HealthChanged ──→ 更新 StatusLabel.Text
├─ player.ShieldChanged ──→ 更新 StatusLabel.Text
├─ player.EnergyChanged ──→ 更新 EnergyLabel.Text
├─ player.PositionChanged ──→ RefreshDistanceTrack()
├─ player.BuffApplied ──→ GD.Print (预留 buff 条 UI)
├─ player.BuffRemoved ──→ GD.Print (预留 buff 条 UI)
└─ player.Died ──→ StatusLabel.Text = "玩家阵亡！"

UIManager.BindEnemyManager(manager)
├─ manager.EnemySpawned ──→ BindEnemy(enemy) 逐个绑定
│   ├─ enemy.HealthChanged ──→ 更新 StatusLabel
│   ├─ enemy.ShieldChanged ──→ 更新 StatusLabel
│   ├─ enemy.PositionChanged ──→ RefreshDistanceTrack()
│   └─ enemy.Died ──→ StatusLabel + RefreshDistanceTrack()
└─ manager.AllEnemiesDefeated ──→ StatusLabel += "敌人全灭！"
```

### 从"轮询"到"推送"

```csharp
// 之前（每帧/每次信号都全量重建 UI 文字）：
private void RefreshStatus() {
    StatusLabel.Text = $"玩家 HP {battleManager.PlayerHp}/{battleManager.PlayerMaxHp} ...";
}

// 之后（仅在值变化时更新对应 Label）：
player.HealthChanged += (cur, max) => {
    StatusLabel.Text = $"玩家 HP {cur}/{max} ...";  // 仅在 HP 变化时触发
};
player.EnergyChanged += (cur, max) => {
    EnergyLabel.Text = $"⚡ {cur} / {max}";         // 仅在能量变化时触发
};
```

---

## 7. MainScene — 黑箱战斗入口

**文件**: `Script/CS/MainScene.cs`

### 初始化流程

```
_Ready()
├─ CreateBattleInstances(rules)
│   ├─ new PlayerBattle()          → AddChild
│   └─ new EnemyManager()          → AddChild
│       └─ SetEnemyConfigs([enemyData])
├─ BattleManager.Player = Player           ← 注入
├─ BattleManager.EnemyManager = EnemyManager ← 注入
├─ BattleManager.BoardManager = ...        ← 注入
├─ BattleScreen.BindBattleInstances(...)    ← 信号订阅
├─ 连接所有 UI 信号
└─ ShowBuild()                              ← 默认显示构筑
```

### 外部调用

外部场景只需 `change_scene` 到此即可。MainScene 自动完成：
1. 从 DataManager + GameRules 加载配置
2. 实例化战斗实体
3. 注入依赖
4. 绑定信号
5. 显示构筑界面

---

## 8. 文件依赖图

```
MainScene
├─ PlayerBattle.cs          (new)
├─ EnemyManager.cs          (new)
│   └─ EnemyBattle.cs       (new)
├─ BattleManager.cs         (modified)
├─ BattleScreen.cs          (modified)
│   └─ UIManager.cs         (new, 上轮重构)
├─ BoardManager.cs          (modified, 上轮重构)
├─ BuildScreen.cs           (modified, 上轮重构)
├─ EffectResolver.cs        (unchanged)
├─ DataManager.cs           (unchanged, Autoload)
├─ GDScriptKeys.cs          (new, 上轮重构)
└─ GameRules.tres           (unchanged)
    ├─ player_data.tres
    ├─ enemy_data.tres
    └─ battle_map.tres
```

---

## 9. 扩展指南

### 添加第二个敌人
```csharp
// 在 EnemyManager.SetEnemyConfigs 中传入多个 enemy_data：
var configs = new Array<GodotObject> { enemyData1, enemyData2 };
EnemyManager.SetEnemyConfigs(configs);
// SpawnAllFromConfigs 会逐个生成，UIManager 自动绑定信号
```

### 替换为 .tscn 场景
```csharp
// MainScene.CreateBattleInstances 中：
Player = GD.Load<PackedScene>("res://Scenes/game_scene/player_battle.tscn").Instantiate<PlayerBattle>();
EnemyManager = GD.Load<PackedScene>("res://Scenes/game_scene/enemy_manager.tscn").Instantiate<EnemyManager>();
```

### 添加动画机
在 PlayerBattle/EnemyBattle 场景中添加 `AnimatedSprite2D` + `AnimationTree`：
- `TakeDamage()` 中调用 `animTree.Set("parameters/conditions/hit", true)`
- `Died()` 中调用 `animTree.Set("parameters/conditions/dead", true)`
- `PositionChanged` 触发时，由 UIManager 将实例 reparent 到对应轨道格子的 Panel 下
