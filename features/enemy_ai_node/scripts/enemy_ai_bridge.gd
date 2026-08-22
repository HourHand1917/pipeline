extends Node
class_name EnemyAINodeBridge

## Non-invasive bridge for the inherited Main scene.  It only observes the
## original C# nodes and feeds snapshots into EnemyDataNodeAdapter resources.
@export var battle_manager_path: NodePath = NodePath("../battlemanager")
@export var enemy_manager_path: NodePath = NodePath("../EnemyManager")
@export var player_path: NodePath = NodePath("../PlayerBattle")
@export var effect_resolver_path: NodePath = NodePath("../effectresolver")
@export var board_manager_path: NodePath = NodePath("../boardmanager")
@export var ranged_card_names: PackedStringArray = PackedStringArray([
	"四握式线圈铳", "豆豆枪", "致命亲亲", "简易火炮",
])

var last_player_damage: int = 0
var last_player_action_type: StringName = &""
var player_has_unlit_cell: bool = false
var _previous_enemy_hp: Dictionary = {}
var _damage_this_round: Dictionary = {}
var _observed_round: int = -1
var _bound_effect_resolver: Node


func _ready() -> void:
	_bind_effect_resolver()


func _process(_delta: float) -> void:
	_bind_effect_resolver()
	refresh_runtime_contexts()


func refresh_runtime_contexts() -> void:
	var battle_manager := get_node_or_null(battle_manager_path)
	var enemy_manager := get_node_or_null(enemy_manager_path)
	var player := get_node_or_null(player_path)
	var board_manager := get_node_or_null(board_manager_path)
	if enemy_manager == null or player == null:
		return
	player_has_unlit_cell = _board_has_unlit_cell(board_manager)
	var round_number := int(_read_property(battle_manager, [&"RoundNumber", &"round_number"], 1))
	if _observed_round != round_number:
		_observed_round = round_number
		_damage_this_round.clear()
		last_player_damage = 0
		last_player_action_type = &""
		player_has_unlit_cell = false
	var battle_phase := int(_read_property(battle_manager, [&"CurrentPhase", &"current_phase"], -1))
	var runtime_enemies := _runtime_enemies(enemy_manager)
	var true_hand_hp := _role_hp(runtime_enemies, &"true_hand")
	var false_hand_hp := _role_hp(runtime_enemies, &"false_hand")
	for enemy in runtime_enemies:
		var data: Variant = _call_first(enemy, [&"GetEnemyData", &"get_enemy_data"])
		if not data is EnemyDataNodeAdapter:
			continue
		var enemy_position := int(_read_property(enemy, [&"MapPosition", &"map_position"], 1))
		var player_position := int(_read_property(player, [&"MapPosition", &"map_position"], 1))
		var current_enemy_hp := int(_read_property(enemy, [&"CurrentHp", &"current_hp"], data.max_hp))
		var runtime_key: int = enemy.get_instance_id()
		if _previous_enemy_hp.has(runtime_key):
			var hp_loss := maxi(0, int(_previous_enemy_hp[runtime_key]) - current_enemy_hp)
			_damage_this_round[runtime_key] = int(_damage_this_round.get(runtime_key, 0)) + hp_loss
		_previous_enemy_hp[runtime_key] = current_enemy_hp
		data.set_ai_runtime_context({
			"enemy_id": data.id,
			"role": data.role,
			"enemy_hp": current_enemy_hp,
			"enemy_max_hp": int(_read_property(enemy, [&"MaxHp", &"max_hp"], data.max_hp)),
			"enemy_shield": int(_read_property(enemy, [&"Shield", &"shield"], 0)),
			"enemy_position": enemy_position,
			"player_hp": int(_read_property(player, [&"CurrentHp", &"current_hp"], 20)),
			"player_max_hp": int(_read_property(player, [&"MaxHp", &"max_hp"], 20)),
			"player_shield": int(_read_property(player, [&"Shield", &"shield"], 0)),
			"player_position": player_position,
			"distance": absi(enemy_position - player_position),
			"round_number": round_number,
			"last_player_damage": maxi(last_player_damage, int(_damage_this_round.get(runtime_key, 0))),
			"damage_taken_last_turn": maxi(last_player_damage, int(_damage_this_round.get(runtime_key, 0))),
			"phase": 2 if data.role == &"body" else 1,
			"battle_phase": battle_phase,
			"last_player_action_type": last_player_action_type,
			"player_has_unlit_cell": player_has_unlit_cell,
			"true_hand_hp": true_hand_hp,
			"false_hand_hp": false_hand_hp,
			"enemy_has_true_buff": _enemy_has_buff(enemy, "true"),
		})


func _role_hp(enemies: Array, expected_role: StringName) -> int:
	for enemy in enemies:
		var data: Variant = _call_first(enemy, [&"GetEnemyData", &"get_enemy_data"])
		if data is EnemyDataNodeAdapter and data.role == expected_role:
			return maxi(0, int(_read_property(enemy, [&"CurrentHp", &"current_hp"], 0)))
	return 0


func _enemy_has_buff(enemy: Object, buff_id: String) -> bool:
	var stats: Variant = _call_first(enemy, [&"GetStats", &"get_stats"])
	return stats != null and stats.has_method(&"has_buff") and bool(stats.call(&"has_buff", buff_id))


func report_player_action(damage: int, action_type: StringName, has_unlit_cell: bool = false) -> void:
	last_player_damage = maxi(0, damage)
	last_player_action_type = action_type
	player_has_unlit_cell = has_unlit_cell


func _bind_effect_resolver() -> void:
	if _bound_effect_resolver != null and is_instance_valid(_bound_effect_resolver):
		return
	var resolver := get_node_or_null(effect_resolver_path)
	if resolver == null or not resolver.has_signal(&"effect_executed"):
		return
	var callback := Callable(self, &"_on_effect_executed")
	if not resolver.is_connected(&"effect_executed", callback):
		resolver.connect(&"effect_executed", callback)
	_bound_effect_resolver = resolver


func _on_effect_executed(source_name: String, effect_type: String, value: int) -> void:
	if effect_type != "damage":
		return
	var action_type: StringName = &"gun" if ranged_card_names.has(source_name) else &"melee"
	report_player_action(maxi(0, value), action_type, player_has_unlit_cell)


func _board_has_unlit_cell(board_manager: Object) -> bool:
	if board_manager == null:
		return false
	var columns := int(_read_property(board_manager, [&"columns", &"Columns"], 0))
	var rows := int(_read_property(board_manager, [&"rows", &"Rows"], 0))
	if columns <= 0 or rows <= 0 or not board_manager.has_method(&"GetCell"):
		return false
	for y in range(rows):
		for x in range(columns):
			var cell: Variant = board_manager.call(&"GetCell", Vector2i(x, y))
			if cell != null and not bool(_read_property(cell, [&"is_lit", &"IsLit"], false)):
				return true
	return false


func _read_property(object: Object, candidates: Array[StringName], fallback: Variant) -> Variant:
	if object == null:
		return fallback
	var available := {}
	for descriptor in object.get_property_list():
		available[StringName(descriptor.get("name", ""))] = true
	for candidate in candidates:
		if available.has(candidate):
			return object.get(candidate)
	return fallback


func _call_first(object: Object, candidates: Array[StringName]) -> Variant:
	if object == null:
		return null
	for candidate in candidates:
		if object.has_method(candidate):
			return object.call(candidate)
	return null


func _runtime_enemies(enemy_manager: Object) -> Array:
	if enemy_manager == null:
		return []
	var configured: Variant = _read_property(enemy_manager, [&"Enemies", &"enemies"], null)
	if configured is Array:
		return configured
	if enemy_manager is Node:
		return enemy_manager.get_children()
	return []
