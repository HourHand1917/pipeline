# MVP 道具目录

`DataManager.EnsureMvpCatalogLoaded()` 会幂等注册全部 8 种道具，并按 `mvp_stock` 建立完整仓库；战斗携带栏仍限制为 4 格。调用 `EquipItem(id)` 从仓库挑选，调用 `UnequipItem(index)` 放回。

| ID | 道具 | 数量 | 价格 | 执行方式 |
|---|---|---:|---:|---|
| universal_toolkit | 万能工具包 | 1 | 18 | special：全冷却归零 + 清全部 Debuff |
| emergency_battery | 应急电池 | 2 | 8 | 标准 effect：能量 +2 |
| power_sunglasses | 强化墨镜 | 2 | 10 | special：本回合力量 +3 |
| teleport_insoles | 瞬移鞋垫 | 2 | 10 | 标准移动 + special 跨越/翻转 |
| bandage | 绷带 | 2 | 8 | 标准 effect：治疗 6 |
| blast_plate | 防爆板 | 2 | 9 | 标准 effect：护盾 8 |
| cooldown_spray | 冷却喷雾 | 2 | 6 | special：选择一卡冷却归零 |
| smoke_grenade | 烟雾弹 | 1 | 14 | special：取消锁定攻击；Boss 激光免疫 |

每个 special 道具的 `.tres` 里都带 `runtime_contract`，这是 `BattleManager` / `EffectResolver` 应执行的明确契约。
