extends Node

## Pure GDScript regression suite for EnermyMannager.
## Run this scene headlessly; no original C# battle scene is instantiated.

const MANAGER_SCRIPT = preload("res://features/enemy_ai/scripts/enermy_mannager.gd")
const BOOM_PROFILE = preload("res://features/enemy_ai/profiles/boom_profile.tres")
const ROCKY_PROFILE = preload("res://features/enemy_ai/profiles/rocky_profile.tres")
const SHARKK_PROFILE = preload("res://features/enemy_ai/profiles/sharkk_profile.tres")

var _failures: Array[String] = []
var _checks: int = 0


func _ready() -> void:
	print("ENEMY_AI_TESTS: health_and_shield")
	_test_health_and_shield()
	print("ENEMY_AI_TESTS: distance_and_boundaries")
	_test_distance_directions_and_boundaries()
	print("ENEMY_AI_TESTS: stable_random")
	_test_same_state_does_not_reroll()
	print("ENEMY_AI_TESTS: boom")
	_test_boom_rules()
	print("ENEMY_AI_TESTS: rocky")
	_test_rocky_rules_and_cooldowns()
	print("ENEMY_AI_TESTS: sharkk")
	_test_sharkk_rules()
	print("ENEMY_AI_TESTS: isolation")
	_test_instance_isolation()
	print("ENEMY_AI_TESTS: null_safety")
	_test_missing_profile_is_safe()
	print("ENEMY_AI_TESTS: stress")
	_test_long_random_stress()

	if _failures.is_empty():
		print("ENEMY_AI_TESTS: PASS (%d checks)" % _checks)
		get_tree().quit(0)
	else:
		printerr("ENEMY_AI_TESTS: FAIL (%d failures / %d checks)" % [_failures.size(), _checks])
		for failure: String in _failures:
			printerr("  - %s" % failure)
		get_tree().quit(1)


func _test_health_and_shield() -> void:
	var manager = _make_manager(BOOM_PROFILE, 6, 2, 20, 7, 4)
	_check_equal(manager.take_damage(3), 3, "shield: reported absorbed damage")
	var state: Dictionary = manager.get_state()
	_check_equal(state["shield"], 1, "shield: three damage leaves one shield")
	_check_equal(state["enemy_hp"], 7, "shield: HP is untouched while shield remains")

	manager.take_damage(4)
	state = manager.get_state()
	_check_equal(state["shield"], 0, "shield: overflow consumes remaining shield")
	_check_equal(state["enemy_hp"], 4, "shield: overflow reaches HP")
	_check_equal(state["damage_received_this_player_turn"], 7, "HP: received damage is accumulated")

	manager.add_shield(7)
	_check_equal(manager.get_state()["shield"], 7, "shield: add_shield is managed locally")
	manager.heal(999)
	_check_equal(manager.get_state()["enemy_hp"], 7, "HP: healing clamps to max HP")
	manager.take_damage(999)
	_check_equal(manager.get_state()["enemy_hp"], 0, "HP: damage clamps at zero")
	manager.free()


func _test_distance_directions_and_boundaries() -> void:
	var manager = _make_manager(BOOM_PROFILE, 6, 2)
	_check_equal(manager.get_distance(), 4, "distance: enemy right of player")
	manager.set_debug_snapshot({"enemy_position": 2, "player_position": 6})
	_check_equal(manager.get_distance(), 4, "distance: enemy left of player is symmetric")

	manager.set_debug_snapshot({"enemy_position": -100, "player_position": 999})
	var state: Dictionary = manager.get_state()
	_check_equal(state["enemy_position"], 1, "boundary: enemy clamps to first cell")
	_check_equal(state["player_position"], 7, "boundary: player clamps to final cell")

	manager.set_debug_snapshot({"enemy_position": 7, "player_position": 1})
	var right_to_left: Dictionary = manager.refresh_intent_preview()
	_check_between(int(right_to_left["enemy_to"]), 1, 7, "boundary: right-to-left move remains legal")
	_check_true(int(right_to_left["enemy_to"]) < 7, "direction: enemy approaches left-side player")

	manager.set_debug_snapshot({"enemy_position": 1, "player_position": 7})
	var left_to_right: Dictionary = manager.refresh_intent_preview()
	_check_between(int(left_to_right["enemy_to"]), 1, 7, "boundary: left-to-right move remains legal")
	_check_true(int(left_to_right["enemy_to"]) > 1, "direction: enemy approaches right-side player")
	manager.free()


func _test_same_state_does_not_reroll() -> void:
	# Equalize the two Boom approach priorities, creating a genuine weighted
	# random choice. The state fingerprint must keep that choice stable.
	var random_profile = BOOM_PROFILE.duplicate(true)
	var advance_one = random_profile.find_action(&"boom_advance_1")
	var advance_two = random_profile.find_action(&"boom_advance_2")
	advance_one.base_priority = 55
	advance_two.base_priority = 55

	var manager = _make_manager(random_profile, 5, 2)
	manager.random_seed_override = 87531
	var first: Dictionary = manager.refresh_intent_preview()
	_check_true(first["id"] in [&"boom_advance_1", &"boom_advance_2"], "stable random: test has two valid random choices")
	for index in range(200):
		var repeated: Dictionary = manager.notify_player_action()
		_check_equal(repeated["id"], first["id"], "stable random: unchanged state refresh %d" % index)
		_check_equal(repeated["enemy_to"], first["enemy_to"], "stable random: unchanged target %d" % index)
	manager.free()


func _test_boom_rules() -> void:
	var manager = _make_manager(BOOM_PROFILE, 3, 2)
	var intent: Dictionary = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"boom_attack", "Boom: distance 1 attacks")
	_check_equal(intent["damage"], 3, "Boom: default configurable attack damage")

	manager.set_debug_snapshot({"enemy_position": 4, "player_position": 2})
	intent = manager.refresh_intent_preview()
	_check_true(
		intent["id"] in [&"boom_attack", &"boom_advance_1"],
		"Boom: distance 2 stably rolls attack or one-cell advance"
	)

	manager.set_debug_snapshot({"enemy_position": 6, "player_position": 2})
	intent = manager.refresh_intent_preview()
	_check_true(intent["id"] in [&"boom_advance_1", &"boom_advance_2"], "Boom: long range advances")
	_check_true(int(intent["enemy_to"]) < 6, "Boom: advance moves toward player")
	manager.free()


func _test_rocky_rules_and_cooldowns() -> void:
	var manager = _make_manager(ROCKY_PROFILE, 3, 2, 20)
	var intent: Dictionary = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"rocky_close_attack", "Rocky: distance 1 selects heavy attack")
	_check_equal(intent["damage"], 6, "Rocky: close attack deals 6")
	var result: Dictionary = manager.advance_enemy_turn_for_test()
	_check_equal(result["id"], &"rocky_close_attack", "Rocky: close attack executes")
	_check_equal(manager.get_state()["player_hp"], 14, "Rocky: close attack removes 6 player HP")
	_check_equal(manager.get_cooldown(&"rocky_close_attack"), 2, "Rocky: close attack starts cooldown 2")
	_check_equal(manager.refresh_intent_preview()["id"], &"rocky_mid_attack", "Rocky: cooling heavy attack falls back to mid attack")

	manager.advance_enemy_turn_for_test()
	_check_equal(manager.get_cooldown(&"rocky_close_attack"), 1, "cooldown: one complete enemy turn leaves 1")
	manager.advance_enemy_turn_for_test()
	_check_equal(manager.get_cooldown(&"rocky_close_attack"), 0, "cooldown: second complete enemy turn reaches 0")
	_check_equal(manager.refresh_intent_preview()["id"], &"rocky_close_attack", "Rocky: heavy attack is available again")

	manager.configure_manual_state(ROCKY_PROFILE, 4, 2, 20)
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"rocky_mid_attack", "Rocky: distance 2 uses mid attack")
	_check_equal(intent["damage"], 4, "Rocky: mid attack deals 4")

	# Force only the shield-retreat defensive candidate to remain available.
	manager.set_debug_snapshot({
		"enemy_position": 4,
		"player_position": 2,
		"damage_received_this_player_turn": 7,
		"cooldowns": {&"rocky_retreat": 1},
	})
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"rocky_defend_retreat", "Rocky: reactive shield-retreat can be selected")
	_check_true(intent["is_forced"], "Rocky: damage-threshold defense is marked forced")
	manager.advance_enemy_turn_for_test()
	var state: Dictionary = manager.get_state()
	_check_equal(state["enemy_position"], 5, "Rocky: shield-retreat moves away 1")
	_check_equal(state["shield"], 7, "Rocky: shield-retreat grants 7 shield")
	_check_equal(manager.get_cooldown(&"rocky_defend_retreat"), 2, "Rocky: shield-retreat starts cooldown 2")

	# Force only the plain two-cell retreat candidate to remain available.
	manager.configure_manual_state(ROCKY_PROFILE, 4, 2, 20)
	manager.set_debug_snapshot({
		"damage_received_this_player_turn": 7,
		"cooldowns": {&"rocky_defend_retreat": 1},
	})
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"rocky_retreat", "Rocky: reactive plain retreat can be selected")
	manager.advance_enemy_turn_for_test()
	state = manager.get_state()
	_check_equal(state["enemy_position"], 6, "Rocky: plain retreat moves away 2")
	_check_equal(manager.get_cooldown(&"rocky_retreat"), 3, "Rocky: plain retreat starts cooldown 3")
	manager.free()


func _test_sharkk_rules() -> void:
	var manager = _make_manager(SHARKK_PROFILE, 4, 2, 30)
	manager.set_debug_snapshot({"damage_received_this_player_turn": 10})
	var intent: Dictionary = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"sharkk_dust_retreat", "Sharkk: 10 received damage forces dust retreat")
	_check_true(intent["is_forced"], "Sharkk: forced dust is exposed to UI")
	manager.advance_enemy_turn_for_test()
	var state: Dictionary = manager.get_state()
	_check_equal(state["enemy_position"], 6, "Sharkk: dust retreat moves away 2")
	_check_equal(manager.get_cooldown(&"sharkk_dust_retreat"), 2, "Sharkk: dust retreat starts cooldown 2")

	# At distance 3, prepare has higher priority than normal one-cell advance.
	manager.configure_manual_state(SHARKK_PROFILE, 6, 3, 30)
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"sharkk_prepare_dash", "Sharkk: prepares dash at a valid distance")
	manager.advance_enemy_turn_for_test()
	state = manager.get_state()
	_check_equal(state["enemy_position"], 7, "Sharkk: preparation retreats 1")
	_check_true(state["prepared_dash"], "Sharkk: preparation flag is stored")
	_check_equal(manager.refresh_intent_preview()["id"], &"sharkk_dash", "Sharkk: prepared valid dash becomes forced intent")

	var dash: Dictionary = manager.advance_enemy_turn_for_test()
	state = manager.get_state()
	_check_equal(dash["damage"], 10, "Sharkk: dash deals 10")
	_check_equal(state["player_hp"], 20, "Sharkk: dash removes 10 player HP")
	_check_equal(state["enemy_position"], 3, "Sharkk: dash ends at player's old cell")
	_check_equal(state["player_position"], 2, "Sharkk: dash pushes player one cell")
	_check_equal(state["stunned_turns"], 1, "Sharkk: dash applies one self-stun turn")
	_check_equal(manager.get_cooldown(&"sharkk_dash"), 3, "Sharkk: dash starts cooldown 3")
	_check_equal(manager.refresh_intent_preview()["id"], &"stunned", "Sharkk: next intent is stunned")
	manager.advance_enemy_turn_for_test()
	_check_equal(manager.get_state()["stunned_turns"], 0, "Sharkk: stun consumes exactly one enemy turn")
	_check_equal(manager.get_cooldown(&"sharkk_dash"), 2, "Sharkk: cooldown ticks during stunned turn")

	# A player on the boundary has no cell behind them. Even with the prepared
	# flag set, dash must not be chosen because the push destination is invalid.
	manager.configure_manual_state(SHARKK_PROFILE, 4, 1, 30)
	manager.set_debug_snapshot({"prepared_dash": true})
	intent = manager.refresh_intent_preview()
	_check_true(intent["id"] != &"sharkk_dash", "Sharkk: no dash when push space is missing")
	_check_between(int(intent["enemy_to"]), 1, 9, "Sharkk: fallback intent remains on board")
	manager.free()


func _test_instance_isolation() -> void:
	var first = _make_manager(ROCKY_PROFILE, 3, 2, 20)
	var second = _make_manager(ROCKY_PROFILE, 3, 2, 20)
	first.take_damage(5)
	first.advance_enemy_turn_for_test()
	var first_state: Dictionary = first.get_state()
	var second_state: Dictionary = second.get_state()
	_check_equal(first_state["enemy_hp"], 25, "isolation: first instance records its damage")
	_check_equal(second_state["enemy_hp"], 30, "isolation: second instance HP is untouched")
	_check_true(first.get_cooldown(&"rocky_close_attack") > 0, "isolation: first instance owns its cooldown")
	_check_equal(second.get_cooldown(&"rocky_close_attack"), 0, "isolation: second instance cooldown is untouched")
	first.free()
	second.free()


func _test_missing_profile_is_safe() -> void:
	var manager = MANAGER_SCRIPT.new()
	manager.show_debug_panel = false
	manager.configure_manual_state(null, 99, -20, 20)
	var state: Dictionary = manager.get_state()
	var intent: Dictionary = manager.refresh_intent_preview()
	_check_true(not state.is_empty(), "null safety: missing profile still returns state")
	_check_true(not intent.is_empty(), "null safety: missing profile returns wait intent")
	_check_equal(intent["reason"], "missing_profile", "null safety: missing profile is explained")
	_check_between(int(state["enemy_position"]), 1, int(state["cell_count"]), "null safety: enemy position stays legal")
	_check_between(int(state["player_position"]), 1, int(state["cell_count"]), "null safety: player position stays legal")
	manager.free()


func _test_long_random_stress() -> void:
	var profiles: Array = [BOOM_PROFILE, ROCKY_PROFILE, SHARKK_PROFILE]
	var managers: Array = []
	for profile in profiles:
		managers.append(_make_manager(profile, profile.start_cell, 2, 1000))

	var rng := RandomNumberGenerator.new()
	rng.seed = 733104
	const STEPS := 1200
	for step in range(STEPS):
		var manager = managers[step % managers.size()]
		var cell_count: int = manager.get_state()["cell_count"]
		var player_cell := rng.randi_range(1, cell_count)
		var enemy_cell := rng.randi_range(1, cell_count)
		if enemy_cell == player_cell:
			enemy_cell = 1 if player_cell == cell_count else player_cell + 1
		manager.set_debug_snapshot({
			"enemy_position": enemy_cell,
			"player_position": player_cell,
			"enemy_hp": manager.get_state()["max_hp"],
			"player_hp": 1000,
			"damage_received_this_player_turn": rng.randi_range(0, 14),
			"prepared_dash": rng.randi_range(0, 4) == 0,
		})
		var intent: Dictionary = manager.refresh_intent_preview()
		_check_true(not intent.is_empty(), "stress %d: intent is never null/empty" % step)
		_check_between(int(intent["enemy_to"]), 1, cell_count, "stress %d: planned enemy cell" % step)
		_check_between(int(intent["player_to"]), 1, cell_count, "stress %d: planned player cell" % step)
		if step % 3 == 0:
			manager.advance_enemy_turn_for_test()
		var state: Dictionary = manager.get_state()
		_check_between(int(state["enemy_position"]), 1, cell_count, "stress %d: actual enemy cell" % step)
		_check_between(int(state["player_position"]), 1, cell_count, "stress %d: actual player cell" % step)
		_check_true(int(state["distance"]) >= 0, "stress %d: distance is nonnegative" % step)
		var cooldowns: Dictionary = state["cooldowns"]
		for action_id: Variant in cooldowns:
			_check_true(int(cooldowns[action_id]) >= 0, "stress %d: cooldown %s is nonnegative" % [step, str(action_id)])

	for manager in managers:
		manager.free()


func _make_manager(
	profile,
	enemy_cell: int,
	player_cell: int,
	player_hp: int = 20,
	enemy_hp: int = -1,
	shield: int = -1
):
	var manager = MANAGER_SCRIPT.new()
	manager.show_debug_panel = false
	manager.debug_auto_refresh = false
	manager.configure_manual_state(profile, enemy_cell, player_cell, player_hp, enemy_hp, shield)
	return manager


func _check_true(condition: bool, label: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(label)


func _check_equal(actual: Variant, expected: Variant, label: String) -> void:
	_checks += 1
	if actual != expected:
		_failures.append("%s (expected %s, got %s)" % [label, str(expected), str(actual)])


func _check_between(actual: int, minimum: int, maximum: int, label: String) -> void:
	_checks += 1
	if actual < minimum or actual > maximum:
		_failures.append("%s (expected %d..%d, got %d)" % [label, minimum, maximum, actual])
