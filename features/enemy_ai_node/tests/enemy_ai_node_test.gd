extends Node

var failures: Array[String] = []
var checks: int = 0


func _ready() -> void:
	_test_enemy_data()
	_test_boom()
	_test_rocky()
	_test_sharkk()
	_test_core00()
	_test_maps()
	_test_adapter_dispatch()
	_test_preview_execute_stability()
	_test_sharkk_preview_state()
	_test_main_presets()
	_test_main_scene_load()
	if failures.is_empty():
		print("ENEMY_AI_NODE_TEST_PASS checks=%d" % checks)
		get_tree().quit(0)
	else:
		for failure in failures:
			push_error(failure)
		print("ENEMY_AI_NODE_TEST_FAIL failures=%d checks=%d" % [failures.size(), checks])
		get_tree().quit(1)


func _check(value: bool, message: String) -> void:
	checks += 1
	if not value:
		failures.append(message)


func _ctx(values: Dictionary) -> EnemyAIContext:
	var context := EnemyAIContext.new()
	context.update_from_dictionary(values)
	return context


func _scene(path: String) -> EnemyAIController:
	var packed := load(path) as PackedScene
	_check(packed != null, "cannot load scene: " + path)
	if packed == null:
		return null
	var instance := packed.instantiate() as EnemyAIController
	_check(instance != null, "scene root is not EnemyAIController: " + path)
	return instance


func _test_enemy_data() -> void:
	var expected := {
		"boom": 8,
		"rocky": 20,
		"sharkk": 40,
		"core00_true_hand": 21,
		"core00_false_hand": 21,
		"core00_body": 50,
	}
	for file_name in expected:
		var data := load("res://features/enemy_ai_node/enemy_data/%s.tres" % file_name) as EnemyDataNodeAdapter
		_check(data != null, "missing EnemyDataNodeAdapter: " + file_name)
		if data != null:
			_check(data.max_hp == expected[file_name], "wrong HP for " + file_name)
			_check(data.ai_scene != null, "missing AI scene for " + file_name)
			var provider := data.get_ai_provider()
			_check(provider is EnemyAIController, "adapter did not instantiate provider: " + file_name)


func _test_boom() -> void:
	var ai := _scene("res://features/enemy_ai_node/scenes/boom_ai.tscn")
	if ai == null: return
	ai.reset_ai()
	var attack := ai.select_action(_ctx({"distance": 1}))
	_check(attack != null and attack.id == &"boom_attack", "boom must attack at 1")
	var advance := ai.select_action(_ctx({"distance": 6}))
	_check(advance != null and advance.id in [&"boom_advance_1", &"boom_advance_2"], "boom must advance when far")


func _test_rocky() -> void:
	var ai := _scene("res://features/enemy_ai_node/scenes/rocky_ai.tscn")
	if ai == null: return
	ai.reset_ai()
	ai.retreat_chance_after_heavy_hit = 1.0
	var evade := ai.select_action(_ctx({"distance": 3, "damage_taken_last_turn": 9}))
	_check(evade != null and evade.id in [&"rocky_retreat", &"rocky_defend"], "rocky must react to heavy damage")
	var close_action := ai.select_action(_ctx({"distance": 1}))
	_check(close_action != null and close_action.id in [&"rocky_close_attack", &"rocky_defend"], "rocky close decision invalid")


func _test_sharkk() -> void:
	var ai := _scene("res://features/enemy_ai_node/scenes/sharkk_ai.tscn")
	if ai == null: return
	ai.reset_ai()
	var punish := ai.select_action(_ctx({"distance": 2, "damage_taken_last_turn": 10}))
	_check(punish != null and punish.id == &"sharkk_sand_retreat", "sharkk 10+ damage reaction must be sand retreat")
	ai.reset_ai()
	ai.charge_chance = 1.0
	var prepare_context := _ctx({"distance": 4, "round_number": 1})
	var prepare := ai.select_action(prepare_context)
	_check(prepare != null and prepare.id == &"sharkk_prepare_charge", "sharkk should prepare charge")
	ai.confirm_action(prepare, prepare_context)
	var charge := ai.select_action(_ctx({"distance": 4, "round_number": 2}))
	_check(charge != null and charge.id == &"sharkk_charge", "prepared sharkk should charge")


func _test_core00() -> void:
	var true_ai := _scene("res://features/enemy_ai_node/scenes/core00_ai.tscn")
	var false_ai := _scene("res://features/enemy_ai_node/scenes/core00_ai.tscn")
	if true_ai == null or false_ai == null: return

	# True starts a latched three-step sequence. A True flag that appears in
	# the middle cannot interrupt it; only the judgement after step 3 sees it.
	var true_expected := [
		&"core_true_send_heal", &"core_true_charge", &"core_true_guard_beam",
		&"core_true_death_loop", &"core_true_death_loop", &"core_true_send_heal",
	]
	var true_flags := [false, true, true, true, true, false]
	for index in range(true_expected.size()):
		var context := _ctx({
			"role": &"true_hand", "phase": 1, "round_number": index + 1,
			"player_position": 5 if index == 2 else 6,
			"enemy_has_true_buff": true_flags[index],
			"true_hand_hp": 21, "false_hand_hp": 21,
		})
		var action := true_ai.select_action(context)
		_check(action != null and action.id == true_expected[index],
			"true hand state mismatch at decision %d" % (index + 1))
		true_ai.confirm_action(action, context)

	# False owns an independent five-action loop and never changes intent based
	# on player position/parity.
	var false_expected := [
		&"core_false_charge", &"core_false_heal", &"core_false_stun",
		&"core_false_charge_complete", &"core_false_break_beam",
	]
	for index in range(10):
		var context := _ctx({
			"role": &"false_hand", "phase": 1, "round_number": index + 1,
			"player_position": 6 if index % 2 == 0 else 5,
			"true_hand_hp": 21, "false_hand_hp": 21,
		})
		var action := false_ai.select_action(context)
		_check(action != null and action.id == false_expected[index % 5],
			"false hand cycle mismatch at decision %d" % (index + 1))
		false_ai.confirm_action(action, context)

	true_ai.reset_ai()
	var passive_true := true_ai.select_action(_ctx({
		"role": &"true_hand", "phase": 1, "true_hand_hp": 21, "false_hand_hp": 0,
	}))
	var passive_false := false_ai.select_action(_ctx({
		"role": &"false_hand", "phase": 1, "true_hand_hp": 0, "false_hand_hp": 21,
	}))
	_check(passive_true != null and passive_true.id == &"core_hand_passive",
		"True must become passive after False dies")
	_check(passive_false != null and passive_false.id == &"core_hand_passive",
		"False must become passive after True dies")

	# Buff state is part of the adapter signature; changing only that flag must
	# invalidate the cached preview in the same round.
	var true_data := load("res://features/enemy_ai_node/enemy_data/core00_true_hand.tres") as EnemyDataNodeAdapter
	true_data.reset_ai_provider()
	true_data.set_ai_runtime_context({
		"round_number": 1, "battle_phase": 1, "true_hand_hp": 21,
		"false_hand_hp": 21, "enemy_has_true_buff": false,
	})
	var no_flag := true_data.get_action_for_distance(5)
	true_data.set_ai_runtime_context({"enemy_has_true_buff": true})
	var with_flag := true_data.get_action_for_distance(5)
	_check(no_flag != null and no_flag.id == &"core_true_send_heal",
		"True without its marker must start at step 1")
	_check(with_flag != null and with_flag.id == &"core_true_death_loop",
		"True marker change must invalidate cached intent")

	var ai := _scene("res://features/enemy_ai_node/scenes/core00_ai.tscn")
	if ai == null: return
	ai.phase_two_attack_chance = 1.0
	var sniper := ai.select_action(_ctx({"role": &"body", "phase": 2, "round_number": 1, "distance": 8}))
	_check(sniper != null and sniper.id == &"core_body_sniper", "Core body should snipe at 6-12")
	var blink := ai.select_action(_ctx({"role": &"body", "phase": 2, "round_number": 2, "distance": 3, "damage_taken_last_turn": 9}))
	_check(blink != null and blink.id == &"core_body_teleport_guard", "Core body should reactively teleport")
	var body_data := load("res://features/enemy_ai_node/enemy_data/core00_body.tres") as EnemyDataNodeAdapter
	body_data.set_ai_runtime_context({"round_number": 1, "enemy_hp": 50})
	var body_from_adapter := body_data.get_action_for_distance(8)
	_check(body_from_adapter != null and body_from_adapter.id == &"core_body_sniper", "Core body adapter must default to phase two")


func _test_maps() -> void:
	for map_name in ["boom_test_map", "rocky_boom_map", "sharkk_map", "core00_phase_one_map", "core00_phase_two_map"]:
		var battle_map := load("res://features/enemy_ai_node/battle_maps/%s.tres" % map_name) as BattleMapData
		_check(battle_map != null, "cannot load map " + map_name)
		if battle_map != null:
			_check(battle_map.is_configuration_valid(), "invalid map " + map_name)


func _test_adapter_dispatch() -> void:
	var data := load("res://features/enemy_ai_node/enemy_data/boom.tres") as EnemyDataNodeAdapter
	_check(data != null, "boom adapter missing")
	if data == null: return
	data.set_ai_runtime_context({"enemy_hp": 8, "enemy_position": 6, "player_position": 5, "round_number": 1})
	var action := data.get_action_for_distance(1)
	_check(action != null and action.id == &"boom_attack", "adapter did not delegate to boom AI")


func _test_preview_execute_stability() -> void:
	var data := load("res://features/enemy_ai_node/enemy_data/rocky.tres") as EnemyDataNodeAdapter
	_check(data != null, "rocky adapter missing for preview stability")
	if data == null: return
	data.reset_ai_provider()
	data.set_ai_runtime_context({
		"enemy_hp": 20, "enemy_position": 6, "player_position": 5,
		"distance": 1, "round_number": 1, "battle_phase": 1,
		"enemy_shield": 7,
	})
	var preview_one := data.get_action_for_distance(1)
	var provider := data.get_ai_provider() as EnemyAIController
	var decisions_after_first := provider.decision_turn
	var preview_two := data.get_action_for_distance(99)
	_check(preview_one == preview_two, "same state must return the same cached preview")
	_check(provider.decision_turn == decisions_after_first, "repeated UI preview must not reroll or advance decision")
	# Enemy-turn guard expiry changes the public snapshot, but confirmation must
	# commit the exact locked preview rather than secretly rerolling.
	data.set_ai_runtime_context({"battle_phase": 2, "enemy_shield": 0})
	data.confirm_locked_action(preview_one, 1)
	var executed := data.get_action_for_distance(1)
	_check(executed == preview_one, "EnemyTurn must execute the final PlayerTurn preview")
	_check(provider.decision_turn == decisions_after_first, "execution lookup must not select twice")
	var cooldown_after_confirm := provider.cooldown_remaining(executed.id)
	data.get_action_for_distance(1)
	_check(provider.cooldown_remaining(executed.id) == cooldown_after_confirm, "duplicate execution lookup must not commit cooldown twice")


func _test_sharkk_preview_state() -> void:
	var data := load("res://features/enemy_ai_node/enemy_data/sharkk.tres") as EnemyDataNodeAdapter
	_check(data != null, "sharkk adapter missing for preview state")
	if data == null: return
	data.reset_ai_provider()
	var provider := data.get_ai_provider() as EnemyAIController
	_check(provider != null, "sharkk provider missing for preview state")
	if provider == null: return
	if provider is SharkkEnemyAI:
		(provider as SharkkEnemyAI).charge_chance = 1.0
	data.set_ai_runtime_context({
		"enemy_hp": 40, "enemy_position": 6, "player_position": 2,
		"distance": 4, "round_number": 1, "battle_phase": 1,
	})
	var preview := data.get_action_for_distance(4)
	_check(preview != null, "Sharkk preview should produce an action")
	data.get_action_for_distance(4)
	data.set_ai_runtime_context({"battle_phase": 2})
	var confirmed := data.get_action_for_distance(4)
	_check(confirmed == preview, "Sharkk confirmed action must equal preview")
	data.set_ai_runtime_context({"battle_phase": 1, "round_number": 2})
	var next_preview := data.get_action_for_distance(4)
	_check(next_preview != null, "Sharkk should produce a next-round preview after confirmation")
	if preview != null and preview.id in [&"sharkk_prepare_charge", &"trained_sharkk_prepare_charge"]:
		_check(next_preview.id in [&"sharkk_charge", &"trained_sharkk_charge_d1", &"trained_sharkk_charge_d2", &"trained_sharkk_charge_d3", &"trained_sharkk_charge_d4"],
			"charge preparation state changes only after confirmed execution")


func _test_main_scene_load() -> void:
	# The standard (non-Mono) test binary cannot load the inherited C# scene.
	# Verify the additive inheritance/bridge declarations textually here; the
	# Mono build/scene load is covered by dotnet + project integration testing.
	var file := FileAccess.open("res://features/enemy_ai_node/scenes/main_enemy_ai_node.tscn", FileAccess.READ)
	_check(file != null, "inherited Main scene file cannot open")
	if file == null: return
	var text := file.get_as_text()
	_check(text.contains("res://Scenes/game_scene/game.tscn"), "Main scene does not inherit game.tscn")
	_check(text.contains("EnemyAINodeBridge"), "Main scene has no EnemyAINodeBridge declaration")


func _test_main_presets() -> void:
	var presets := {
		"main_enemy_ai_node.tscn": "boom_rules.tres",
		"main_rocky_boom.tscn": "rocky_boom_rules.tres",
		"main_sharkk.tscn": "sharkk_rules.tres",
		"main_core00_phase_one.tscn": "core00_phase_one_rules.tres",
		"main_core00_phase_two.tscn": "core00_phase_two_rules.tres",
	}
	for file_name in presets:
		var path := "res://features/enemy_ai_node/scenes/%s" % file_name
		var file := FileAccess.open(path, FileAccess.READ)
		_check(file != null, "missing configured Main preset: " + file_name)
		if file != null:
			_check(file.get_as_text().contains(presets[file_name]), "Main preset has wrong rules: " + file_name)
