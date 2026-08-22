extends Node

var failures: Array[String] = []
var checks := 0


func _ready() -> void:
	_test_forward_ranges()
	_test_core00_fixed_cells()
	_test_charge_and_push()
	_test_prepared_and_global_warning()
	_test_prediction_has_no_ai_side_effect()
	_test_sharkk_state_machine()
	_test_sharkk_pressure_and_continuous_movement()
	_test_weighted_ai_is_deterministic()
	if failures.is_empty():
		print("DANGER_AREA_PREDICTOR_TEST_PASS checks=%d" % checks)
		get_tree().quit(0)
	else:
		for failure in failures:
			push_error(failure)
		print("DANGER_AREA_PREDICTOR_TEST_FAIL failures=%d checks=%d" % [failures.size(), checks])
		get_tree().quit(1)


func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)


func _predictor() -> DangerAreaPredictor:
	return DangerAreaPredictor.new()


func _test_forward_ranges() -> void:
	var predictor := _predictor()
	var boom := load("res://features/enemy_ai_node/resources/actions/boom_attack.tres") as EnemyActionData
	var right := predictor.predict_action(boom, 3, 0, 6, 8)
	_check(right.current_cells == PackedInt32Array([4, 5]), "forward range facing right is wrong")
	var left := predictor.predict_action(boom, 6, 1, 3, 8)
	_check(left.current_cells == PackedInt32Array([5, 4]), "forward range facing left is wrong")
	predictor.free()


func _test_core00_fixed_cells() -> void:
	var predictor := _predictor()
	var package := load("res://features/enemy_ai_node/resources/actions/core_true_send_heal.tres") as EnemyActionData
	var package_result := predictor.predict_action(package, 1, 0, 6, 12)
	_check(package_result.current_cells == PackedInt32Array([2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
		"Core treatment-package telegraph must cover absolute cells 2-11")

	var beam := load("res://features/enemy_ai_node/resources/actions/core_true_guard_beam.tres") as EnemyActionData
	var beam_result := predictor.predict_action(beam, 1, 0, 6, 12)
	_check(beam_result.current_cells == PackedInt32Array([2, 4, 6, 8, 10, 12]),
		"Core protection beam telegraph must cover every even cell")

	var charge := load("res://features/enemy_ai_node/resources/actions/core_false_charge_complete.tres") as EnemyActionData
	var charge_result := predictor.predict_action(charge, 12, 1, 6, 12)
	_check(charge_result.current_cells == PackedInt32Array([1, 2, 3]),
		"Core False completed charge telegraph must cover absolute cells 1-3")

	var break_beam := load("res://features/enemy_ai_node/resources/actions/core_false_break_beam.tres") as EnemyActionData
	var break_result := predictor.predict_action(break_beam, 12, 1, 6, 12)
	_check(break_result.current_cells == PackedInt32Array([1, 3, 5, 7, 9, 11]),
		"Core False break beam telegraph must cover odd cells 1-11 only")
	predictor.free()


func _test_charge_and_push() -> void:
	var predictor := _predictor()
	var charge := load("res://features/enemy_ai_node/resources/actions/sharkk_charge.tres") as EnemyActionData
	var right := predictor.predict_action(charge, 2, 0, 5, 9)
	_check(right.movement_destination == 4, "charge destination should stop adjacent to player")
	_check(right.current_cells == PackedInt32Array([3, 4, 5, 6, 7, 8, 9]), "charge push-to-right path is wrong")
	var blocked := predictor.predict_action(charge, 2, 0, 5, 9, PackedInt32Array([7]))
	_check(blocked.current_cells == PackedInt32Array([3, 4, 5, 6]), "charge push must stop before occupied cell")
	var left := predictor.predict_action(charge, 8, 1, 5, 9)
	_check(left.movement_destination == 6, "left charge destination is wrong")
	_check(left.current_cells == PackedInt32Array([7, 6, 5, 4, 3, 2, 1]), "charge push-to-left path is wrong")
	predictor.free()


func _test_prepared_and_global_warning() -> void:
	var predictor := _predictor()
	var prepare := load("res://features/enemy_ai_node/resources/actions/sharkk_prepare_charge.tres") as EnemyActionData
	var prepared := predictor.predict_action(prepare, 8, 1, 4, 9)
	_check(prepared.current_cells.is_empty(), "prepared charge must not mark current danger")
	_check(not prepared.future_cells.is_empty(), "prepared charge must mark next-turn danger")
	var jam := load("res://features/enemy_ai_node/resources/actions/core_body_jam.tres") as EnemyActionData
	var jam_result := predictor.predict_action(jam, 8, 1, 2, 13)
	_check(not String(jam_result.global_warning).is_empty(), "global jam warning is missing")
	_check(jam_result.current_cells.is_empty(), "global warning must not fake track cells")
	predictor.free()


func _test_prediction_has_no_ai_side_effect() -> void:
	var packed := load("res://features/enemy_ai_node/scenes/sharkk_ai.tscn") as PackedScene
	var ai := packed.instantiate() as SharkkEnemyAI
	ai.charge_chance = 1.0
	var context := EnemyAIContext.new().update_from_dictionary({"distance": 4, "round_number": 1})
	var action := ai.select_action(context)
	var decisions := ai.decision_turn
	var predictor := _predictor()
	for _read in range(100):
		predictor.predict_action(action, 8, 1, 4, 9)
	_check(ai.decision_turn == decisions, "prediction changed AI decision counter")
	_check(ai.charge_state == SharkkEnemyAI.ChargeState.NEUTRAL, "prediction advanced Sharkk state")
	predictor.free()
	ai.free()


func _test_sharkk_state_machine() -> void:
	var packed := load("res://features/enemy_ai_node/scenes/sharkk_ai.tscn") as PackedScene
	var ai := packed.instantiate() as SharkkEnemyAI
	var hit := EnemyAIContext.new().update_from_dictionary({"distance": 2, "round_number": 1, "damage_taken_last_turn": 10})
	var retreat := ai.select_action(hit)
	_check(retreat.id == &"sharkk_sand_retreat", "10+ middle-range hit must force sand retreat")
	ai.confirm_action(retreat, hit)
	_check(ai.charge_state == SharkkEnemyAI.ChargeState.PREPARED, "retreat must prepare charge")
	var charge_context := EnemyAIContext.new().update_from_dictionary({"distance": 4, "round_number": 2})
	var charge := ai.select_action(charge_context)
	_check(charge.id == &"sharkk_charge", "prepared Sharkk must charge")
	ai.confirm_action(charge, charge_context)
	_check(ai.charge_state == SharkkEnemyAI.ChargeState.RECOVERING, "charge must enter recovery")
	var recover_context := EnemyAIContext.new().update_from_dictionary({"distance": 1, "round_number": 3})
	var stunned := ai.select_action(recover_context)
	_check(stunned.id == &"sharkk_stunned", "post-charge turn must be stunned")
	ai.confirm_action(stunned, recover_context)
	_check(ai.charge_state == SharkkEnemyAI.ChargeState.NEUTRAL, "stunned turn must return to neutral")
	ai.free()


func _test_sharkk_pressure_and_continuous_movement() -> void:
	var packed := load("res://features/enemy_ai_node/scenes/sharkk_ai.tscn") as PackedScene
	var ai := packed.instantiate() as SharkkEnemyAI
	var ranged_context := EnemyAIContext.new().update_from_dictionary({
		"distance": 6,
		"round_number": 1,
		"last_player_action_type": &"ranged",
		"damage_taken_last_turn": 4,
	})
	var advance := ai.select_action(ranged_context)
	_check(advance.id == &"sharkk_advance", "long-range Sharkk must keep moving")
	ai.confirm_action(advance, ranged_context)
	_check(ai.ranged_pressure == 3, "ranged hit pressure should add 2+1")
	var light_hit := EnemyAIContext.new().update_from_dictionary({
		"distance": 3,
		"round_number": 2,
		"damage_taken_last_turn": 3,
	})
	var prepare := ai.select_action(light_hit)
	_check(prepare.id == &"sharkk_prepare_charge", "any middle-range hit must prepare a charge")
	ai.confirm_action(prepare, light_hit)
	_check(ai.ranged_pressure == 2, "non-ranged turn should decay pressure by one")
	ai.free()


func _test_weighted_ai_is_deterministic() -> void:
	var packed := load("res://features/enemy_ai_node/scenes/rocky_ai.tscn") as PackedScene
	var first := packed.instantiate() as RockyEnemyAI
	var second := packed.instantiate() as RockyEnemyAI
	first.reset_ai()
	second.reset_ai()
	var first_sequence: Array[StringName] = []
	var second_sequence: Array[StringName] = []
	for round_number in range(1, 13):
		var first_context := EnemyAIContext.new().update_from_dictionary({"distance": 2, "round_number": round_number})
		var first_action := first.select_action(first_context)
		first_sequence.append(first_action.id)
		first.confirm_action(first_action, first_context)
		var second_context := EnemyAIContext.new().update_from_dictionary({"distance": 2, "round_number": round_number})
		var second_action := second.select_action(second_context)
		second_sequence.append(second_action.id)
		second.confirm_action(second_action, second_context)
	_check(first_sequence == second_sequence, "same AI seed/context must produce identical weighted sequence")
	var exact_repeats := 0
	for index in range(1, first_sequence.size()):
		if first_sequence[index] == first_sequence[index - 1]:
			exact_repeats += 1
	_check(exact_repeats < first_sequence.size() - 1, "repeat penalty failed to diversify all weighted decisions")
	first.free()
	second.free()
