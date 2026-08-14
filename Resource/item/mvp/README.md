# MVP 道具目录（策划图 10 件版）

`DataManager.EnsureMvpCatalogLoaded()` 会幂等注册下表 10 种道具，每种默认库存 1 件；
战斗携带栏仍限制为 4 格。策划图未给出价格，因此本批资源的 `shop_price` 统一为 0，
仅用于战斗配置验证，不代表正式商店售价。

每件道具都有独立的 `ItemData` 子脚本和原创 SVG 图标。运行时行为不写在物品脚本中：
物品只按顺序触发自身 `effects`，再由 `EffectResolver` 统一结算。

| ID | 道具 | 库存 | 挂载效果 | 结算结果 |
|---|---|---:|---|---|
| `coolant` | 冷却剂 | 1 | `APPLY_BUFF: anneal ×1` | 解除全部已构筑卡牌冷却 |
| `spare_battery` | 备用电池 | 1 | `ENERGY 2` | 玩家能量 +2 |
| `spinach_powerups` | 菠菜罐头 Power-Ups | 1 | `APPLY_BUFF: temporary_strength ×3` | 本回合力量 +3 |
| `gasoline` | 汽油 | 1 | `APPLY_BUFF: strength ×1` | 本场战斗力量 +1 |
| `roller_shoes` | 滑轮鞋 | 1 | 两条 `MOVE_PLAYER_FORWARD_ONE` | 分两次判定，各前进 1 格 |
| `grenade` | 手雷 | 1 | `DAMAGE_FARTHEST_ENEMY 5` | 无视距离伤害最远存活敌人 |
| `bulletproof_vest` | 防弹衣 | 1 | `SHIELD 7` | 玩家护盾 +7 |
| `particle_wall` | 粒子墙 | 1 | `APPLY_BUFF: holographic ×1` | 本回合每次受伤按 1 点结算 |
| `ice_cream` | 冰淇淋 | 1 | 无 | 暂无战斗作用且不会消耗 |
| `medkit` | 医疗箱 | 1 | `HEAL 5` | 玩家生命 +5，不超过上限 |

## 关键契约

- 滑轮鞋不是瞬移：两个独立 effect 依次重新检查地图边界和敌人占位。
- 手雷不是当前目标攻击：由 `DAMAGE_FARTHEST_ENEMY` 选择距玩家最远的存活敌人。
- 力量对每个 `damage` effect 分别增加层数；多段伤害会逐段获得加成。
- 冷却剂、两种力量与粒子墙均先通过 `APPLY_BUFF` 进入 Buff 系统。
- 冰淇淋保留策划图中的占位语义，`effects` 为空且 `consume_on_use=false`。
- 旧版 8 件道具资源仍保留在目录中供历史引用，但不再进入 MVP Catalog。
