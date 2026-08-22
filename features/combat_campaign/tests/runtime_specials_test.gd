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
	}
	for id in expected:
		var action := load("res://features/enemy_ai_node/resources/actions/%s.tres" % id) as EnemyActionData
		_check(action != null, "missing enemy action %s" % id)
		if action != null:
			_check(action.special_effect == StringName(expected[id]), "%s special contract mismatch" % id)
	var charge := load("res://features/enemy_ai_node/resources/actions/sharkk_charge.tres") as EnemyActionData
	_check(charge != null and charge.special_replaces_effects, "charge must replace legacy effects")
	_check(charge != null and charge.special_value == 10, "charge damage must be 10")

	_test_core_true_contracts()
	_test_core_false_contracts()


func _test_core_true_contracts() -> void:
	var heal := load("res://features/enemy_ai_node/resources/actions/core_true_send_heal.tres") as EnemyActionData
	_check(heal != null and heal.effect_target_role == &"false_hand", "Core heal must target False hand")
	_check(heal != null and heal.fixed_target_cells == PackedInt32Array([2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
		"Core treatment package must attack absolute cells 2-11")
	_check(_single_effect_is(heal, CombatEffectData.Type.DAMAGE, 5),
		"Core treatment package must deal 5 damage")
	_check(heal != null and heal.deferred_role_effects.size() == 1,
		"Core heal must carry one deferred role effect")
	if heal != null and heal.deferred_role_effects.size() == 1:
		var effect := heal.deferred_role_effects[0]
		_check(effect.type == CombatEffectData.Type.APPLY_BUFF,
			"Core heal package must be an APPLY_BUFF effect")
		_check(effect.buff != null and effect.buff.id == &"deployed_medkit",
			"Core heal package must mount deployed_medkit")
		_check(effect.buff_stacks == 12,
			"Core heal package must carry 12 deployed-medkit stacks")

	var charge := load("res://features/enemy_ai_node/resources/actions/core_true_charge.tres") as EnemyActionData
	_check(_is_wait_action(charge), "Core True step 2 must be a pure charge/wait action")

	var guard := load("res://features/enemy_ai_node/resources/actions/core_true_guard_beam.tres") as EnemyActionData
	_check(guard != null and guard.pattern_parity == 0,
		"Core protection beam must target even cells")
	_check(_single_effect_is(guard, CombatEffectData.Type.DAMAGE, 7),
		"Core protection beam must deal 7 damage")
	_check(guard != null and guard.pattern_hit_effects.size() == 2,
		"Core protection beam hit must apply its two True effects")
	if guard != null and guard.pattern_hit_effects.size() == 2:
		_check(_buff_effect_is(guard.pattern_hit_effects[0], &"true", 3),
			"Core protection beam must apply 3 True stacks to the player")
		_check(_buff_effect_is(guard.pattern_hit_effects[1], &"true", 1),
			"Core protection beam must mark True hand with persistent True")
	_check(guard != null and guard.effect_target_role == &"false_hand",
		"Core protection beam must protect False hand")
	_check(guard != null and guard.pattern_hit_role_effects.size() == 1
		and _effect_is(guard.pattern_hit_role_effects[0], CombatEffectData.Type.SHIELD, 25),
		"Core protection beam must grant False hand 25 block on hit")

	var death_loop := load("res://features/enemy_ai_node/resources/actions/core_true_death_loop.tres") as EnemyActionData
	_check(death_loop != null and death_loop.special_effect == &"",
		"Core death loop must not use the removed immortality special")
	_check(death_loop != null and death_loop.effect_target_role == &"self"
		and _single_effect_is(death_loop, CombatEffectData.Type.SHIELD, 15),
		"Core death loop must grant True hand 15 block")


func _test_core_false_contracts() -> void:
	for action_id in [&"core_false_charge", &"core_false_heal", &"core_false_stun"]:
		var wait_action := load("res://features/enemy_ai_node/resources/actions/%s.tres" % action_id) as EnemyActionData
		_check(_is_wait_action(wait_action), "%s must be a pure wait cycle" % action_id)

	var complete := load("res://features/enemy_ai_node/resources/actions/core_false_charge_complete.tres") as EnemyActionData
	_check(complete != null and complete.fixed_target_cells == PackedInt32Array([1, 2, 3]),
		"Core False completed charge must target absolute cells 1-3")
	_check(_single_effect_is(complete, CombatEffectData.Type.DAMAGE, 11),
		"Core False completed charge must deal 11 damage")
	_check(complete != null and complete.pattern_hit_effects.size() == 1
		and _buff_effect_is(complete.pattern_hit_effects[0], &"false", 3),
		"Core False completed charge must apply 3 False stacks on hit")

	var break_beam := load("res://features/enemy_ai_node/resources/actions/core_false_break_beam.tres") as EnemyActionData
	_check(break_beam != null and break_beam.fixed_target_cells == PackedInt32Array([1, 3, 5, 7, 9, 11]),
		"Core False break beam must target odd cells 1-11 only")
	_check(_single_effect_is(break_beam, CombatEffectData.Type.DAMAGE, 25),
		"Core False break beam must deal 25 damage")
	_check(break_beam != null and break_beam.special_effect == &"",
		"Core False break beam must not use the removed death-loop special")
	_check(break_beam != null and break_beam.pattern_hit_effects.size() == 1
		and _buff_effect_is(break_beam.pattern_hit_effects[0], &"false", 3),
		"Core False break beam must apply 3 False stacks to the player on hit")
	_check(break_beam != null and break_beam.effect_target_role == &"true_hand"
		and break_beam.pattern_hit_role_effects.size() == 1
		and _buff_effect_is(break_beam.pattern_hit_role_effects[0], &"false", 3),
		"Core False break beam must apply 3 False stacks to True hand on hit")


func _is_wait_action(action: EnemyActionData) -> bool:
	return action != null \
		and action.special_effect == &"wait" \
		and action.special_replaces_effects \
		and action.effects.is_empty()


func _single_effect_is(action: EnemyActionData, type: CombatEffectData.Type, amount: int) -> bool:
	return action != null and action.effects.size() == 1 and _effect_is(action.effects[0], type, amount)


func _effect_is(effect: CombatEffectData, type: CombatEffectData.Type, amount: int) -> bool:
	return effect != null and effect.type == type and effect.amount == amount


func _buff_effect_is(effect: CombatEffectData, buff_id: StringName, stacks: int) -> bool:
	return effect != null \
		and effect.type == CombatEffectData.Type.APPLY_BUFF \
		and effect.buff != null \
		and effect.buff.id == buff_id \
		and effect.buff_stacks == stacks


func _test_campaign_preserve_flag() -> void:
	var campaign := load("res://features/combat_campaign/data/default_combat_campaign.tres")
	_check(campaign != null and campaign.waves.size() == 5, "campaign must contain five waves")
	if campaign != null and campaign.waves.size() == 5:
		var body = campaign.waves[4]
		_check(body.preserve_player_state, "Core body must preserve player state")
		_check(body.preserve_board_runtime_state, "Core body must preserve card state")
