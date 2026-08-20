extends EnemyAIController
class_name AdaptiveEnemyAIController

## Public-information-only weighted AI. Runtime decisions never read the
## player's hand, deck, backpack, weapon id, upgrade flag or selected command.

@export_group("Observable board")
@export_range(1, 99, 1) var board_min_cell: int = 1
@export_range(1, 99, 1) var board_max_cell: int = 9
@export var blocked_cells: PackedInt32Array = PackedInt32Array()

@export_group("Battle randomness")
## Production presets randomize once when a new provider is created/reset.
## UI previews inside that battle remain stable.
@export var randomize_battle_seed: bool = true
@export var fixed_test_seed: int = 20260820

@export_group("Adaptive memory")
@export_range(1, 12, 1) var observable_history_size: int = 6
@export_range(0.05, 1.0, 0.05) var ewma_alpha: float = 0.40
@export_range(0.0, 0.45, 0.01) var random_jitter: float = 0.15
@export_range(0.0, 1.0, 0.01) var immediate_repeat_multiplier: float = 0.35
@export_range(0.0, 1.0, 0.01) var double_repeat_multiplier: float = 0.12
@export_range(0.0, 1.0, 0.01) var recent_family_multiplier: float = 0.55
@export_range(0.50, 0.95, 0.01) var probability_cap: float = 0.85

var _observable_history: Array[Dictionary] = []
var _last_observed_round: int = -1
var _last_confirmed_distance: int = -1
var _active_battle_seed: int = 20260820


func reset_ai() -> void:
	super.reset_ai()
	_observable_history.clear()
	_last_observed_round = -1
	_last_confirmed_distance = -1
	if randomize_battle_seed:
		_active_battle_seed = int(Time.get_ticks_usec()) ^ int(get_instance_id()) ^ int(randi())
	else:
		_active_battle_seed = fixed_test_seed


## Test/simulation hook. Production scenes leave randomize_battle_seed on.
func lock_test_seed(seed: int) -> void:
	randomize_battle_seed = false
	fixed_test_seed = seed
	reset_ai()


func battle_seed_snapshot() -> int:
	return _active_battle_seed


func confirm_observable_history(context: EnemyAIContext) -> void:
	if context == null or _last_observed_round == context.round_number:
		return
	var observed_distance := int(context.metadata.get("damage_distance", context.distance))
	var movement := 0
	if _last_confirmed_distance >= 0:
		movement = signi(context.distance - _last_confirmed_distance)
	_observable_history.push_back({
		"round": context.round_number,
		"damage": maxi(0, context.damage_taken_last_turn),
		"distance": maxi(0, observed_distance),
		"movement": movement,
		"action_type": StringName(context.last_player_action_type),
	})
	while _observable_history.size() > observable_history_size:
		_observable_history.pop_front()
	_last_observed_round = context.round_number
	_last_confirmed_distance = context.distance


func observable_metrics(context: EnemyAIContext) -> Dictionary:
	var samples: Array[Dictionary] = _observable_history.duplicate(true)
	if context != null and context.round_number != _last_observed_round:
		var movement := 0
		if _last_confirmed_distance >= 0:
			movement = signi(context.distance - _last_confirmed_distance)
		samples.push_back({
			"round": context.round_number,
			"damage": maxi(0, context.damage_taken_last_turn),
			"distance": maxi(0, int(context.metadata.get("damage_distance", context.distance))),
			"movement": movement,
			"action_type": StringName(context.last_player_action_type),
		})
	while samples.size() > observable_history_size:
		samples.pop_front()
	var result := {
		"close_damage": 0.0, "mid_damage": 0.0, "far_damage": 0.0,
		"burst": 0.0, "approach_rate": 0.0, "retreat_rate": 0.0,
		"hold_rate": 0.0, "ranged_rate": 0.0, "melee_rate": 0.0,
		"sample_count": samples.size(),
	}
	if samples.is_empty():
		return result
	var approach_count := 0
	var retreat_count := 0
	var hold_count := 0
	var ranged_count := 0
	var melee_count := 0
	for sample in samples:
		var damage := float(sample.get("damage", 0))
		var distance := int(sample.get("distance", 0))
		result.burst = lerpf(float(result.burst), damage, ewma_alpha)
		result.close_damage = lerpf(float(result.close_damage), damage if distance == 1 else 0.0, ewma_alpha)
		result.mid_damage = lerpf(float(result.mid_damage), damage if distance >= 2 and distance <= 4 else 0.0, ewma_alpha)
		result.far_damage = lerpf(float(result.far_damage), damage if distance >= 5 else 0.0, ewma_alpha)
		match int(sample.get("movement", 0)):
			-1: approach_count += 1
			1: retreat_count += 1
			_: hold_count += 1
		var action_type := StringName(sample.get("action_type", &""))
		if action_type in [&"ranged", &"gun"]:
			ranged_count += 1
		elif action_type in [&"melee", &"fist"]:
			melee_count += 1
	var count := float(samples.size())
	result.approach_rate = float(approach_count) / count
	result.retreat_rate = float(retreat_count) / count
	result.hold_rate = float(hold_count) / count
	result.ranged_rate = float(ranged_count) / count
	result.melee_rate = float(melee_count) / count
	return result


func adaptive_weighted_ids(context: EnemyAIContext, raw_weights: Dictionary) -> EnemyActionData:
	var entries: Array[Dictionary] = []
	var ids: Array[StringName] = []
	for key in raw_weights:
		ids.append(StringName(key))
	ids.sort()
	var local_rng := RandomNumberGenerator.new()
	local_rng.seed = _observable_seed(context)
	for action_id in ids:
		var action := action_by_id(action_id)
		if action == null or not action.is_available(context.distance) or not is_ready(action.id):
			continue
		var weight := maxf(0.0, float(raw_weights.get(action_id, 0.0)))
		if weight <= 0.0:
			continue
		weight *= _repeat_multiplier(action)
		weight *= local_rng.randf_range(1.0 - random_jitter, 1.0 + random_jitter)
		entries.append({"action": action, "weight": maxf(0.0001, weight)})
	if entries.is_empty():
		return null
	if entries.size() == 1:
		return entries[0].action as EnemyActionData
	_cap_entry_probability(entries)
	var total := 0.0
	for entry in entries:
		total += float(entry.weight)
	var roll := local_rng.randf_range(0.0, total)
	var cursor := 0.0
	for entry in entries:
		cursor += float(entry.weight)
		if roll <= cursor:
			return entry.action as EnemyActionData
	return entries.back().action as EnemyActionData


func can_move_toward(context: EnemyAIContext, amount: int) -> bool:
	return _can_move_exact(context.enemy_position, context.player_position, amount, true)


func can_move_away(context: EnemyAIContext, amount: int) -> bool:
	return _can_move_exact(context.enemy_position, context.player_position, amount, false)


func charge_fifth_cell(context: EnemyAIContext) -> int:
	var direction := signi(context.player_position - context.enemy_position)
	return context.enemy_position if direction == 0 else context.enemy_position + direction * 5


func is_exact_fifth_cell_charge_legal(context: EnemyAIContext) -> bool:
	if context.distance < 1 or context.distance > 4:
		return false
	var direction := signi(context.player_position - context.enemy_position)
	if direction == 0:
		return false
	var destination := charge_fifth_cell(context)
	if destination < board_min_cell or destination > board_max_cell or blocked_cells.has(destination):
		return false
	for offset in range(1, 6):
		var cell := context.enemy_position + direction * offset
		if cell < board_min_cell or cell > board_max_cell:
			return false
		if blocked_cells.has(cell) and cell != context.player_position:
			return false
	return true


func history_snapshot() -> Array[Dictionary]:
	return _observable_history.duplicate(true)


func _can_move_exact(actor_position: int, player_position: int, amount: int, toward: bool) -> bool:
	if amount <= 0:
		return false
	var direction := signi(player_position - actor_position)
	if not toward:
		direction *= -1
	if direction == 0:
		return false
	var current := actor_position
	for _step in range(amount):
		var candidate := current + direction
		if candidate < board_min_cell or candidate > board_max_cell:
			return false
		if candidate == player_position or blocked_cells.has(candidate):
			return false
		current = candidate
	return true


func _observable_seed(context: EnemyAIContext) -> int:
	var public_history: Array = []
	for sample in _observable_history:
		public_history.append([int(sample.get("round", 0)), int(sample.get("damage", 0)), int(sample.get("distance", 0)), int(sample.get("movement", 0)), StringName(sample.get("action_type", &""))])
	return hash([_active_battle_seed, context.decision_signature(), public_history, _recent_actions])


func _repeat_multiplier(action: EnemyActionData) -> float:
	if _recent_actions.is_empty():
		return 1.0
	if _recent_actions[0] == action.id:
		if _recent_actions.size() >= 2 and _recent_actions[1] == action.id:
			return double_repeat_multiplier
		return immediate_repeat_multiplier
	var multiplier := 1.0
	if _recent_actions.has(action.id):
		multiplier *= recent_repeat_penalty
	var family := _action_family(action)
	if family != &"":
		var recent_action := action_by_id(_recent_actions[0])
		if recent_action != null and _action_family(recent_action) == family:
			multiplier *= recent_family_multiplier
	return multiplier


func _action_family(action: EnemyActionData) -> StringName:
	for family in [&"attack", &"movement", &"defence", &"debuff", &"stun"]:
		if action.tags.has(String(family)):
			return family
	return &""


func _cap_entry_probability(entries: Array[Dictionary]) -> void:
	var max_index := 0
	var max_weight := -1.0
	var other_total := 0.0
	for index in range(entries.size()):
		var weight := float(entries[index].weight)
		if weight > max_weight:
			if max_weight >= 0.0:
				other_total += max_weight
			max_weight = weight
			max_index = index
		else:
			other_total += weight
	if other_total <= 0.0:
		return
	var cap_weight := probability_cap * other_total / maxf(0.0001, 1.0 - probability_cap)
	if max_weight > cap_weight:
		entries[max_index].weight = cap_weight
