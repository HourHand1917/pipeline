# Pipeline2 正式卡牌字典

所有已经完成数值设计的卡牌、升级版及效果资源统一放在
`res://Resource/card/`，直接使用项目现有：

- `res://Script/GD/resource/card_data.gd`
- `res://Script/GD/resource/combat_effect_data.gd`

## 目录

- `cards/`：14 张基础 `CardData`。
- `upgrades/`：与基础卡一一对应的 14 张升级版。
- `effects/`：17 个基础效果资源。
- `upgrade_effects/`：15 个升级效果资源；未改动的复合效果可复用基础效果。
- `card_catalog.gd`：生产卡牌的唯一字典。
- `tests/`：检查 14 组、28 个版本的路径、升级关系、数值和效果链。

## 字典接口

```gdscript
PipelineCardCatalog.load_all()                 # 14 张基础卡
PipelineCardCatalog.load_upgrades()            # 14 张升级卡
PipelineCardCatalog.load_all_versions()        # 全部 28 个版本
PipelineCardCatalog.load_by_id(&"deadly_kiss_up")
PipelineCardCatalog.load_upgrade_for(&"deadly_kiss")
```

`CARD_PATHS` 保留为 `BASE_CARD_PATHS` 的兼容别名，旧调用者仍然只会获得基础卡。
每张基础卡的 `upgraded_version` 已直接指向对应的 `upgrades/*.tres`，升级卡则设置
`is_upgraded = true`。

根目录中原先由剧情、商店直接引用的原型卡仍保持原路径；其中部分升级资源是空壳，
因此不会混入这份生产字典，避免把不可执行的卡牌交给玩家。

## 复合效果的执行顺序

现有 `EffectResolver` 按 `CardData.effects` 的数组顺序结算。本配置明确保证：

1. 火箭冲拳：先让玩家前进 1 格，再造成 4 点伤害。
2. 致命亲亲：依次结算两次 5 点伤害。
3. 简易火炮：先将敌人击退 1 格，再造成 5 点伤害。

## 14 组正式卡牌

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

每组均有同名 `_up` 升级版。测试还会特别验证容易断链的两张卡：

- `deadly_kiss_up`：两个独立效果，各造成 6 点伤害（6 + 6）。
- `hemostatic_pump_up`：恢复 8 点生命。

## 无头测试

```powershell
Godot_v4.6.1-stable_mono_win64_console.exe --headless --path D:\Godot\Pipeline2\pipeline --scene res://Resource/card/tests/card_config_test.tscn
```

通过标记：

```text
CARD_CONFIG_TEST PASS (19780 checks, 100 rounds, 14 cards, 17 effects)
```
