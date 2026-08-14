# 设计稿 Buff 资产

本目录提供图片设计稿锁定的九种 Buff。每个具体脚本都**直接继承 `Buff`**，并至少重载一个 `buff_data.gd` 生命周期函数；用户提供的 `buff_data.gd`、`combat_effect_data.gd` 未被修改。

统一目录：

- 脚本：`res://features/buff_system/behaviors/`，蒙尘和禁用复用并扩展原有脚本。
- Buff 资源与原创 SVG：`res://features/buff_system/resources/buffs/`
- 挂载效果：`res://features/buff_system/resources/effects/`
- Catalog：`designed_buff_catalog.tres`

| ID | 名称 | 极性 | 结算 | 衰减 / 移除 |
|---|---|---|---|---|
| `strength` | 力量 | 正 | 每个伤害 Effect 增加层数 | 战斗结束统一清除 |
| `temporary_strength` | 临时力量 | 正 | 每个伤害 Effect 增加层数 | 持有者回合结束整体移除 |
| `dust` | 蒙尘 | 负 | 点亮附着格额外耗能等于层数 | 玩家回合结束减 1 层 |
| `deployed_medkit` | 部署治疗包 | 正 | 持有者下回合开始治疗等于层数 | 治疗结算后移除 |
| `disabled` | 禁用 | 负 | 玩家回合开始令全部卡牌至少冷却 1 回合 | 回合结束减 1 层 |
| `anneal` | 退火 | 正 | 施加时解除全部卡牌冷却 | 结算后立即移除 |
| `true` | True | 正 | 造成与受到伤害均 `floor(value / 2)` | 3 次回合结束 Tick；与 False 中和 |
| `false` | False | 负 | 伤害 `value + ceil(value / 2)`；回合开始能量 -1 | 3 次回合结束 Tick；与 True 中和 |
| `holographic` | 全息化 | 正 | 受到伤害固定为 1 | 下次玩家回合开始减 1 层并移除 |

## EffectResolver 契约

Buff 通过 `get_effects_for_phase(phase)` 暴露挂载的 `PipelineCombatEffectData`：

- `apply`：施加后立即执行。
- `turn_start` / `turn_end`：对应回合边界。
- `outgoing_damage` / `incoming_damage`：伤害修正阶段。

`get_decay_phase()` 决定衰减时点，`decay_uses_stacks()` 区分层数衰减与持续时间衰减。`consume_after_apply()` 和 `consume_after_phase()` 用于退火、部署治疗包等一次性 Buff。

True/False 以详细节点锁定的“三次回合结束 Tick”为准。False 同时保留总览备注中的“玩家回合开始失去 1 能量”。

## 正式战斗接入

- Core-00 True 手保护光束：仅在偶数格命中时施加 `true ×3`。
- Core-00 False 手破坏光束：仅在偶数格命中时施加 `false ×3`。
- Core-00 False 手晕眩：施加 `disabled ×1`，在下一玩家回合开始封锁卡牌。
- Core-00 True 手治疗包：回合末延迟挂到 False 手；False 手下回合开始回复 5 点。
- `dust` 继续由格子型敌方效果使用；力量、临时力量、退火与全息化由对应物品施加。

Core 双手到本体的连续阶段会保留角色 Buff；阶段二首个玩家回合会正常执行 `turn_start`，因此部署治疗包、False 能量扣减与全息化到期不会延后一回合。

## 验证

无头测试场景：

```powershell
godot --headless --path D:\Godot\Pipeline2\pipeline `
  --scene res://features/buff_system/tests/designed_buff_contract_test.tscn
```

成功标记：`DESIGNED_BUFF_CONTRACT_TEST_PASS`。

真实战斗集成场景：

```powershell
godot --headless --path D:\Godot\Pipeline2\pipeline `
  --scene res://features/buff_system/tests/buff_item_runtime_smoke.tscn
```

成功标记：`BUFF_ITEM_RUNTIME_SMOKE_PASS`。
