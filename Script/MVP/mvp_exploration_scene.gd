class_name MvpExplorationScene
extends "res://Script/MVP/mvp_scene_base.gd"

@export_group("探索参数 - Inspector 可调")
@export_range(50.0, 900.0, 10.0) var player_move_speed: float = 330.0
@export_range(40.0, 300.0, 5.0) var interaction_distance: float = 105.0
@export_range(600.0, 3000.0, 50.0) var world_width: float = 1300.0
@export var start_position_x: float = 110.0
@export var station_color: Color = Color("ef8f45")
@export var player_color: Color = Color("67efff")

var stage: int
var player_x: float
var lane: Control
var player_marker: ColorRect
var prompt_label: Label
var status_label: Label
var stations: Array[Dictionary] = []
var station_buttons: Array[Button] = []
var nearest_index: int = -1

func _ready() -> void:
	set_full_rect(self)
	stage = int(scene_context.get("stage", run_state.map_progress))
	player_x = clampf(run_state.last_exploration_x, 0.0, world_width)
	if scene_context.get("reset_position", false):
		player_x = start_position_x
	_build_ui()
	_build_stage()
	_refresh_player()

func _process(delta: float) -> void:
	var direction := 0.0
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		direction -= 1.0
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		direction += 1.0
	if direction != 0.0:
		player_x = clampf(player_x + direction * player_move_speed * delta, 0.0, world_width)
		run_state.last_exploration_x = player_x
		_refresh_player()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_E or event.keycode == KEY_ENTER:
			_interact_nearest()
		elif event.keycode == KEY_ESCAPE:
			send_command("map")

func _build_ui() -> void:
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("0a1218")
	add_child(background)
	add_screen_frame(self, player_color)
	var top := VBoxContainer.new()
	top.position = Vector2(60, 42)
	top.size = Vector2(1460, 150)
	add_child(top)
	top.add_child(make_title("探索 · %s" % _stage_name(), 38))
	status_label = Label.new()
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	status_label.add_theme_font_size_override("font_size", 19)
	top.add_child(status_label)
	prompt_label = Label.new()
	prompt_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	prompt_label.add_theme_font_size_override("font_size", 22)
	prompt_label.modulate = Color("7deeff")
	top.add_child(prompt_label)
	lane = Control.new()
	lane.position = Vector2(60, 220)
	lane.size = Vector2(1460, 650)
	add_child(lane)
	var lane_panel := Panel.new()
	lane_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var lane_style := StyleBoxFlat.new()
	lane_style.bg_color = Color("17232b")
	lane_style.border_color = Color("39505c")
	lane_style.set_border_width_all(3)
	lane_style.set_corner_radius_all(12)
	lane_panel.add_theme_stylebox_override("panel", lane_style)
	lane.add_child(lane_panel)
	var ground := ColorRect.new()
	ground.position = Vector2(0, 510)
	ground.size = Vector2(lane.size.x, 12)
	ground.color = Color("52717c")
	lane.add_child(ground)
	player_marker = ColorRect.new()
	player_marker.size = Vector2(58, 104)
	player_marker.color = player_color
	lane.add_child(player_marker)
	var actions := HBoxContainer.new()
	actions.position = Vector2(60, 930)
	actions.size = Vector2(1460, 80)
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.add_theme_constant_override("separation", 15)
	add_child(actions)
	actions.add_child(make_button("← A / 左移", func(): _nudge(-1)))
	actions.add_child(make_button("E / 互动", _interact_nearest))
	actions.add_child(make_button("D / 右移 →", func(): _nudge(1)))
	actions.add_child(make_button("地图", func(): send_command("map")))

func _build_stage() -> void:
	match stage:
		0:
			stations = [
				{"type": "chest", "name": "物资箱", "x": 470.0, "event": "chest_01"},
				{"type": "exit", "name": "通往巡逻区", "x": 1160.0},
			]
		1:
			stations = [
				{"type": "enemy", "name": "巡逻怪物", "x": 780.0, "event": "enemy_01"},
			]
		2:
			stations = [
				{"type": "campfire", "name": "篝火", "x": 390.0},
				{"type": "workbench", "name": "工作台", "x": 760.0},
				{"type": "exit", "name": "通往首领巢穴", "x": 1160.0},
			]
		3:
			stations = [
				{"type": "boss", "name": "管线守卫", "x": 850.0, "event": "boss_01"},
			]
	for index in range(stations.size()):
		var station := stations[index]
		var button := Button.new()
		button.text = String(station.name)
		button.custom_minimum_size = Vector2(190, 130)
		button.position = Vector2(_world_to_lane(float(station.x)) - 95, 350)
		button.size = Vector2(190, 130)
		var chosen := index
		button.pressed.connect(func(): _interact(chosen))
		lane.add_child(button)
		station_buttons.append(button)
	_refresh_status()

func _refresh_player() -> void:
	if player_marker == null:
		return
	player_marker.position = Vector2(_world_to_lane(player_x) - player_marker.size.x * 0.5, 406)
	nearest_index = -1
	var nearest_distance := INF
	for index in range(stations.size()):
		var distance := absf(player_x - float(stations[index].x))
		if distance < nearest_distance:
			nearest_distance = distance
			nearest_index = index
		station_buttons[index].disabled = distance > interaction_distance
	if nearest_index >= 0 and nearest_distance <= interaction_distance:
		prompt_label.text = "按 E 与「%s」互动（距离 %.0f）" % [stations[nearest_index].name, nearest_distance]
	else:
		prompt_label.text = "A / D 移动，靠近设施后按 E"
	_refresh_status()

func _nudge(direction: int) -> void:
	player_x = clampf(player_x + direction * 90.0, 0.0, world_width)
	run_state.last_exploration_x = player_x
	_refresh_player()

func _interact_nearest() -> void:
	if nearest_index < 0:
		return
	if absf(player_x - float(stations[nearest_index].x)) > interaction_distance:
		return
	_interact(nearest_index)

func _interact(index: int) -> void:
	if index < 0 or index >= stations.size():
		return
	var station := stations[index]
	if absf(player_x - float(station.x)) > interaction_distance:
		prompt_label.text = "距离太远，先靠近「%s」" % station.name
		return
	match String(station.type):
		"chest":
			var event_id := String(station.event)
			if run_state.is_event_opened(event_id):
				prompt_label.text = "物资箱已经打开。"
			else:
				send_command("reward", {"kind": "chest", "event_id": event_id})
		"enemy":
			if run_state.is_event_opened(String(station.event)):
				run_state.map_progress = maxi(run_state.map_progress, 2)
				send_command("map")
			else:
				send_command("battle", {"boss": false, "event_id": station.event})
		"boss":
			send_command("battle", {"boss": true, "event_id": station.event})
		"campfire":
			run_state.heal_full()
			run_state.save_game()
			prompt_label.text = "已在篝火休息，生命完全恢复。"
		"workbench":
			send_command("build", {"return_to": "exploration", "stage": stage})
		"exit":
			run_state.map_progress = mini(3, stage + 1)
			run_state.last_exploration_x = start_position_x
			run_state.save_game()
			send_command("map")

func _world_to_lane(value: float) -> float:
	return lerpf(70.0, lane.size.x - 70.0, value / world_width)

func _stage_name() -> String:
	var names := ["废弃站台", "巡逻区", "休息营地", "首领巢穴"]
	return names[clampi(stage, 0, names.size() - 1)]

func _refresh_status() -> void:
	if status_label != null:
		status_label.text = "HP %d/%d　护盾 %d　金钱 %d　位置 %.0f / %.0f" % [
			run_state.current_hp, run_state.max_hp, run_state.shield,
			run_state.money, player_x, world_width
		]
