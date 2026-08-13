# Node-based enemy AI (local add-on)

This directory is additive: it does not replace or edit the original `EnemyData`, `BattleManager`, scenes, or resources.

## Designer workflow

1. Open one of `scenes/boom_ai.tscn`, `rocky_ai.tscn`, `sharkk_ai.tscn`, or `core00_ai.tscn`.
2. Select the root node. Its exported `Actions` array contains every `EnemyActionData` resource used by that AI.
3. Change probabilities/cooldowns/thresholds in the same Inspector.
4. Drag the scene into an `EnemyAIHost`, or assign it to an `EnemyDataNodeAdapter.ai_scene` slot.
5. Use the matching `.tres` under `enemy_data/` in a `BattleMapData.enemy_data_list`.

`EnemyDataNodeAdapter.get_action_for_distance()` preserves the exact method expected by the current C# `BattleManager`. It builds an `EnemyAIContext` and delegates the intent decision to the Node. `EnemyAINodeBridge` enriches that context with HP, shield, positions, player damage/action type, and round data without modifying the original framework.

The adapter caches one intent per state snapshot. UI inspection is read-only;
cooldown and prepared-charge state are committed only when the original
`BattleManager` enters `EnemyTurn`. Clicking the intent panel therefore cannot
reroll or consume the action that will actually execute.

## Configured enemies

- boom: 8 HP, approach/attack.
- rocky: 20 HP, mid/close attacks, advance, shield/retreat response to heavy damage.
- sharkk: 40 HP, attack-first melee AI, prepared charge, gun adaptation, forced sand retreat after 10+ damage.
- Core-00: two 21 HP hand resources with fixed five-turn choreography, plus a 50 HP dynamic ranged body.

## Integration note

The additive Main/Bridge scenes are ready to open directly and never edit the
original `game.tscn`:

- `main_enemy_ai_node.tscn`: Boom (8 HP)
- `main_rocky_boom.tscn`: Rocky 20 HP + Boom 8 HP
- `main_sharkk.tscn`: Sharkk (40 HP)
- `main_core00_phase_one.tscn`: True/False hands (21 HP each)
- `main_core00_phase_two.tscn`: body (50 HP)

The last two remain separate encounters because automatic two-hand-to-body
transition needs a battle lifecycle hook the original framework does not expose.

The existing C# combat loop only asks an `EnemyData` for one action. All actions here therefore remain ordinary `EnemyActionData` resources, so the existing `EffectResolver` executes them unchanged. Conditional boss mechanics such as "beam only on even cells", cross-hand healing, true death-loop persistence, exact teleport-to-behind, and disabling every card require callbacks that the original resolver currently does not expose. Their intent selection/configuration is present here; full runtime side effects should be wired by the programmer through `EnemyAINodeBridge.report_player_action()` or future resolver hooks.
