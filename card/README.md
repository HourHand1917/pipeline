# Pipeline2 卡牌配置（独立新增）

本目录只新增资源，不修改项目原有框架。所有卡牌直接使用：

- `res://Script/GD/resource/card_data.gd`
- `res://Script/GD/resource/combat_effect_data.gd`

## 目录

- `cards/`：14 张可直接拖入 Inspector、背包或 DataManager 的 `CardData` 资源。
- `effects/`：17 个独立 `CombatEffectData` 资源。
- `card_catalog.gd`：可选的只读目录；`PipelineCardCatalog.load_all()` 加载全部卡牌，`load_by_id()` 按 ID 加载。
- `tests/`：纯 GDScript 无头测试，重复 100 轮检查数值、距离、冷却、效果目标及复合效果顺序。

## 复合效果的执行顺序

现有 `EffectResolver` 按 `CardData.effects` 的数组顺序结算。本配置明确保证：

1. 火箭冲拳：先让玩家前进 1 格，再造成 4 点伤害。
2. 致命亲亲：依次结算两次 5 点伤害。
3. 简易火炮：先将敌人击退 1 格，再造成 5 点伤害。

## 14 张卡牌

| ID | 名称 | 格数 | 使用距离 | 效果 | 冷却 |
|---|---|---:|---:|---|---:|
| `knuckle_striker` | 指节撞针 | 1 | 1 | 3 伤害 | 1 |
| `scrap_fist` | 废铁拳 | 2 | 2–3 | 5 伤害 | 2 |
| `hydraulic_fist` | 液压重拳 | 2 | 1 | 9 伤害 | 3 |
| `rocket_fist` | 火箭冲拳 | 2 | 2–3 | 前进 1，再造成 4 伤害 | 2 |
| `quad_coil_gun` | 四握式线圈铳 | 4 | 4–6 | 20 伤害 | 3 |
| `pea_gun` | 豆豆枪 | 2 | 2–4 | 4 伤害 | 1 |
| `deadly_kiss` | 致命亲亲 | 3 | 2–3 | 5×2 伤害 | 2 |
| `simple_cannon` | 简易火炮 | 3 | 1–3 | 击退 1，再造成 5 伤害 | 3 |
| `military_power_pack` | 军用动力匣 | 2 | 自身 | 恢复 3 能量 | 2 |
| `scrap_battery` | 废旧电池 | 1 | 自身 | 恢复 1 能量 | 2 |
| `hemostatic_pump` | 止血泵 | 2 | 自身 | 恢复 4 生命 | 99 |
| `mechanical_shoes` | 机械鞋 | 1 | 自身 | 前进 1 格 | 1 |
| `tactical_armor` | 战术护甲 | 1 | 自身 | 获得 4 护盾 | 2 |
| `armored_shield` | 装甲盾 | 2 | 自身 | 获得 7 护盾 | 2 |

## 无头测试

```powershell
Godot_v4.6.1-stable_win64_console.exe --headless --path D:\Godot\Pipeline2\pipeline --scene res://card/tests/card_config_test.tscn
```
