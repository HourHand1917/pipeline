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
- `pattern_parity`: `0` for even, `1` for odd, `-1` for no parity pattern.
- `fixed_target_cells`: optional absolute board cells, independent of facing.
- `pattern_hit_effects`: actor/player effects that run only on a pattern hit.
- `pattern_hit_role_effects`: hit-only effects aimed at `effect_target_role`.
- `danger_profile`: read-only telegraph metadata.

## Special effects

| Key | Runtime behavior |
| --- | --- |
| `wait` | Successful no-op. The action is still confirmed so AI state advances. |
| `push_player_to_edge` | After charge movement/damage, push the player in the charge direction to the last legal unoccupied cell before the wall. |
| `move_behind_player` | Place actor at the nearest legal cell behind player and update both facings. |
| `add_all_card_cooldown` | Add `special_value` to every placed runtime card's remaining cooldown. |
| `heal_specific_enemy` | Heal the living enemy whose `role == effect_target_role` by `special_value`. |
| `set_death_loop` | Legacy compatibility hook; current Core-00 phase one does not use HP locking. |
| `clear_true_death_loop` | Legacy compatibility hook paired with `set_death_loop`. |

`move_behind_player` augments the action: skip its legacy movement effect, perform
the teleport, then execute non-movement effects such as its 4 shield. Hooks marked
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

True locks a three-step chain: package shot (absolute cells 2–11, damage 5,
defer 12 healing stacks to False), wait, then the even-cell protection beam
(damage 7). A hit gives the player three True stacks, marks True internally,
and gives False 25 shield. While marked, True uses Death Loop for 15 shield;
the marker does not make it immortal.

False independently loops through cooldown, recovery, charge, a completed shot
(absolute cells 1–3, damage 11), and a break beam (absolute cells
1/3/5/7/9/11, damage 25). Both attacks apply three False stacks on hit; the
break beam also applies False to True, mutually cancelling its internal True.
If either hand dies, the survivor uses the passive wait action.

All living enemies lose carried shield together at the enemy-turn boundary.
Shield granted later during that same enemy turn is retained until the next one.
