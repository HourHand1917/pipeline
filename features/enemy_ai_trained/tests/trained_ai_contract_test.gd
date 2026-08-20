extends Node

const ROOT := "res://features/enemy_ai_trained/"
const SCENE_ROOT := ROOT + "scenes/"
const ACTION_ROOT := ROOT + "resources/actions/"
const DATA_ROOT := ROOT + "enemy_data/"

var _checks := 0
var _failures: Array[String] = []


func _ready() -> void:
	call_deferred(&"_run")


func _run() -> void:
	_test_resource_contract()
	_test_boom_matrix()
	_test_sharkk_exact_charge()
	_test_preview_and_hidden_information_invariance()
	_test_observable_memory()
	_test_core_initial_sniper_cooldown()
	_test_seeded_randomness()
	_test_enemy_data_presets()
	_test_action_configuration()
	if _checks != 237:
		_failures.append("contract expected 237 checks, got %d" % _checks)
	if _failures.is_empty():
		print("TRAINED_AI_CONTRACT_TEST_PASS checks=%d" % _checks)
		get_tree().quit(0)
	else:
		for failure in _failures:
			push_error(failure)
		print("TRAINED_AI_CONTRACT_TEST_FAIL checks=%d failures=%d" % [_checks, _failures.size()])
		get_tree().quit(1)


func _test_resource_contract() -> void:
	var expected_scenes := {"boom_trained_ai.tscn": 4, "rocky_trained_ai.tscn": 6, "sharkk_trained_ai.tscn": 9, "core00_body_trained_ai.tscn": 8}
	for file_name in expected_scenes:
		var scene := load(SCENE_ROOT + file_name) as PackedScene
		_check(scene != null, "scene loads: %s" % file_name)
		if scene == null:
			continue
		var ai := scene.instantiate() as EnemyAIController
		_check(ai != null, "scene root is EnemyAIController: %s" % file_name)
		if ai != null:
			_check(ai.actions.size() == int(expected_scenes[file_name]), "action count: %s" % file_name)
			var unique := {}
			for action in ai.actions:
				_check(action != null, "non-null action: %s" % file_name)
				if action != null:
					_check(not unique.has(action.id), "unique action id: %s" % action.id)
					unique[action.id] = true
		ai.free()
	var boom_1 := load(ACTION_ROOT + "boom_attack_1.tres") as EnemyActionData
	var boom_2 := load(ACTION_ROOT + "boom_attack_2.tres") as EnemyActionData
	_check(_single_damage(boom_1) == 4 and boom_1.min_range == 1 and boom_1.max_range == 1, "Boom distance1 damage4")
	_check(_single_damage(boom_2) == 2 and boom_2.min_range == 2 and boom_2.max_range == 2, "Boom distance2 damage2")
	var rocky_close := load(ACTION_ROOT + "rocky_close_attack.tres") as EnemyActionData
	var rocky_mid := load(ACTION_ROOT + "rocky_mid_attack.tres") as EnemyActionData
	_check(_single_damage(rocky_close) == 6 and rocky_close.cooldown_turns == 2, "Rocky close6 CD2")
	_check(_single_damage(rocky_mid) == 4 and rocky_mid.min_range == 1 and rocky_mid.max_range == 3, "Rocky mid1-3 damage4")
	var shark_attack := load(ACTION_ROOT + "sharkk_attack.tres") as EnemyActionData
	_check(_single_damage(shark_attack) == 5 and shark_attack.min_range == 1 and shark_attack.max_range == 2, "Sharkk attack1-2 damage5")
	var pulse := load(ACTION_ROOT + "core_body_pulse.tres") as EnemyActionData
	_check(_damage_effect_count(pulse, 2) == 3 and pulse.cooldown_turns == 2, "Core pulse is 2x3 CD2")


func _test_boom_matrix() -> void:
	var ai := _new_ai("boom_trained_ai.tscn")
	var at_one := ai.select_action(_context(3, 4, 5, 1))
	var at_two := ai.select_action(_context(2, 4, 5, 2))
	_check(at_one != null and at_one.id == &"trained_boom_attack_1", "Boom d1 selects damage4")
	_check(at_two != null and at_two.id == &"trained_boom_attack_2", "Boom d2 selects damage2")
	ai.free()


func _test_sharkk_exact_charge() -> void:
	for distance in range(1, 5):
		var ai := _new_ai("sharkk_trained_ai.tscn") as SharkkTrainedAI
		ai.charge_state = SharkkTrainedAI.ChargeState.PREPARED
		var start := 2
		var context := _context(start, start + distance, 40, distance)
		var action := ai.select_action(context)
		_check(action != null and action.id == StringName("trained_sharkk_charge_d%d" % distance), "Sharkk d%d selects exact charge" % distance)
		if action != null:
			var result := _simulate_charge(action, start, start + distance, 9)
			_check(int(result.player) == start + 5, "Sharkk d%d lands player at S0+5" % distance)
			_check(int(result.damage) == 10, "Sharkk d%d deals 10" % distance)
			_check(action.special_effect == &"", "Sharkk d%d does not call wall-push special" % distance)
			ai.confirm_action(action, context)
			var next_action := ai.select_action(_context(int(result.enemy), int(result.player), 40, distance + 1))
			_check(next_action != null and next_action.id == &"trained_sharkk_stunned", "Sharkk d%d is stunned next turn" % distance)
		ai.free()
	var boundary_ai := _new_ai("sharkk_trained_ai.tscn") as SharkkTrainedAI
	boundary_ai.charge_state = SharkkTrainedAI.ChargeState.PREPARED
	var illegal_action := boundary_ai.select_action(_context(5, 8, 40, 1))
	_check(illegal_action == null or not String(illegal_action.id).begins_with("trained_sharkk_charge_d"), "Sharkk rejects out-of-bounds fifth cell")
	_check(boundary_ai.cooldown_remaining(&"trained_sharkk_charge_d3") == 0, "illegal charge does not start cooldown")
	boundary_ai.charge_state = SharkkTrainedAI.ChargeState.PREPARED
	var d5_action := boundary_ai.select_action(_context(2, 7, 40, 2))
	_check(d5_action == null or not String(d5_action.id).begins_with("trained_sharkk_charge_d"), "Sharkk charge cannot hit distance5")
	boundary_ai.free()


func _test_preview_and_hidden_information_invariance() -> void:
	var ai := _new_ai("rocky_trained_ai.tscn")
	var context := _context(7, 4, 20, 3)
	context.damage_taken_last_turn = 8
	context.last_player_action_type = &"ranged"
	var first := ai.select_action(context)
	for _index in range(100):
		var preview := ai.select_action(context)
		_check(preview != null and first != null and preview.id == first.id, "100 UI previews do not reroll intent")
	ai.free()
	var left := _new_ai("sharkk_trained_ai.tscn", 9981)
	var right := _new_ai("sharkk_trained_ai.tscn", 9981)
	var public_a := _context(2, 5, 40, 2)
	var public_b := _context(2, 5, 40, 2)
	public_a.metadata = {"hidden_deck": ["four_grip_coil_rifle"], "upgraded": false, "selected_command": "attack"}
	public_b.metadata = {"hidden_deck": ["finger_knuckle_striker"], "upgraded": true, "selected_command": "defend"}
	var action_a := left.select_action(public_a)
	var action_b := right.select_action(public_b)
	_check(action_a != null and action_b != null and action_a.id == action_b.id, "hidden deck/upgrade/command cannot change intent")
	left.free()
	right.free()


func _test_observable_memory() -> void:
	var ai := _new_ai("sharkk_trained_ai.tscn") as AdaptiveEnemyAIController
	for round_number in range(1, 5):
		var context := _context(2, 7, 40, round_number)
		context.damage_taken_last_turn = 6
		context.last_player_action_type = &"ranged"
		ai.confirm_observable_history(context)
	var metrics := ai.observable_metrics(_context(2, 7, 40, 5))
	_check(float(metrics.far_damage) > float(metrics.mid_damage), "far damage learned from settled history")
	_check(float(metrics.ranged_rate) >= 0.75, "ranged pressure inferred from settled actions")
	_check(ai.history_snapshot().size() <= 6, "observable memory capped at six rounds")
	ai.free()


func _test_core_initial_sniper_cooldown() -> void:
	var ai := _new_ai("core00_body_trained_ai.tscn")
	var first := ai.select_action(_context(2, 8, 50, 1))
	_check(first == null or first.id != &"trained_core_body_sniper", "Core sniper starts unavailable")
	_check(ai.cooldown_remaining(&"trained_core_body_sniper") == 4, "Core sniper initial remaining cooldown is4")
	for round_number in range(2, 6):
		ai.select_action(_context(2, 8, 50, round_number))
	_check(ai.cooldown_remaining(&"trained_core_body_sniper") == 0, "Core sniper ready after four turns")
	ai.free()


func _test_seeded_randomness() -> void:
	var outcomes := {}
	for seed in range(1, 33):
		var ai := _new_ai("rocky_trained_ai.tscn", seed)
		var action := ai.select_action(_context(7, 4, 20, 1))
		if action != null:
			outcomes[action.id] = true
		ai.free()
	_check(outcomes.size() >= 2, "different battle seeds preserve weighted randomness")


func _test_enemy_data_presets() -> void:
	var expected := {"boom_trained.tres": [8, &"boom"], "rocky_trained.tres": [20, &"rocky"], "sharkk_trained.tres": [40, &"sharkk"], "core00_body_trained.tres": [50, &"body"]}
	for file_name in expected:
		var data := load(DATA_ROOT + file_name) as EnemyDataNodeAdapter
		_check(data != null, "EnemyData loads: %s" % file_name)
		_check(data != null and data.max_hp == int(expected[file_name][0]), "EnemyData hp: %s" % file_name)
		_check(data != null and data.role == StringName(expected[file_name][1]), "EnemyData role: %s" % file_name)
		var provider := data.instantiate_ai_provider() if data != null else null
		_check(provider is AdaptiveEnemyAIController, "EnemyData provider: %s" % file_name)
		if provider != null: provider.free()


func _test_action_configuration() -> void:
	var boom_move := load(ACTION_ROOT + "boom_advance_2.tres") as EnemyActionData
	_check(boom_move.cooldown_turns == 2, "Boom advance2 CD2")
	var rocky_move := load(ACTION_ROOT + "rocky_advance_2.tres") as EnemyActionData
	var rocky_defend := load(ACTION_ROOT + "rocky_defend.tres") as EnemyActionData
	var rocky_retreat := load(ACTION_ROOT + "rocky_retreat.tres") as EnemyActionData
	_check(rocky_move.cooldown_turns == 2, "Rocky advance2 CD2")
	_check(rocky_defend.cooldown_turns == 2, "Rocky defend CD2")
	_check(rocky_defend.effects.size() == 2, "Rocky defend moves and shields")
	_check(rocky_retreat.min_range == 2, "Rocky retreat starts at distance2")
	_check(rocky_retreat.cooldown_turns == 3, "Rocky retreat CD3")
	var sand := load(ACTION_ROOT + "sharkk_sand_retreat.tres") as EnemyActionData
	_check(sand.min_range == 2 and sand.max_range == 3, "Sharkk sand range2-3")
	_check(sand.cooldown_turns == 2, "Sharkk sand CD2")
	var sniper := load(ACTION_ROOT + "core_body_sniper.tres") as EnemyActionData
	var gunstock := load(ACTION_ROOT + "core_body_gunstock.tres") as EnemyActionData
	var pulse := load(ACTION_ROOT + "core_body_pulse.tres") as EnemyActionData
	var blink := load(ACTION_ROOT + "core_body_teleport_guard.tres") as EnemyActionData
	var disable := load(ACTION_ROOT + "core_body_disable.tres") as EnemyActionData
	_check(sniper.min_range == 6 and sniper.max_range == 12, "Core sniper range6-12")
	_check(sniper.cooldown_turns == 4, "Core sniper CD4")
	_check(gunstock.min_range == 1 and gunstock.max_range == 1, "Core gunstock range1")
	_check(gunstock.cooldown_turns == 2, "Core gunstock CD2")
	_check(pulse.min_range == 3 and pulse.max_range == 4, "Core pulse range3-4")
	_check(blink.special_effect == &"move_behind_player", "Core blink uses supported special")
	_check(disable.min_range == 3, "Core disable starts at distance3")
	var seeded := _new_ai("rocky_trained_ai.tscn", 424242) as AdaptiveEnemyAIController
	_check(seeded.battle_seed_snapshot() == 424242, "tests can lock battle seed")
	seeded.free()


func _new_ai(scene_file: String, seed: int = 123456) -> EnemyAIController:
	var scene := load(SCENE_ROOT + scene_file) as PackedScene
	var ai := scene.instantiate() as EnemyAIController
	if ai is AdaptiveEnemyAIController:
		(ai as AdaptiveEnemyAIController).lock_test_seed(seed)
	else:
		ai.reset_ai()
	return ai


func _context(enemy_position: int, player_position: int, hp: int, round_number: int) -> EnemyAIContext:
	var context := EnemyAIContext.new()
	context.enemy_id = &"test"
	context.enemy_hp = hp
	context.enemy_max_hp = hp
	context.enemy_position = enemy_position
	context.player_position = player_position
	context.distance = absi(player_position - enemy_position)
	context.round_number = round_number
	context.player_hp = 30
	context.player_max_hp = 30
	context.player_has_unlit_cell = true
	return context


func _single_damage(action: EnemyActionData) -> int:
	if action == null: return -1
	for effect in action.effects:
		if effect != null and String(effect.call(&"type_key")) == "damage": return int(effect.amount)
	return -1


func _damage_effect_count(action: EnemyActionData, amount: int) -> int:
	var count := 0
	if action == null: return count
	for effect in action.effects:
		if effect != null and String(effect.call(&"type_key")) == "damage" and int(effect.amount) == amount: count += 1
	return count


func _simulate_charge(action: EnemyActionData, enemy_start: int, player_start: int, cell_count: int) -> Dictionary:
	var enemy := enemy_start
	var player := player_start
	var damage := 0
	for effect in action.effects:
		var type_key := String(effect.call(&"type_key"))
		var amount := int(effect.amount)
		var target := int(effect.target)
		match type_key:
			"move_toward_opponent":
				if target == 1:
					for _step in range(amount):
						var candidate := enemy + signi(player - enemy)
						if candidate == player or candidate < 1 or candidate > cell_count: break
						enemy = candidate
			"move_away_from_opponent":
				if target == 0:
					for _step in range(amount):
						var candidate := player + signi(player - enemy)
						if candidate < 1 or candidate > cell_count: break
						player = candidate
			"damage":
				if target == 0: damage += amount
	return {"enemy": enemy, "player": player, "damage": damage}


func _check(condition: bool, message: String) -> void:
	_checks += 1
	if not condition: _failures.append(message)
