# Core-00 全卡牌可玩战斗

这是一个完全独立、只新增文件的 1920×1080 战斗场景。它直接调用：

- `PipelineCardCatalogLoader.load_all_cards()` 加载 `card/` 中全部 14 张卡。
- `Core00EnermyMannager` 驱动 21HP True手、21HP False手与 50HP 二阶段本体。
- `CombatEffectData` 的数组顺序执行伤害、护盾、治疗、位移与亮格补充。

## 直接游玩

在 Godot 中打开并运行：

`res://features/core00_ai/card_battle/core00_card_battle.tscn`

操作流程：

1. 点击一张卡牌，再点“点亮1格”。每回合有2点亮格。
2. 卡牌的点亮进度跨回合保留；全部格子点亮才能使用。
3. 攻击卡必须先选择存活目标，并满足卡牌的严格距离。
4. 每回合可以免费向相邻格移动一次。
5. 点击“结束玩家回合”后，Core-00执行已显示的意图。

## 冷却的精确定义

卡牌写着 `CD N`，表示使用后会完整阻止接下来的 N 个玩家回合。新启动的冷却不会在使用当回合结束时被误减；例如 CD1：

`使用回合 -> 下个玩家回合不可用 -> 再下个玩家回合恢复`

Core-00 的“全部卡牌干扰”会在下一玩家回合开始时生效，不清空已有充能，也不会把原本更长的冷却缩短。

## 无界面测试

运行：

```powershell
Godot_v4.6.1-stable_mono_win64_console.exe --headless --path D:\Godot\Pipeline\pipeline --scene res://features/core00_ai/card_battle/core00_card_battle_test.tscn
```

测试覆盖：14卡加载、充能跨回合、射程拒绝、精确冷却、5×2多段伤害、先移动再攻击、双手转本体、全卡干扰与胜利结束。

## 供自动化/调试使用的 API

- `debug_reset(position, hp)`
- `debug_set_light_points(value)`
- `debug_set_card_charge(card_id, value)`
- `debug_set_player_position(position)`
- `charge_card(card_id, points)`
- `use_card(card_id, target_id)`
- `move_player_adjacent(direction)`
- `end_player_turn()`
- `debug_damage_target(target_id, amount)`
- `debug_force_phase_two()`
- `debug_queue_jam(turns)` / `debug_apply_pending_jam()`
- `get_battle_snapshot()`
