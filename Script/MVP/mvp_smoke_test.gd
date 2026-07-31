extends Node

const BoardModel = preload("res://Script/MVP/mvp_board_model.gd")
const SCENE_CASES := [
	["res://Scenes/MVP/main_menu.tscn", {}],
	["res://Scenes/MVP/world_map.tscn", {}],
	["res://Scenes/MVP/exploration.tscn", {"stage": 0, "reset_position": true}],
	["res://Scenes/MVP/exploration.tscn", {"stage": 2, "reset_position": true}],
	["res://Scenes/MVP/build.tscn", {"return_to": "map"}],
	["res://Scenes/MVP/battle.tscn", {"boss": false, "event_id": "smoke_enemy"}],
	["res://Scenes/MVP/battle.tscn", {"boss": true, "event_id": "smoke_boss"}],
	["res://Scenes/MVP/reward.tscn", {"kind": "chest", "event_id": "smoke_chest"}],
	["res://Scenes/MVP/game_over.tscn", {}],
	["res://Scenes/MVP/victory.tscn", {}],
]

func _ready() -> void:
	var state: Node = get_node("/root/MvpState")
	var catalog: Resource = load("res://Resource/MVP/card_catalog.tres")
	var expected_effects := {
		&"knockback_gun": "knockback",
		&"teleport_shoes": "teleport_through",
		&"rapier": "advance_attack",
		&"poison_sword": "poison",
		&"cleanse_pill": "cleanse",
		&"strength_glasses": "strength",
	}
	assert(catalog.ids().size() == 10)
	for card_id in expected_effects:
		var card: Resource = catalog.get_card(card_id)
		assert(card != null and card.effect_type == expected_effects[card_id], "新增卡牌配置错误：" + String(card_id))
	state.new_run(false)
	for scene_case in SCENE_CASES:
		var scene_path: String = scene_case[0]
		var context: Dictionary = scene_case[1]
		var packed: PackedScene = load(scene_path)
		assert(packed != null, "无法加载场景：" + scene_path)
		var instance: Node = packed.instantiate()
		instance.call("configure", state, catalog, context)
		add_child(instance)
		await get_tree().process_frame
		print("MVP_SMOKE_SCENE_OK: ", scene_path, " ", context)
		remove_child(instance)
		instance.free()
		await get_tree().process_frame
	var exploration_commands: Array = []
	var exploration_scene: PackedScene = load("res://Scenes/MVP/exploration.tscn")
	var exploration: Node = exploration_scene.instantiate()
	exploration.call("configure", state, catalog, {"stage": 0, "reset_position": true})
	exploration.connect("scene_command", func(command: String, payload: Dictionary): exploration_commands.append([command, payload]))
	add_child(exploration)
	await get_tree().process_frame
	exploration.set("player_x", 470.0)
	exploration.call("_refresh_player")
	exploration.call("_interact_nearest")
	assert(not exploration_commands.is_empty() and exploration_commands[0][0] == "reward")
	remove_child(exploration)
	exploration.free()
	state.new_run(false)
	var battle_commands: Array = []
	var battle_scene: PackedScene = load("res://Scenes/MVP/battle.tscn")
	var battle: Node = battle_scene.instantiate()
	battle.call("configure", state, catalog, {"boss": false, "event_id": "smoke_combat"})
	battle.connect("scene_command", func(command: String, payload: Dictionary): battle_commands.append([command, payload]))
	add_child(battle)
	await get_tree().process_frame
	assert(battle.get("enemy_ai_profiles").size() == 4)
	assert(battle.call("_available_ai_profiles").size() == 5)
	battle.set("enemy_hp", 200)
	battle.set("enemy_max_hp", 200)
	battle.set("enemy_shield", 0)
	var activate_card: Callable = func(card_id: StringName):
		var single_board: RefCounted = BoardModel.new(3, 2, catalog, [])
		assert(single_board.add_entry(card_id, Vector2i.ZERO, 0), "测试卡牌无法放入构筑板：" + String(card_id))
		battle.set("board", single_board)
		var lit_cells: Dictionary = {}
		for cell in single_board.cells_for_entry(single_board.entries[0]):
			lit_cells[cell] = true
		battle.set("charged_cells", lit_cells)
		battle.set("cooldowns", {})
		battle.call("_activate_card", 0)
	battle.set("player_debuffs", {"poison": {"amount": 2, "turns": 3}})
	activate_card.call(&"cleanse_pill")
	assert(battle.get("player_debuffs").is_empty())
	activate_card.call(&"strength_glasses")
	assert(battle.call("_strength_amount") == 2)
	battle.set("player_position", 1)
	battle.set("enemy_position", 3)
	var hp_before_gun: int = battle.get("enemy_hp")
	activate_card.call(&"knockback_gun")
	assert(battle.get("enemy_position") == 5)
	assert(battle.get("enemy_hp") == hp_before_gun - 6)
	battle.set("player_position", 1)
	battle.set("enemy_position", 2)
	activate_card.call(&"poison_sword")
	assert(battle.get("enemy_debuffs").has("poison"))
	var hp_before_poison: int = battle.get("enemy_hp")
	battle.call("_tick_enemy_debuffs")
	assert(battle.get("enemy_hp") == hp_before_poison - 2)
	battle.set("player_position", 1)
	battle.set("enemy_position", 4)
	var hp_before_rapier: int = battle.get("enemy_hp")
	activate_card.call(&"rapier")
	assert(battle.get("player_position") == 2)
	assert(battle.get("enemy_hp") == hp_before_rapier - 9)
	battle.set("player_position", 1)
	battle.set("enemy_position", 4)
	battle.call("_update_facing", false)
	activate_card.call(&"teleport_shoes")
	assert(battle.get("player_position") == 5)
	assert(battle.get("player_facing") == -1 and battle.get("enemy_facing") == 1)
	battle.call("_switch_ai_profile", 1)
	battle.set("player_position", 5)
	battle.set("enemy_position", 6)
	battle.call("_enemy_turn")
	assert(battle.get("enemy_position") == 7, "狙击手过近时应后退")
	battle.call("_switch_ai_profile", 2)
	battle.set("player_position", 1)
	battle.set("enemy_position", 3)
	battle.call("_enemy_turn")
	assert(battle.get("player_debuffs").has("poison"), "投毒 AI 应给玩家施加毒")
	battle.call("_damage_enemy", 999)
	await get_tree().process_frame
	assert(not battle_commands.is_empty() and battle_commands[0][0] == "reward")
	assert(state.is_event_opened("smoke_combat"))
	remove_child(battle)
	battle.free()
	var reward_scene: PackedScene = load("res://Scenes/MVP/reward.tscn")
	var reward: Node = reward_scene.instantiate()
	reward.call("configure", state, catalog, {"kind": "battle", "event_id": "smoke_combat"})
	add_child(reward)
	await get_tree().process_frame
	reward.call("_accept")
	assert(state.board_level >= 1 and state.money > 0)
	remove_child(reward)
	reward.free()
	var dimensions: Vector2i = state.board_size()
	var board: RefCounted = BoardModel.new(dimensions.x, dimensions.y, catalog, [])
	assert(board.add_entry(&"revolver", Vector2i(0, 0), 0))
	assert(not board.add_entry(&"shield", Vector2i(0, 0), 0))
	assert(board.add_entry(&"shield", Vector2i(2, 0), 0))
	assert(board.entry_index_at(Vector2i(1, 0)) == 0)
	assert(absi(6 - 1) == 5)
	state.set_build(board.entries)
	assert(state.save_game())
	assert(state.load_game())
	print("MVP_SMOKE_PASS: scenes=10 cards=", catalog.ids().size(), " ai_profiles=5 board_entries=", board.entries.size(), " distance=", absi(6 - 1))
	await get_tree().create_timer(0.1).timeout
	get_tree().quit()
