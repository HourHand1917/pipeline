extends Resource
class_name EnemyActionData

@export_group("Identity")
@export var id: StringName = &""
@export var display_name: String = ""
@export var intent_text: String = ""
@export var priority: int = 0

@export_group("Decision Tuning")
## Relative weight used by node-based AI.  Legacy priority selection remains
## available for old EnemyData resources.
@export_range(0.0, 999.0, 0.05, "or_greater") var base_weight: float = 1.0
## Multiplier applied when this was the immediately preceding action.
@export_range(0.0, 1.0, 0.01) var repeat_penalty: float = 0.18
## Number of following enemy turns during which this action is unavailable.
@export_range(0, 99, 1, "or_greater") var cooldown_turns: int = 0
@export var tags: PackedStringArray = PackedStringArray()

@export_group("Availability")
@export_range(0, 99, 1, "or_greater") var min_range: int = 2
@export_range(0, 99, 1, "or_greater") var max_range: int = 99

@export_group("Effects")
@export var effects: Array[CombatEffectData] = []
## Effects that are resolved only when a patterned attack (for example a
## Core-00 even-cell beam) actually hits its board condition.
@export var pattern_hit_effects: Array[CombatEffectData] = []
## -1 = no fixed board pattern, 0 = even cells, 1 = odd cells.
## A patterned action keeps the same intent when it misses; only its damage
## and conditional effects are skipped.
@export_range(-1, 1, 1) var pattern_parity: int = -1
## Optional absolute board cells. When populated, these cells replace the
## ordinary facing/range test (used by Core-00's fixed-position hands).
@export var fixed_target_cells: PackedInt32Array = PackedInt32Array()
## Conditional effects resolved against `effect_target_role` when the board
## pattern hits. This supports cross-enemy protection/buff interactions while
## keeping every number in a draggable resource.
@export var pattern_hit_role_effects: Array[CombatEffectData] = []
## Effects resolved against `effect_target_role` after every enemy has acted.
## This keeps cross-enemy packages deterministic and prevents them from
## triggering during the recipient's current turn-start window.
@export var deferred_role_effects: Array[CombatEffectData] = []

@export_group("Runtime Contract")
## Optional semantic hook consumed by the C# battle executor.  Ordinary
## actions leave this empty and execute their `effects` array unchanged.
@export var special_effect: StringName = &""
## `self`, `player`, or an encounter role such as `false_hand`.
@export var effect_target_role: StringName = &"self"
@export_range(0, 999, 1, "or_greater") var special_value: int = 0
## When true, the battle executor must not also replay the legacy effects.
@export var special_replaces_effects: bool = false
## Optional pure-data telegraph profile consumed by DangerAreaPredictor.
@export var danger_profile: Resource


func is_available(distance: int) -> bool:
	var range_min := mini(min_range, max_range)
	var range_max := maxi(min_range, max_range)
	return distance >= range_min and distance <= range_max
