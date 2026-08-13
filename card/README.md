# Pipeline 卡牌配置（独立新增目录）

这里包含策划表中的全部 14 张卡。所有内容都在新建的 `card/` 下，原有 `Resource/card/`、`DataManager` 和战斗脚本没有被修改。

## 目录

- `cards/`：14 个 `CardData` 卡牌资源。
- `effects/`：每一步战斗效果。复合卡按数组顺序执行。
- `catalog/all_cards.tres`：完整卡牌目录。
- `scripts/`：独立目录类型和加载器。
- `tests/`：无界面配置测试。

## 加载方式

```gdscript
var all_cards: Array[CardData] = PipelineCardCatalogLoader.load_all_cards()
var rocket_punch: CardData = PipelineCardCatalogLoader.get_card(&"rocket_punch")
```

也可以直接拖入右侧 Inspector：`res://card/cards/*.tres`。现有 `EffectResolver` 会优先按 `effects` 数组从前到后执行，因此：

- 火箭冲拳：玩家前进 1 格，然后造成 4 伤害。
- 致命亲亲：分别结算两次 5 伤害。
- 简易火炮：先把敌人击退 1 格，然后造成 5 伤害。

## 配置表

| 卡牌 | ID | 格数 | 距离 | 效果 | 冷却 |
|---|---|---:|---:|---|---:|
| 指节撞针 | `finger_knuckle_striker` | 1 | 1 | 3伤害 | 1 |
| 废铁拳 | `scrap_fist` | 2 | 2–3 | 5伤害 | 2 |
| 液压重拳 | `hydraulic_heavy_fist` | 2 | 1 | 9伤害 | 3 |
| 火箭冲拳 | `rocket_punch` | 2 | 2–3 | 前进1，4伤害 | 2 |
| 四握式线圈铳 | `four_grip_coil_rifle` | 4 | 4–6 | 20伤害 | 3 |
| 豆豆枪 | `pea_shooter` | 2 | 2–4 | 4伤害 | 1 |
| 致命亲亲 | `deadly_kiss` | 3 | 2–3 | 5伤害×2 | 2 |
| 简易火炮 | `improvised_cannon` | 3 | 1–3 | 敌人后退1，5伤害 | 3 |
| 军用动力匣 | `military_power_cell` | 2 | 无 | 3能量 | 2 |
| 废旧电池 | `worn_battery` | 1 | 无 | 1能量 | 2 |
| 止血泵 | `hemostatic_pump` | 2 | 无 | 恢复4生命 | 99 |
| 机械鞋 | `mechanical_shoes` | 1 | 无 | 玩家前进1 | 1 |
| 战术护甲 | `tactical_armor` | 1 | 无 | 4护盾 | 2 |
| 装甲盾 | `armored_shield` | 2 | 无 | 7护盾 | 2 |

## 无界面测试

```powershell
Godot_v4.6.1-stable_win64_console.exe --headless --path D:\Godot\Pipeline\pipeline --scene res://card/tests/card_config_headless_test.tscn
```

测试会检查：资源可加载、数量与 ID 唯一、名称、格数、横向形状、距离、冷却，以及每一个效果的类型、目标、数值和执行顺序。

需要长时间重复验证时可传入轮数；测试会在每一轮重新走目录和按 ID 加载接口：

```powershell
Godot_v4.6.1-stable_win64_console.exe --headless --path D:\Godot\Pipeline\pipeline --scene res://card/tests/card_config_headless_test.tscn -- --stress-rounds=10000
```
