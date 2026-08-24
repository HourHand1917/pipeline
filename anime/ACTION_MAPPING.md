# 战斗 Action 与动画名对照

该表用于动画师、策划和程序员核对动作命名。左侧为现有战斗资源 ID，右侧为 `SpriteFrames` 中应使用的动画名。

## 玩家 Rubber

| 卡牌 ID | 动画名 |
|---|---|
| `knuckle_striker`、`hydraulic_fist`、`scrap_fist` | `attack` |
| `deadly_kiss`、`pea_gun`、`quad_coil_gun`、`simple_cannon` | `attack` |
| `rocket_fist` | `attack` |
| `armored_shield`、`tactical_armor`、`hemostatic_pump` | `buff` |
| `mechanical_shoes`、`military_power_pack`、`scrap_battery` | `buff` |

当前玩家源美术只有待机、前进和后撤。为了让战斗反馈立即可用，`attack` 暂时复用前进帧，`hurt` 暂时复用后撤帧，`buff` 暂时复用加速的非循环待机帧；以后换入同名正式素材时无需改战斗逻辑。玩家素材原始朝左，Profile 已统一修正源朝向。

## Boom

| Action ID | 动画名 |
|---|---|
| `boom_advance_1`、`boom_advance_2` | `move_forward` |
| `boom_attack` | `attack` |
| `trained_boom_advance_1`、`trained_boom_advance_2` | `move_forward` |
| `trained_boom_attack_1`、`trained_boom_attack_2` | `attack` |

通用状态：`idle`、`hurt`、`death`。

## Rocky

| Action ID | 动画名 |
|---|---|
| `rocky_advance_1/2`、`trained_rocky_advance_1/2` | `move_forward` |
| `rocky_mid_attack`、`trained_rocky_mid_attack` | `attack_mid` |
| `rocky_close_attack`、`trained_rocky_close_attack` | `attack_close` |
| `rocky_defend`、`trained_rocky_defend` | `defend` |
| `rocky_retreat`、`trained_rocky_retreat` | `retreat` |

通用状态：`idle`、`hurt`、`death`。

## Sharkk

| Action ID | 动画名 |
|---|---|
| `sharkk_advance`、`trained_sharkk_advance` | `move_forward` |
| `sharkk_attack`、`sharkk_punch`、`trained_sharkk_attack` | `attack` |
| `sharkk_prepare_charge`、`trained_sharkk_prepare_charge` | `charge_prepare` |
| `sharkk_charge`、`trained_sharkk_charge_d1..d4` | `charge` |
| `sharkk_sand_retreat`、`trained_sharkk_sand_retreat` | 串联 `attack` → `move_backward` |
| `sharkk_stunned`、`trained_sharkk_stunned` | `stunned` |

通用状态：`idle`、`hurt`、`death`。

## Core-00 第一阶段 True 手

| Action ID | 动画名 |
|---|---|
| `core_true_guard_beam` | `finger_flick` |
| `core_true_guard_only`、`core_true_death_loop`、`core_true_charge` | `heavy_punch` |
| `core_true_send_heal` | `heal_snap` |

入场：`enter_left`；待机：`idle_1`；通用状态：`hurt`、`death`。

## Core-00 第一阶段 False 手

| Action ID | 动画名 |
|---|---|
| `core_false_charge`、`core_false_charge_complete` | `heavy_punch` |
| `core_false_break_beam`、`core_false_break_only`、`core_false_stun` | `finger_flick` |
| `core_false_heal` | `heal_snap` |

入场：`enter_right`；待机：`idle_2`；通用状态：`hurt`、`death`。

## Core-00 第二阶段本体

| Action ID | 动画名 |
|---|---|
| `core_body_advance_1/2/3` | `move_forward` |
| `core_body_sniper` | `sniper` |
| `core_body_gunstock` | `attack_close` |
| `core_body_pulse` | `pulse_3` |
| `core_body_teleport_guard` | `jam_cast → move_backward` |
| `core_body_jam` | `jam_cast`（负面效果使用攻击动画） |
| `core_body_reposition` | `retreat` |
| `trained_core_body_advance_1/2/3` | `move_forward` |
| `trained_core_body_sniper` | `sniper` |
| `trained_core_body_gunstock` | `attack_close` |
| `trained_core_body_pulse` | `pulse_3` |
| `trained_core_body_teleport_guard` | `jam_cast → move_backward` |
| `trained_core_body_disable` | `jam_cast`（负面效果使用攻击动画） |

通用状态：`idle`、`hurt`、`death`。
