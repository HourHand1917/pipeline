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

	_add_speaker_marker(background, Vector2(310, 745), "鼠", Color("d3a84e"))
	_add_speaker_marker(background, Vector2(960, 805), "R", Color("69b5b3"))
	_add_speaker_marker(background, Vector2(1600, 710), "reb", Color("8b8fc9"))

	var anchor_scene := load("res://features/dialogue/scenes/dialogue_speaker_anchor.tscn") as PackedScene
	var characters := [
		load("res://features/dialogue/test_content/characters/鼠鼠.dch") as DialogicCharacter,
		load("res://features/dialogue/test_content/characters/RUBBER.dch") as DialogicCharacter,
		load("res://features/dialogue/test_content/characters/reb.dch") as DialogicCharacter,
	]
	var anchor_positions := [Vector2(310, 690), Vector2(960, 750), Vector2(1600, 655)]
	var anchors: Array[DialogueSpeakerAnchor] = []
	for index in characters.size():
		var anchor := anchor_scene.instantiate() as DialogueSpeakerAnchor
		anchor.dialogic_character = characters[index]
		anchor.position = anchor_positions[index]
		add_child(anchor)
		anchors.append(anchor)

	var canvas := (load("res://features/dialogue/scenes/npc_dialogue_canvas.tscn") as PackedScene).instantiate()
	canvas.max_visible_bubbles = 6
	canvas.min_bubble_width = 190.0
	canvas.max_bubble_width = 430.0
	add_child(canvas)
	await get_tree().process_frame
	canvas._on_timeline_started()

	var preview_lines: Array[Dictionary] = [
		{"text": "好了，说正事。你的 reb 我检查过了。", "character": characters[0]},
		{"text": "注意。我的机械效率高于这里两个生命体之和。", "character": characters[2]},
		{"text": "这也要损我吗？我很努力了好吧！", "character": characters[1]},
	]
	for info: Dictionary in preview_lines:
		canvas._on_about_to_show_text({"text": info.text, "character": info.character, "append": false})
		await get_tree().process_frame
		var active := canvas.get_node("Root/BubbleStack").get_child(-1)
		active.get_node("Margin/Content/DialogText").text = info.text
		await get_tree().create_timer(0.24).timeout

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


func _add_speaker_marker(parent: Control, center: Vector2, glyph: String, color: Color) -> void:
	var body := ColorRect.new()
	body.color = Color("35484a")
	body.position = center - Vector2(34, 70)
	body.size = Vector2(68, 96)
	parent.add_child(body)
	var label := Label.new()
	label.position = center - Vector2(70, 128)
	label.size = Vector2(140, 60)
	label.text = glyph
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 34)
	label.add_theme_color_override("font_color", color)
	parent.add_child(label)
