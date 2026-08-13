extends Node
class_name EnemyAIController

## Inherited by all four enemy AI nodes.  This is the array designers edit in
## the Inspector after dragging an AI scene into an EnemyAIHost.
@export_group("Actions (designer editable)")
@export var actions: Array[EnemyActionData] = []

@export_group("Decision")
@export var deterministic_seed: int = 20260813
@export_range(1, 8, 1) var recent_memory_size: int = 3
@export_range(0.0, 1.0, 0.01) var recent_repeat_penalty: float = 0.55

var decision_turn: int = 0
var _cooldowns: Dictionary = {}
var _proposed_cooldowns: Dictionary = {}
var _last_cooldown_tick_round: int = -1
var _last_confirmed_round: int = -1
var _rng := RandomNumberGenerator.new()
var _recent_actions: Array[StringName] = []


func _ready() -> void:
	_rng.seed = deterministic_seed


func reset_ai() -> void:
	decision_turn = 0
	_cooldowns.clear()
	_proposed_cooldowns.clear()
	_last_cooldown_tick_round = -1
	_last_confirmed_round = -1
	_recent_actions.clear()
	_rng.seed = deterministic_seed


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	var candidates: Array[EnemyActionData] = []
	for action in actions:
		if action != null and action.is_available(context.distance) and is_ready(action.id):
			candidates.append(action)
	if candidates.is_empty():
		return null
	candidates.sort_custom(func(a: EnemyActionData, b: EnemyActionData) -> bool: return a.priority > b.priority)
	return commit_action(candidates[0])


func _begin_decision(context: EnemyAIContext) -> void:
	decision_turn += 1
	var current_round := maxi(1, context.round_number)
	if _last_cooldown_tick_round < 0:
		_last_cooldown_tick_round = current_round
		return
	if current_round <= _last_cooldown_tick_round:
		return
	var elapsed_rounds := current_round - _last_cooldown_tick_round
	for key in _cooldowns.keys():
		_cooldowns[key] = maxi(0, int(_cooldowns[key]) - elapsed_rounds)
	_last_cooldown_tick_round = current_round


func action_by_id(action_id: StringName) -> EnemyActionData:
	for action in actions:
		if action != null and action.id == action_id:
			return action
	return null


func first_available(ids: Array[StringName], context: EnemyAIContext) -> EnemyActionData:
	for action_id in ids:
		var action := action_by_id(action_id)
		if action != null and is_ready(action_id) and action.is_available(context.distance):
			return action
	return null


func is_ready(action_id: StringName) -> bool:
	return int(_cooldowns.get(action_id, 0)) <= 0


func commit_action(action: EnemyActionData, cooldown: int = -1) -> EnemyActionData:
	# Selection is a preview and must be side-effect free.  The adapter calls
	# `confirm_action` once, at EnemyTurn, after the final preview is locked.
	if action != null:
		var resolved_cooldown := action.cooldown_turns if cooldown < 0 else cooldown
		_proposed_cooldowns[action.id] = maxi(0, resolved_cooldown)
	return action


func confirm_action(action: EnemyActionData, context: EnemyAIContext) -> void:
	if action == null or _last_confirmed_round == context.round_number:
		return
	var cooldown := int(_proposed_cooldowns.get(action.id, 0))
	if cooldown > 0:
		# CD N means the following N enemy decisions cannot reuse the action.
		_cooldowns[action.id] = cooldown + 1
	_last_confirmed_round = context.round_number
	if has_method(&"_on_action_confirmed"):
		call(&"_on_action_confirmed", action, context)
	_recent_actions.push_front(action.id)
	while _recent_actions.size() > recent_memory_size:
		_recent_actions.pop_back()


func cooldown_remaining(action_id: StringName) -> int:
	return int(_cooldowns.get(action_id, 0))


func random_chance(chance: float) -> bool:
	return _rng.randf() < clampf(chance, 0.0, 1.0)


## `weighted_actions` entries are dictionaries with `action` and `weight`.
## Cooldown/range filtering happens here so every enemy follows the same rules.
func weighted_choice(context: EnemyAIContext, weighted_actions: Array[Dictionary]) -> EnemyActionData:
	var candidates: Array[Dictionary] = []
	var total_weight := 0.0
	for entry in weighted_actions:
		var action := entry.get("action") as EnemyActionData
		if action == null or not action.is_available(context.distance) or not is_ready(action.id):
			continue
		var weight := maxf(0.0, float(entry.get("weight", action.base_weight)))
		if not _recent_actions.is_empty() and _recent_actions[0] == action.id:
			weight *= action.repeat_penalty
		elif _recent_actions.has(action.id):
			weight *= recent_repeat_penalty
		if weight <= 0.0:
			continue
		candidates.append({"action": action, "weight": weight})
		total_weight += weight
	if candidates.is_empty() or total_weight <= 0.0:
		return null
	var roll := _rng.randf_range(0.0, total_weight)
	var cursor := 0.0
	for entry in candidates:
		cursor += float(entry.weight)
		if roll <= cursor:
			return entry.action as EnemyActionData
	return candidates.back().action as EnemyActionData


func weighted_ids(context: EnemyAIContext, weights: Dictionary) -> EnemyActionData:
	var candidates: Array[Dictionary] = []
	for action_id in weights:
		candidates.append({"action": action_by_id(StringName(action_id)), "weight": float(weights[action_id])})
	return weighted_choice(context, candidates)


func recent_actions() -> Array[StringName]:
	return _recent_actions.duplicate()
