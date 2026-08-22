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
- Core-00: fixed True/False hands with independent three-step/five-step state machines, plus a 50 HP dynamic ranged body.

## Integration note

The additive Main/Bridge scenes are ready to open directly and never edit the
original `game.tscn`:

- `main_enemy_ai_node.tscn`: Boom (8 HP)
- `main_rocky_boom.tscn`: Rocky 20 HP + Boom 8 HP
- `main_sharkk.tscn`: Sharkk (40 HP)
- `main_core00_phase_one.tscn`: True/False hands (21 HP each)
- `main_core00_phase_two.tscn`: body (50 HP)

The campaign controller joins the two Core presets into one encounter and
preserves player state across the hand-to-body transition.

Core fixed-cell attacks, hit-only Buffs, cross-hand package/shield effects,
teleport, card jam, and phase transition are connected through the current
`BattleManager`/`EffectResolver`. The UI reads each cached planned action; it
never asks the AI to select again. At execution, the adapter explicitly confirms
that locked action so turn-boundary shield expiry cannot cause a hidden reroll.
