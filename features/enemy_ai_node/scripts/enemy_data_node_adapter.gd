extends EnemyData
class_name EnemyDataNodeAdapter

## Drop one of the four AI PackedScenes here.  The inherited `actions` array is
## intentionally left unused: selection now lives in the configurable Node.
@export_group("Node AI Provider")
@export var ai_scene: PackedScene
@export var provider_entry: StringName = &"select_action"
@export var role: StringName = &""

var _provider: Node
var _runtime_context: Dictionary = {}
var _cached_signature: int = -1
var _cached_action: EnemyActionData


func set_ai_runtime_context(values: Dictionary) -> void:
	_runtime_context.merge(values, true)


func clear_ai_runtime_context() -> void:
	_runtime_context.clear()
	_cached_signature = -1
	_cached_action = null


func reset_ai_provider() -> void:
	if _provider != null and _provider.has_method("reset_ai"):
		_provider.call("reset_ai")
	_cached_signature = -1
	_cached_action = null


func get_ai_provider() -> Node:
	_ensure_provider()
	return _provider


func get_action_for_distance(current_distance: int) -> EnemyActionData:
	_ensure_provider()
	if _provider != null and _provider.has_method(provider_entry):
		var context := EnemyAIContext.new()
		context.update_from_dictionary(_runtime_context)
		context.enemy_id = id
		context.role = role
		context.enemy_max_hp = max_hp
		if not _runtime_context.has("distance"):
			context.distance = current_distance
		# Core-00 body resources are phase two by construction.  A bridge may
		# still override this explicitly through `set_ai_runtime_context`.
		if role == &"body" and not _runtime_context.has("phase"):
			context.phase = 2
		if context.enemy_hp <= 0:
			context.enemy_hp = max_hp
		var signature := context.decision_signature()
		if signature != _cached_signature:
			var selected: Variant = _provider.call(provider_entry, context)
			_cached_action = selected as EnemyActionData
			_cached_signature = signature
		# The UI and BattleManager both call this method.  Only the call made
		# during EnemyTurn commits cooldown/state; preview calls stay read-only.
		if context.battle_phase == 2 and _provider.has_method("confirm_action"):
			_provider.call("confirm_action", _cached_action, context)
		if _cached_action != null:
			return _cached_action
	# Safe compatibility fallback for a missing/misconfigured node.
	var best: EnemyActionData
	for action in actions:
		if action != null and action.is_available(current_distance):
			if best == null or action.priority > best.priority:
				best = action
	return best


func _ensure_provider() -> void:
	if _provider != null or ai_scene == null:
		return
	_provider = ai_scene.instantiate()
	if _provider != null and _provider.has_method("reset_ai"):
		_provider.call("reset_ai")
