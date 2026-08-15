extends Node

const OUTPUT_PATH := "D:/Godot/Pipeline2/work/npc_dialogue_choice_bubbles_preview.png"


func _ready() -> void:
	var background := ColorRect.new()
	background.color = Color("151a1d")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	_add_rect(background, Vector2(0, 0), Vector2(1920, 130), Color("242b2e"))
	_add_rect(background, Vector2(0, 810), Vector2(1920, 270), Color("30383a"))
	_add_rect(background, Vector2(0, 806), Vector2(1920, 4), Color("a05b31"))
	for x in range(80, 1920, 220):
		_add_rect(background, Vector2(x, 72), Vector2(10, 10), Color("806f56"))

	var header := Label.new()
	header.position = Vector2(68, 38)
	header.add_theme_font_size_override("font_size", 27)
	header.add_theme_color_override("font_color", Color("e8d7b2"))
	header.text = "PIPELINE // 废土通讯终端"
	background.add_child(header)

	var npc_shadow := ColorRect.new()
	npc_shadow.color = Color(0, 0, 0, 0.35)
	npc_shadow.position = Vector2(894, 775)
	npc_shadow.size = Vector2(132, 18)
	background.add_child(npc_shadow)

	var npc_body := ColorRect.new()
	npc_body.color = Color("35484a")
	npc_body.position = Vector2(925, 676)
	npc_body.size = Vector2(70, 110)
	background.add_child(npc_body)
	var npc_head := ColorRect.new()
	npc_head.color = Color("69b5b3")
	npc_head.position = Vector2(937, 635)
	npc_head.size = Vector2(46, 46)
	background.add_child(npc_head)

	var anchor := Node2D.new()
	anchor.position = Vector2(960, 648)
	add_child(anchor)

	var canvas := (load("res://features/dialogue/scenes/npc_dialogue_canvas.tscn") as PackedScene).instantiate()
	add_child(canvas)
	await get_tree().process_frame
	canvas.set_dialogue_anchor(anchor)
	canvas._on_timeline_started()

	for line in [
		"你终于来了。风暴正在吞掉北侧的旧电站。",
		"仓库里留着一份路线图，但每条路都要付出代价。",
		"选吧。我们没有时间再等下一班车了。"
	]:
		canvas._on_about_to_show_text({"text": line, "character": null, "append": false})
		await get_tree().process_frame
		var active := canvas.get_node("Root/BubbleStack").get_child(-1)
		active.get_node("Margin/Content/DialogText").text = line
		await get_tree().process_frame

	var choice_buttons := get_tree().get_nodes_in_group("dialogic_choice_button")
	choice_buttons[0]._load_info({"button_index": 1, "text": "先去仓库，找出旧地图上的安全路线", "visible": true, "disabled": false})
	choice_buttons[1]._load_info({"button_index": 2, "text": "直接穿过风暴区，争取在天黑前抵达", "visible": true, "disabled": false})
	choice_buttons[2]._load_info({"button_index": 3, "text": "要求更多情报（线路故障）", "visible": true, "disabled": true})
	canvas._on_question_shown({"invalid": false})
	choice_buttons[0].grab_focus()
	await get_tree().create_timer(0.45).timeout

	DirAccess.make_dir_recursive_absolute(OUTPUT_PATH.get_base_dir())
	var image := get_viewport().get_texture().get_image()
	var error := image.save_png(OUTPUT_PATH)
	if error == OK:
		print("NPC_DIALOGUE_VISUAL_TEST_PASS path=", OUTPUT_PATH)
		get_tree().quit(0)
	else:
		push_error("Unable to save dialogue preview: " + str(error))
		get_tree().quit(1)


func _add_rect(parent: Control, position: Vector2, size: Vector2, color: Color) -> void:
	var rect := ColorRect.new()
	rect.position = position
	rect.size = size
	rect.color = color
	parent.add_child(rect)
