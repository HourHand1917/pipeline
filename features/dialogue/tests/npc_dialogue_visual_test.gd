extends Node

const OUTPUT_PATH := "D:/Godot/Pipeline2/work/npc_dialogue_canvas_preview.png"


func _ready() -> void:
	var background := ColorRect.new()
	background.color = Color("1b2228")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(background)

	var floor := ColorRect.new()
	floor.color = Color("303b40")
	floor.position = Vector2(0, 790)
	floor.size = Vector2(1440, 290)
	background.add_child(floor)

	var npc_body := ColorRect.new()
	npc_body.color = Color("68c8d0")
	npc_body.position = Vector2(685, 675)
	npc_body.size = Vector2(70, 115)
	background.add_child(npc_body)

	var anchor := Node2D.new()
	anchor.position = Vector2(720, 675)
	add_child(anchor)

	var canvas := (load("res://features/dialogue/scenes/npc_dialogue_canvas.tscn") as PackedScene).instantiate()
	add_child(canvas)
	await get_tree().process_frame
	canvas.set_dialogue_anchor(anchor)
	canvas._on_timeline_started()

	for line in [
		"你终于来了，这里的风暴越来越近。",
		"仓库里有一份旧地图，也许能帮上忙。",
		"你准备先调查哪一条线索？"
	]:
		canvas._on_about_to_show_text({"text": line, "character": null, "append": false})
		await get_tree().process_frame
		var active := canvas.get_node("Root/BubbleStack").get_child(-1)
		active.get_node("Margin/Content/DialogText").text = line
		await get_tree().process_frame

	var choice_buttons := get_tree().get_nodes_in_group("dialogic_choice_button")
	choice_buttons[0]._load_info({"text": "先查看仓库里的旧地图", "visible": true, "disabled": false})
	choice_buttons[1]._load_info({"text": "先询问风暴出现的时间", "visible": true, "disabled": false})
	canvas._on_question_shown({"invalid": false})
	await get_tree().create_timer(0.4).timeout

	DirAccess.make_dir_recursive_absolute(OUTPUT_PATH.get_base_dir())
	var image := get_viewport().get_texture().get_image()
	var error := image.save_png(OUTPUT_PATH)
	if error == OK:
		print("NPC_DIALOGUE_VISUAL_TEST_PASS path=", OUTPUT_PATH)
		get_tree().quit(0)
	else:
		push_error("Unable to save dialogue preview: " + str(error))
		get_tree().quit(1)
