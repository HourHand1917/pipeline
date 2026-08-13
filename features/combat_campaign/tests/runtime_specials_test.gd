extends Node

var failures: Array[String] = []


func _ready() -> void:
	_test_item_contracts()
	_test_enemy_special_contracts()
	_test_campaign_preserve_flag()
	if failures.is_empty():
		print("RUNTIME_SPECIALS_TEST PASS")
		get_tree().quit(0)
		return
	for failure in failures:
		push_error(failure)
	get_tree().quit(1)


func _check(value: bool, message: String) -> void:
	if not value:
		failures.append(message)


func _test_item_contracts() -> void:
	var expected := {
		"universal_toolkit": [1, 2],
		"power_sunglasses": [3],
		"teleport_insoles": [4],
		"cooldown_spray": [5],
		"smoke_grenade": [6],
	}
	for id in expected:
		var item := load("res://Resource/item/mvp/%s.tres" % id) as ItemData
		_check(item != null, "missing item %s" % id)
		if item != null:
			_check(item.special_effects == expected[id], "%s special enum mismatch" % id)


func _test_enemy_special_contracts() -> void:
	var expected := {
		"sharkk_charge": "push_player_to_edge",
		"sharkk_stunned": "wait",
		"core_body_teleport_guard": "move_behind_player",
		"core_body_jam": "add_all_card_cooldown",
		"core_true_send_heal": "heal_specific_enemy",
		"core_true_death_loop": "set_death_loop",
		"core_false_break_beam": "clear_true_death_loop",
	}
	for id in expected:
		var action := load("res://features/enemy_ai_node/resources/actions/%s.tres" % id) as EnemyActionData
		_check(action != null, "missing enemy action %s" % id)
		if action != null:
			_check(action.special_effect == StringName(expected[id]), "%s special contract mismatch" % id)
	var charge := load("res://features/enemy_ai_node/resources/actions/sharkk_charge.tres") as EnemyActionData
	_check(charge != null and charge.special_replaces_effects, "charge must replace legacy effects")
	_check(charge != null and charge.special_value == 10, "charge damage must be 10")


func _test_campaign_preserve_flag() -> void:
	var campaign := load("res://features/combat_campaign/data/default_combat_campaign.tres")
	_check(campaign != null and campaign.waves.size() == 5, "campaign must contain five waves")
	if campaign != null and campaign.waves.size() == 5:
		var body = campaign.waves[4]
		_check(body.preserve_player_state, "Core body must preserve player state")
		_check(body.preserve_board_runtime_state, "Core body must preserve card state")

