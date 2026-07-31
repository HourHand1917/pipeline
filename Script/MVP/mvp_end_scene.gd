class_name MvpEndScene
extends "res://Script/MVP/mvp_scene_base.gd"

@export var victory: bool = false
@export var title_text: String = ""
@export_multiline var body_text: String = ""

func _ready() -> void:
	set_full_rect(self)
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("061017") if victory else Color("17090b")
	add_child(background)
	add_screen_frame(self, Color("55e6ff") if victory else Color("ff5f70"))
	var center := CenterContainer.new()
	set_full_rect(center)
	add_child(center)
	var panel := make_panel(Color(0.035, 0.085, 0.095, 0.98) if victory else Color(0.12, 0.035, 0.045, 0.98))
	panel.custom_minimum_size = Vector2(820, 620)
	center.add_child(panel)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 24)
	panel.add_child(box)
	var title := title_text
	if title == "":
		title = "管线恢复运转" if victory else "本次探索失败"
	box.add_child(make_title(title, 48))
	var body := Label.new()
	body.text = body_text if body_text != "" else ("你已经完成探索、构筑、战斗与 Boss 的完整 MVP 循环。" if victory else "可以从主菜单读取最近一次自动存档，或开始新的探索。")
	body.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	body.add_theme_font_size_override("font_size", 22)
	body.custom_minimum_size = Vector2(680, 180)
	box.add_child(body)
	if victory:
		box.add_child(make_button("返回地图继续调试", func(): send_command("map"), Vector2(330, 58)))
	else:
		box.add_child(make_button("读取最近存档", func(): send_command("continue"), Vector2(330, 58)))
	box.add_child(make_button("返回主菜单", func(): send_command("menu"), Vector2(330, 54)))
	box.add_child(make_button("重新开始", func(): send_command("new_run"), Vector2(330, 54)))
