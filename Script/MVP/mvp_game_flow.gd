class_name MvpGameFlow
extends Control

const ThemeFactory = preload("res://Script/MVP/mvp_theme_factory.gd")

@export_group("场景 - 可在 Inspector 替换")
@export var main_menu_scene: PackedScene
@export var map_scene: PackedScene
@export var exploration_scene: PackedScene
@export var build_scene: PackedScene
@export var battle_scene: PackedScene
@export var reward_scene: PackedScene
@export var game_over_scene: PackedScene
@export var victory_scene: PackedScene
@export var card_catalog: Resource

@export_group("调试")
@export var show_runtime_debug_panel: bool = true
@export_enum("menu", "map", "exploration", "build", "battle", "boss") var debug_start_scene: String = "menu"
@export_range(0, 3, 1) var debug_start_progress: int = 0
@export var force_new_run_on_start: bool = false

var run_state: Node
var current_scene: Control
var scene_holder: Control
var debug_panel: PanelContainer

func _ready() -> void:
	add_to_group("mvp_flow")
	theme = ThemeFactory.create_theme()
	run_state = get_node("/root/MvpState")
	scene_holder = Control.new()
	scene_holder.name = "SceneHolder"
	add_child(scene_holder)
	scene_holder.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if force_new_run_on_start:
		run_state.new_run()
	if debug_start_scene != "menu":
		run_state.map_progress = debug_start_progress
	_show_debug_start()
	if show_runtime_debug_panel:
		_create_debug_panel()

func _show_debug_start() -> void:
	match debug_start_scene:
		"map":
			show_map()
		"exploration":
			show_exploration()
		"build":
			show_build({"return_to": "map"})
		"battle":
			show_battle({"boss": false, "event_id": "debug_enemy"})
		"boss":
			show_battle({"boss": true, "event_id": "debug_boss"})
		_:
			show_menu()

func show_scene(packed: PackedScene, context: Dictionary = {}) -> void:
	if packed == null:
		push_error("MVP 场景未配置。")
		return
	if current_scene != null:
		current_scene.queue_free()
		current_scene = null
	var instance := packed.instantiate()
	if not instance.has_method("configure"):
		push_error("场景根节点必须继承 MvpSceneBase。")
		instance.queue_free()
		return
	current_scene = instance
	current_scene.call("configure", run_state, card_catalog, context)
	current_scene.connect("scene_command", _on_scene_command)
	scene_holder.add_child(current_scene)

func show_menu() -> void:
	show_scene(main_menu_scene)

func show_map() -> void:
	show_scene(map_scene)

func show_exploration(context: Dictionary = {}) -> void:
	show_scene(exploration_scene, context)

func show_build(context: Dictionary = {}) -> void:
	show_scene(build_scene, context)

func show_battle(context: Dictionary = {}) -> void:
	show_scene(battle_scene, context)

func show_reward(context: Dictionary = {}) -> void:
	show_scene(reward_scene, context)

func show_game_over() -> void:
	show_scene(game_over_scene)

func show_victory() -> void:
	show_scene(victory_scene)

func _on_scene_command(command: String, payload: Dictionary) -> void:
	match command:
		"new_run":
			run_state.new_run()
			show_map()
		"continue":
			if run_state.load_game():
				show_map()
			else:
				run_state.new_run()
				show_map()
		"menu":
			show_menu()
		"map":
			show_map()
		"exploration":
			show_exploration(payload)
		"build":
			show_build(payload)
		"battle":
			show_battle(payload)
		"reward":
			show_reward(payload)
		"game_over":
			show_game_over()
		"victory":
			show_victory()
		"quit":
			get_tree().quit()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and event.keycode == KEY_F1 and debug_panel != null:
		debug_panel.visible = not debug_panel.visible

func _create_debug_panel() -> void:
	var layer := CanvasLayer.new()
	layer.layer = 100
	add_child(layer)
	debug_panel = PanelContainer.new()
	debug_panel.name = "RuntimeDebugPanel"
	debug_panel.theme = ThemeFactory.create_theme()
	var style: StyleBoxFlat = ThemeFactory.panel_style(Color(0.018, 0.04, 0.06, 0.98), Color("4de4ff"), 16, 1, 18)
	debug_panel.add_theme_stylebox_override("panel", style)
	layer.add_child(debug_panel)
	_position_debug_panel()
	get_viewport().size_changed.connect(_position_debug_panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 9)
	debug_panel.add_child(box)
	var title := Label.new()
	title.text = "运行时调试台"
	title.add_theme_font_size_override("font_size", 24)
	title.add_theme_color_override("font_color", Color("79edff"))
	box.add_child(title)
	var hint := Label.new()
	hint.text = "F1 隐藏｜战斗页可切换并单步执行 AI"
	hint.add_theme_font_size_override("font_size", 14)
	hint.add_theme_color_override("font_color", Color("88a8b6"))
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	box.add_child(hint)
	box.add_child(HSeparator.new())
	var status := Label.new()
	status.name = "DebugStatus"
	status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	box.add_child(status)
	var actions := [
		["主菜单", func(): show_menu()],
		["地图", func(): show_map()],
		["探索当前节点", func(): show_exploration()],
		["构筑", func(): show_build({"return_to": "map"})],
		["普通战斗", func(): show_battle({"boss": false, "event_id": "debug_enemy"})],
		["Boss战斗", func(): show_battle({"boss": true, "event_id": "debug_boss"})],
		["满血", func(): run_state.heal_full(); run_state.save_game(); _refresh_debug_status()],
		["进度 +1", func(): run_state.map_progress = mini(3, run_state.map_progress + 1); run_state.state_changed.emit(); _refresh_debug_status()],
		["构筑尺寸 +1", func(): run_state.board_level = mini(2, run_state.board_level + 1); run_state.state_changed.emit(); _refresh_debug_status()],
		["保存", func(): run_state.save_game(); _refresh_debug_status()],
		["读取", func(): run_state.load_game(); show_map()],
	]
	for item in actions:
		var button := Button.new()
		button.text = item[0]
		button.custom_minimum_size.y = 43
		button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		button.pressed.connect(item[1])
		box.add_child(button)
	run_state.state_changed.connect(_refresh_debug_status)
	_refresh_debug_status()

func _position_debug_panel() -> void:
	if debug_panel == null:
		return
	var viewport_size := get_viewport().get_visible_rect().size
	debug_panel.size = Vector2(340, maxf(680.0, viewport_size.y - 40.0))
	debug_panel.position = Vector2(maxf(10.0, viewport_size.x - debug_panel.size.x - 20.0), 20.0)

func _refresh_debug_status() -> void:
	if debug_panel == null:
		return
	var status := debug_panel.get_node_or_null("VBoxContainer/DebugStatus") as Label
	if status == null:
		status = debug_panel.find_child("DebugStatus", true, false) as Label
	if status != null:
		var board_dimensions: Vector2i = run_state.board_size()
		status.text = "HP %d/%d  金钱 %d\n地图进度 %d  构筑 %d×%d\n存档：%s" % [
			run_state.current_hp, run_state.max_hp, run_state.money,
			run_state.map_progress, board_dimensions.x, board_dimensions.y,
			"有" if run_state.has_save() else "无"
		]
