class_name MvpSceneBase
extends Control

signal scene_command(command: String, payload: Dictionary)

const ThemeFactory = preload("res://Script/MVP/mvp_theme_factory.gd")

var run_state: Node
var card_catalog: Resource
var scene_context: Dictionary = {}

func configure(state: Node, catalog: Resource, context: Dictionary = {}) -> void:
	run_state = state
	card_catalog = catalog
	scene_context = context.duplicate(true)

func send_command(command: String, payload: Dictionary = {}) -> void:
	scene_command.emit(command, payload)

func make_button(text_value: String, callback: Callable, min_size := Vector2(180, 52)) -> Button:
	var button := Button.new()
	button.text = text_value
	button.custom_minimum_size = min_size
	button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	button.focus_mode = Control.FOCUS_ALL
	button.pressed.connect(callback)
	return button

func make_title(text_value: String, font_size: int = 34) -> Label:
	var label := Label.new()
	label.text = text_value
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", Color("eafaff"))
	label.add_theme_color_override("font_shadow_color", Color(0.16, 0.82, 0.94, 0.28))
	label.add_theme_constant_override("shadow_offset_x", 2)
	label.add_theme_constant_override("shadow_offset_y", 3)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return label

func make_panel(color := Color(0.045, 0.075, 0.11, 0.96), radius: int = 14) -> PanelContainer:
	var panel := PanelContainer.new()
	var style: StyleBoxFlat = ThemeFactory.panel_style(color, Color(0.22, 0.76, 0.88, 0.78), radius, 1, 22)
	panel.add_theme_stylebox_override("panel", style)
	return panel

func add_screen_frame(parent: Control, accent: Color = Color("55e6ff")) -> void:
	var top_line := ColorRect.new()
	top_line.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_line.color = Color(accent, 0.72)
	top_line.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top_line.offset_bottom = 4
	parent.add_child(top_line)
	var header_glow := ColorRect.new()
	header_glow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	header_glow.color = Color(accent, 0.035)
	header_glow.set_anchors_preset(Control.PRESET_TOP_WIDE)
	header_glow.offset_bottom = 124
	parent.add_child(header_glow)
	var left_mark := ColorRect.new()
	left_mark.mouse_filter = Control.MOUSE_FILTER_IGNORE
	left_mark.color = Color(accent, 0.45)
	left_mark.position = Vector2(28, 42)
	left_mark.size = Vector2(4, 68)
	parent.add_child(left_mark)
	var system_label := Label.new()
	system_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	system_label.text = "PIPELINE // SYSTEM ONLINE"
	system_label.position = Vector2(44, 42)
	system_label.add_theme_font_size_override("font_size", 13)
	system_label.add_theme_color_override("font_color", Color(accent, 0.62))
	parent.add_child(system_label)

func set_full_rect(control: Control) -> void:
	control.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if control == self and theme == null:
		theme = ThemeFactory.create_theme()
