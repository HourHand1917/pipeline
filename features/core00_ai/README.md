# Core-00 独立敌人 AI

本目录只包含新增文件，不修改项目现有的单敌人战斗、卡牌或场景。Core-00 是双阶段、多目标 Boss，不能直接假装成旧系统里的一个普通敌人；因此这里先提供可实例化、可测试的独立管理器和清晰的接入信号。

## 直接预览

在 Godot 中打开并运行：

`res://features/core00_ai/scenes/core00_enermy_mannager.tscn`

右侧 Inspector 可调整 `Core00Profile` 中的全部数值。运行场景后，调试面板可执行回合、分别攻击两只手、强制进入二阶段和攻击本体。

要从头实际操作两阶段战斗，请运行：

`res://features/core00_ai/demo/core00_battle_demo.tscn`

演示支持相邻格移动（A/D 或方向键）、点击轨道格、选择 True/False/本体目标、选择测试伤害、结束回合（Space）、五步双意图显示、自动阶段切换、二阶段距离 AI、卡牌冷却干扰显示和重新开始。它同样完全独立，不引用旧 `BattleManager`。

## 一阶段：True / False 双手

- True 手：21 HP，固定在 1 号格。
- False 手：21 HP，固定在 12 号格。
- 玩家通常在中间 2～11 号格移动，共 10 个可移动格。
- 两只手执行固定的五步循环，意图不会因玩家伤害临时改写。

| 步骤 | True | False |
|---|---|---|
| 1 | 保护光束：玩家在偶数格时造成 6 伤害并标记 `true` | 蓄力并获得 4 格挡 |
| 2 | 进入死循环，获得 4 格挡；死循环期间生命不会低于 1 | 蓄力完成 |
| 3 | 维持死循环并获得 4 格挡 | 破坏光束：偶数格命中 6 伤害、标记 `false`，并解除 True 死循环 |
| 4 | 给 False 发送治疗包 | 晕眩 |
| 5 | 蓄力 | False 使用治疗包恢复 5 HP |

图中没有明确给出的光束伤害、格挡和治疗值采用了易调试的默认值：`6 / 4（上限8）/ 5`，都在 `profiles/core00_profile.tres` 的 Inspector 中可直接改。

只有两只手都死亡才进入二阶段。若 False 提前死亡，管理器会解除 True 的死循环，避免战斗永久卡死。

## 二阶段：50 HP 灵活远程本体

| 行动 | 距离 | 效果 | 冷却 |
|---|---:|---:|---:|
| 蓄力狙击 | 6～12 | 12 伤害 | 4 |
| 近身枪托 | 1 | 9 伤害 | 1 |
| 连发脉冲 | 3～4 | 2×3 伤害 | 2 |
| 前走 | 任意 | 向玩家靠近 1～3 格，优先进入距离 3 或 1 | 0 |
| 闪身 | 受伤达到 8 | 到玩家身后并获得 4 护盾 | 3 |
| 过载干扰 | 默认每第 4 回合 | 玩家下一回合所有卡至少进入冷却 1 | 3 |

优先级是：受到较大伤害时闪身反应 → 定期过载干扰 → 当前距离可用攻击 → 智能前走。闪身只响应本回合达到阈值的高伤害，不会在普通攻击冷却时无条件补盾，因此低伤卡组不会陷入“伤害小于补盾”的软锁。

闪身护盾默认累计上限为 12（`body_shield_cap`，可在 Inspector 调整），避免防守动作在极长战斗中无限叠加导致软锁。

## 接入现有战斗时使用的 API

- `take_damage(&"true_hand", amount)`
- `take_damage(&"false_hand", amount)`
- `take_damage(&"body", amount)`
- `notify_player_action(snapshot)`：玩家行动后更新位置/受伤快照并重新确认意图。
- `advance_enemy_turn()`：正式的完整敌方回合入口；锁定、执行、推进回合、清除反应伤害并刷新下一意图。
- `lock_intent()` / `execute_locked_intent()`：只在需要自行编排生命周期时分步调用；调用者随后必须自行完成回合推进。
- `begin_player_turn(runtime_cards)`：把待生效的全卡冷却施加到下一玩家回合。

关键输出信号：`player_damage_requested`、`card_jam_requested`、`intent_changed`、`phase_changed`、`boss_defeated`。旧战斗接入时只需由一个适配器响应这些信号，无需改本管理器内部 AI。

## 回归测试

```powershell
Godot_v4.6.1-stable_win64_console.exe --headless --path D:\Godot\Pipeline\pipeline --scene res://features/core00_ai/tests/core00_headless_test.tscn
```

测试覆盖：21/21 双手、50 HP 本体、阶段转换、奇偶格光束、死亡循环、护盾上限、治疗、全部二阶段距离和冷却、移动、闪身、卡牌干扰，以及 30 场随机压力测试。
