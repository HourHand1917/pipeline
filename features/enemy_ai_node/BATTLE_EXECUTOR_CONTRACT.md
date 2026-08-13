# Enemy AI ↔ Battle Executor contract

The AI layer only selects and telegraphs actions. It does not mutate combatants.
`BattleManager` must cache one plan per living enemy after every confirmed player
state change. UI and `DangerAreaPredictor` read that cache; they must never call
`select_action()` independently.

## Required execution context

Every enemy action must execute with an explicit **acting EnemyBattle**. Effects
whose target is `ENEMY` default to that actor, never `GetPrimaryEnemy()`.
Movement checks the player, map bounds, and every other living enemy. It stops at
the last legal cell; it never overlaps or crosses another combatant.

`EnemyDataNodeAdapter.instantiate_ai_provider()` is the per-combatant factory.
An `EnemyBattle` should own that provider and its context/cache. Do not keep
runtime cooldown/state solely in the shared `.tres` Resource when one EnemyData
can appear more than once.

The executor reads these `EnemyActionData` fields:

- `cooldown_turns`: the following N enemy turns cannot select this action.
- `special_effect`: semantic runtime hook listed below.
- `effect_target_role`: `self`, `player`, `true_hand`, `false_hand`, or `body`.
- `special_value`: integer argument for the semantic hook.
- `special_replaces_effects`: when true, do not also replay the legacy array.
- `danger_profile`: read-only telegraph metadata.

## Special effects

| Key | Runtime behavior |
| --- | --- |
| `wait` | Successful no-op. The action is still confirmed so AI state advances. |
| `push_player_to_edge` | After charge movement/damage, push the player in the charge direction to the last legal unoccupied cell before the wall. |
| `move_behind_player` | Place actor at the nearest legal cell behind player and update both facings. |
| `add_all_card_cooldown` | Add `special_value` to every placed runtime card's remaining cooldown. |
| `heal_specific_enemy` | Heal the living enemy whose `role == effect_target_role` by `special_value`. |
| `set_death_loop` | Set encounter state `true_death_loop_active = true`; while active, True hand cannot remain dead below 1 HP. |
| `clear_true_death_loop` | Clear that encounter state after the break beam actually hits. |

`move_behind_player` augments the action: skip its legacy movement effect, perform
the teleport, then execute non-movement effects such as its 4 shield. Death-loop
and break-loop hooks augment their shield/damage effects. Hooks marked
`special_replaces_effects = true` replace the whole legacy array; this prevents
charge damage, cross-hand healing, and card jam from being applied twice.

## Sharkk confirmed-state order

`PREPARED` guarantees `sharkk_charge` once legal. Confirming charge enters
`RECOVERING`; the next confirmed action must be `sharkk_stunned` (`wait`), then
returns to `NEUTRAL`. Preview reads never change these states.

## Core-00 encounter transition

Phase one contains fixed True/False hands at cells 1/12, 21 HP each. When both
are defeated, suppress whole-battle victory and spawn the 50 HP body wave in the
same battle. Preserve player HP, position, build, card lights, and cooldowns; reset
only enemy-round choreography. Body defeat ends the encounter.
