class_name MvpMapScene
extends "res://Script/MVP/mvp_scene_base.gd"

@export_group("地图节点")
@export var node_names: Array[String] = ["废弃站台", "巡逻区", "休息营地", "首领巢穴"]
@export var allow_debug_node_selection: bool = true
@export var map_accent_color: Color = Color("48d5ee")

var status_label: Label

func _ready() -> void:
	set_full_rect(self)
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("071018")
	add_child(background)
	add_screen_frame(self, map_accent_color)
	var margin := MarginContainer.new()
	set_full_rect(margin)
	margin.add_theme_constant_override("margin_left", 60)
	margin.add_theme_constant_override("margin_right", 390)
	margin.add_theme_constant_override("margin_top", 54)
	margin.add_theme_constant_override("margin_bottom", 54)
	add_child(margin)
	var main := VBoxContainer.new()
	main.add_theme_constant_override("separation", 22)
	margin.add_child(main)
	main.add_child(make_title("路线地图", 42))
	status_label = Label.new()
	status_label.add_theme_font_size_override("font_size", 20)
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	main.add_child(status_label)
	var route_panel := make_panel()
	route_panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main.add_child(route_panel)
	var route := HBoxContainer.new()
	route.alignment = BoxContainer.ALIGNMENT_CENTER
	route.add_theme_constant_override("separation", 18)
	route_panel.add_child(route)
	for index in range(node_names.size()):
		var column := VBoxContainer.new()
		column.custom_minimum_size = Vector2(250, 330)
		column.alignment = BoxContainer.ALIGNMENT_CENTER
		route.add_child(column)
		var number := Label.new()
		number.text = "%02d" % (index + 1)
		number.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		number.add_theme_font_size_override("font_size", 52)
		number.modulate = map_accent_color if index <= run_state.map_progress else Color(0.3, 0.36, 0.42)
		column.add_child(number)
		var node_button := Button.new()
		node_button.text = node_names[index] if index < node_names.size() else "节点"
		node_button.custom_minimum_size = Vector2(225, 125)
		node_button.disabled = index > run_state.map_progress
		var chosen := index
		node_button.pressed.connect(func(): _enter_node(chosen))
		column.add_child(node_button)
		var state_text := Label.new()
		state_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		if index < run_state.map_progress:
			state_text.text = "已完成"
		elif index == run_state.map_progress:
			state_text.text = "当前位置"
		else:
			state_text.text = "未解锁"
		column.add_child(state_text)
	var actions := HBoxContainer.new()
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.add_theme_constant_override("separation", 12)
	main.add_child(actions)
	actions.add_child(make_button("进入当前节点", func(): _enter_node(run_state.map_progress)))
	actions.add_child(make_button("整理构筑", func(): send_command("build", {"return_to": "map"})))
	actions.add_child(make_button("保存", _save))
	actions.add_child(make_button("返回主菜单", func(): send_command("menu")))
	if allow_debug_node_selection:
		var debug := HBoxContainer.new()
		debug.alignment = BoxContainer.ALIGNMENT_CENTER
		main.add_child(debug)
		for index in range(node_names.size()):
			var chosen := index
			var button := make_button("调试节点 %d" % (index + 1), func(): _debug_jump(chosen), Vector2(135, 38))
			debug.add_child(button)
	_refresh_status()

func _enter_node(index: int) -> void:
	run_state.map_progress = clampi(index, 0, 3)
	run_state.state_changed.emit()
	send_command("exploration", {"stage": run_state.map_progress})

func _debug_jump(index: int) -> void:
	run_state.map_progress = index
	run_state.state_changed.emit()
	_refresh_status()

func _save() -> void:
	run_state.save_game()
	_refresh_status("已保存")

func _refresh_status(extra: String = "") -> void:
	var board_dimensions: Vector2i = run_state.board_size()
	status_label.text = "HP %d/%d　金钱 %d　水 %d　构筑板 %d×%d%s" % [
		run_state.current_hp, run_state.max_hp, run_state.money, run_state.water,
		board_dimensions.x, board_dimensions.y, ("　" + extra) if extra != "" else ""
	]
