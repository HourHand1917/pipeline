# EnermyMannager（敌人 AI）本地使用说明

> 项目沿用策划案中的拼写 `EnermyMannager`。本功能全部位于 `features/enemy_ai/`，只新增文件；原有场景、脚本、资源均未改动。没有上传 GitHub，也没有 commit / push。

## 1. 可直接使用的场景

三个敌人的独立管理器场景：

- `res://features/enemy_ai/scenes/boom_enermy_mannager.tscn`
- `res://features/enemy_ai/scenes/rocky_enermy_mannager.tscn`
- `res://features/enemy_ai/scenes/sharkk_enermy_mannager.tscn`

通用底座：

- `res://features/enemy_ai/scenes/enermy_mannager.tscn`

不改原战斗场景的接入版（继承原战斗场景，并额外挂载对应管理器）：

- `res://features/enemy_ai/battle_presets/boom_battle.tscn`
- `res://features/enemy_ai/battle_presets/rocky_battle.tscn`
- `res://features/enemy_ai/battle_presets/sharkk_battle.tscn`

需要测试某个敌人时，直接打开对应的 `battle_presets/*.tscn`。原来的 `res://Scenes/game_scene/game.tscn` 保持不变。

## 2. Inspector（右侧面板）调参

三个 AI 配置资源：

- `res://features/enemy_ai/profiles/boom_profile.tres`
- `res://features/enemy_ai/profiles/rocky_profile.tres`
- `res://features/enemy_ai/profiles/sharkk_profile.tres`

选中资源后，可在 Inspector 修改：

- `Combat / Max Hp`、`Initial Shield`：血量与开场护盾。
- `Track / Start Cell`、`Cell Count`：出生格和战场总格数。
- `Decision Tuning / Reactive Damage Threshold`：受到多少伤害后触发反制。
- `Decision Tuning / Defensive Hp Ratio`：低血量防守倾向阈值。
- `Decision Tuning / Random Seed`：固定随机种子；同一种子与同一状态会得到同一意图。
- `Actions`：展开每个动作，可修改距离、伤害、移动格数、护盾、冷却、选择权重等。

动作资源常用字段：

- `Min Range / Max Range`：动作可以使用的距离（包含首尾）。
- `Cooldown Turns`：使用后，需要经过多少个完整敌方决策回合才可再次选择。
- `Move Toward / Move Away`：靠近 / 远离玩家的格数。
- `Base Priority`：先比较优先级；同优先级才按 `Random Weight` 抽取。
- `Requires Prepared Dash`：必须先进入“准备冲刺”状态。
- `Stun Self Turns`：动作后自身眩晕的敌方回合数。

## 3. 当前三套默认配置

| 敌人 | 默认血量 | 核心行为 |
|---|---:|---|
| boom | 7 | 距离 1 必定攻击；距离 2 在攻击/逼近间稳定随机；远处前进 1 或 2 格 |
| rocky | 30 | 距离 1 重击 6（冷却 2）；距离 1–3 攻击 4；防退 1 格并加盾 7（冷却 2）；撤退 2 格（冷却 3） |
| sharkk | 52 | 近身拳击 5；准备后冲刺 10、推玩家 1 格、自身下回合眩晕、冷却 3；单回合受伤达到 10 时强制扬尘后撤 |

策划图没有给出 boom 的血量、攻击伤害、真正的自爆伤害/自伤机制和部分随机概率，因此这里使用可调试默认值 `7 HP / 3 伤害`，且不擅自实现死亡或自伤；这些数值全部暴露在 Inspector。Rocky、Sharkk 的血量同样可以直接改资源，不写死在程序里。

Sharkk 的扬尘 Buff 资源使用 `duration = 2`：现有战斗流程会在玩家回合刚开始时先执行一次 Buff Tick，如果写成 1，会在玩家获得操作权之前立即过期；写成 2 才等价于策划语义中的“影响下一个玩家回合”。

## 4. 距离、移动与冷却口径

### 距离

距离使用一维格子的绝对差：

```text
distance = abs(enemy_cell - player_cell)
```

例如玩家在 3、敌人在 5，距离为 2。左右方向完全对称。

移动遵守四条规则：

1. 有效格编号是 `1 ... Cell Count`。
2. 普通前进不能穿过或停在玩家所在格。
3. 后退到边界时按实际可移动格数截断，不会越界。
4. Sharkk 冲刺需要玩家身后还有一格可用于击退；空间不足时，冲刺不会进入候选意图。

### 冷却

冷却 `N` 表示：动作使用后，接下来的 `N` 个敌方决策回合不能再次使用。冷却只会递减到 0，不会出现负数。

例：Rocky 第 1 回合使用“钻头重击（冷却 2）”，第 2、3 回合不可使用，第 4 回合重新可用。

## 5. 意图数据流

```text
玩家每次行动 / 位置变化 / 敌人受伤
    ↓
EnermyMannager 读取最终状态
（双方位置、HP、盾、本回合受伤、冷却、冲刺准备、眩晕）
    ↓
refresh_intent_preview()
    ↓
相同状态指纹直接复用同一结果，不重新随机
    ↓
玩家结束回合
    ↓
lock_intent()
    ↓
execute_locked_intent()
    ↓
结算伤害 / 移动 / 护盾 / 扬尘 / 冷却，并更新下一意图
```

常用公共接口：

- `refresh_intent_preview() -> Dictionary`：刷新并返回预览意图。
- `lock_intent() -> Dictionary`：锁定当前意图。
- `execute_locked_intent() -> Dictionary`：执行锁定意图。
- `take_damage(amount)`、`add_shield(amount)`、`heal(amount)`：管理敌人血量和盾。
- `notify_player_action(context = {})`：玩家行动后通知 AI 重算。
- `get_state() -> Dictionary`：调试时读取完整运行状态。
- `get_cooldown(action_id) -> int`：读取指定动作剩余冷却。
- `configure_manual_state(...)`、`set_debug_snapshot(...)`：脱离战斗场景时人工构造状态。

意图字典包含动作 ID、说明、伤害、距离、双方起止格、冷却、扬尘目标格、是否强制触发和选择原因，UI 可直接订阅 `intent_changed` / `intent_locked` / `intent_executed` 信号。

## 6. Headless 自动测试

测试场景：

- `res://features/enemy_ai/tests/enemy_ai_headless_test.tscn`

Godot 4.6 Mono 命令行运行：

```powershell
Godot_v4.6.1-stable_mono_win64_console.exe --headless --path D:\Godot\Pipeline\pipeline --scene res://features/enemy_ai/tests/enemy_ai_headless_test.tscn
```

看到 `ENEMY_AI_TESTS: PASS` 且进程退出码为 `0` 表示通过；失败会列出具体断言并以退出码 `1` 结束。测试覆盖血量/盾、左右距离、边界、冷却、稳定随机、三套 AI 关键动作、冲刺空间检查、多个管理器实例隔离，以及至少 1000 步随机压力测试。

## 7. 集成边界

新的 Manager 会在无侵入 battle preset 中自动寻找原战斗场景的 `BattleManager`、`PlayerBattle`、`BoardManager` 和原敌人实例，监听玩家行动与回合信号，并将自身状态同步到原敌人节点。旧敌人数据在这三套 preset 中不配置旧动作，避免同一敌方回合被旧系统和新系统重复结算。

如果以后要把其中一套 AI 设为正式主场景，只需让启动流程进入对应 `battle_presets/*.tscn`；无需覆盖或删除原 `game.tscn`。
