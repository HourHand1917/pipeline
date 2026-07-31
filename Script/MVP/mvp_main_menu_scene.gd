class_name MvpMainMenuScene
extends "res://Script/MVP/mvp_scene_base.gd"

@export_group("界面文本")
@export var game_title: String = "PIPELINE"
@export_multiline var subtitle: String = "末日管线 · 卡牌构筑 · 距离战斗"
@export var backdrop_texture: Texture2D

func _ready() -> void:
	set_full_rect(self)
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("07131f")
	add_child(background)
	add_screen_frame(self)
	if backdrop_texture != null:
		var texture := TextureRect.new()
		set_full_rect(texture)
		texture.texture = backdrop_texture
		texture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		texture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		texture.modulate = Color(1, 1, 1, 0.19)
		add_child(texture)
	var center := CenterContainer.new()
	set_full_rect(center)
	add_child(center)
	var panel := make_panel(Color(0.025, 0.055, 0.09, 0.96))
	panel.custom_minimum_size = Vector2(760, 660)
	center.add_child(panel)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 22)
	panel.add_child(box)
	box.add_child(make_title(game_title, 78))
	var sub := make_title(subtitle, 24)
	sub.modulate = Color("79e8ff")
	box.add_child(sub)
	var separator := HSeparator.new()
	box.add_child(separator)
	box.add_child(make_button("开始新游戏", func(): send_command("new_run"), Vector2(420, 64)))
	var continue_button := make_button("继续游戏", func(): send_command("continue"), Vector2(420, 64))
	continue_button.disabled = run_state == null or not run_state.has_save()
	box.add_child(continue_button)
	box.add_child(make_button("清除存档", _clear_save, Vector2(420, 56)))
	box.add_child(make_button("退出", func(): send_command("quit"), Vector2(420, 56)))
	var hint := Label.new()
	hint.text = "A / D 移动，E 互动；战斗中点击格子充能。F1 显示或隐藏调试面板。"
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	hint.custom_minimum_size.x = 590
	hint.add_theme_color_override("font_color", Color("8faab8"))
	box.add_child(hint)

func _clear_save() -> void:
	run_state.clear_save()
	run_state.new_run(false)
	get_tree().reload_current_scene()
