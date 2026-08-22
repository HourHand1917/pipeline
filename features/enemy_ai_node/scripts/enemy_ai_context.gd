extends RefCounted
class_name EnemyAIContext

## A small, engine-facing snapshot.  AI nodes only read this object; they never
## reach into BattleManager directly, which keeps every AI scene reusable.
var enemy_id: StringName = &"enemy"
var role: StringName = &""
var enemy_hp: int = 1
var enemy_max_hp: int = 1
var enemy_shield: int = 0
var enemy_position: int = 1
var player_hp: int = 20
var player_max_hp: int = 20
var player_shield: int = 0
var player_position: int = 1
var distance: int = 1
var round_number: int = 1
var damage_taken_last_turn: int = 0
var last_player_damage: int = 0
var last_player_action_type: StringName = &""
var player_has_unlit_cell: bool = false
var phase: int = 1
var battle_phase: int = -1
var true_hand_hp: int = 21
var false_hand_hp: int = 21
var body_hp: int = 50
var enemy_has_true_buff: bool = false
var metadata: Dictionary = {}


func update_from_dictionary(values: Dictionary) -> EnemyAIContext:
	for key in values:
		match StringName(key):
			&"enemy_id": enemy_id = StringName(values[key])
			&"role": role = StringName(values[key])
			&"enemy_hp": enemy_hp = int(values[key])
			&"enemy_max_hp": enemy_max_hp = int(values[key])
			&"enemy_shield": enemy_shield = int(values[key])
			&"enemy_position": enemy_position = int(values[key])
			&"player_hp": player_hp = int(values[key])
			&"player_max_hp": player_max_hp = int(values[key])
			&"player_shield": player_shield = int(values[key])
			&"player_position": player_position = int(values[key])
			&"distance": distance = int(values[key])
			&"round_number": round_number = int(values[key])
			&"damage_taken_last_turn": damage_taken_last_turn = int(values[key])
			&"last_player_damage": last_player_damage = int(values[key])
			&"last_player_action_type": last_player_action_type = StringName(values[key])
			&"player_has_unlit_cell": player_has_unlit_cell = bool(values[key])
			&"phase": phase = int(values[key])
			&"battle_phase": battle_phase = int(values[key])
			&"true_hand_hp": true_hand_hp = int(values[key])
			&"false_hand_hp": false_hand_hp = int(values[key])
			&"body_hp": body_hp = int(values[key])
			&"enemy_has_true_buff": enemy_has_true_buff = bool(values[key])
			_: metadata[key] = values[key]
	return self


func hp_ratio() -> float:
	return float(enemy_hp) / float(maxi(enemy_max_hp, 1))


func player_hp_ratio() -> float:
	return float(player_hp) / float(maxi(player_max_hp, 1))


func player_is_on_even_cell() -> bool:
	return player_position % 2 == 0


func decision_signature() -> int:
	# `battle_phase` is intentionally excluded.  The preview selected during
	# PlayerTurn must be the exact action executed when EnemyTurn begins.
	return hash([
		enemy_id, role, enemy_hp, enemy_max_hp, enemy_shield, enemy_position,
		player_hp, player_max_hp, player_shield, player_position, distance,
		round_number, damage_taken_last_turn, last_player_damage,
		last_player_action_type, player_has_unlit_cell, phase,
		true_hand_hp, false_hand_hp, body_hp,
		enemy_has_true_buff,
	])
