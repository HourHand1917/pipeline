extends Node
class_name PipelineBuffHost

## Drag a PlayerBattle / EnemyBattle node into actor. If that actor has no
## Stats instance yet, this host creates one and injects it with SetStats().

@export var host_id: StringName = &"host"
@export var actor: Node
@export var board_manager: Node
@export var create_stats_if_missing: bool = true

var _standalone_stats: Stats


func _ready() -> void:
	ensure_stats()


func bind(new_actor: Node, new_board_manager: Node = null) -> void:
	actor = new_actor
	if new_board_manager != null:
		board_manager = new_board_manager
	ensure_stats()


func ensure_stats() -> Stats:
	var existing := get_stats()
	if existing != null:
		return existing
	if not create_stats_if_missing:
		return null
	_standalone_stats = Stats.new()
	if actor != null and actor.has_method("SetStats"):
		actor.call("SetStats", _standalone_stats)
	return _standalone_stats


func get_stats() -> Stats:
	if actor != null and actor.has_method("GetStats"):
		var value: Variant = actor.call("GetStats")
		if value is Stats:
			return value as Stats
	return _standalone_stats


func apply_damage(amount: int) -> bool:
	if actor != null and actor.has_method("TakeDamage"):
		actor.call("TakeDamage", maxi(0, amount))
		return true
	return false


func apply_heal(amount: int) -> bool:
	if actor != null and actor.has_method("Heal"):
		actor.call("Heal", maxi(0, amount))
		return true
	return false


func apply_shield(amount: int) -> bool:
	if actor != null and actor.has_method("AddShield"):
		actor.call("AddShield", maxi(0, amount))
		return true
	return false


func current_hp() -> int:
	if actor != null:
		var value: Variant = actor.get("CurrentHp")
		if value != null:
			return int(value)
	return -1
