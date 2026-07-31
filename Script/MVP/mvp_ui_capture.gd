extends Node

func _ready() -> void:
	var state: Node = get_node("/root/MvpState")
	var catalog: Resource = load("res://Resource/MVP/card_catalog.tres")
	state.new_run(false)
	var packed: PackedScene = load("res://Scenes/MVP/main_menu.tscn")
	var scene: Control = packed.instantiate()
	scene.call("configure", state, catalog, {})
	add_child(scene)
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame
	var image := get_viewport().get_texture().get_image()
	var output_path := "res://ui_preview.png"
	var error := image.save_png(ProjectSettings.globalize_path(output_path))
	print("MVP_UI_CAPTURE: ", image.get_width(), "x", image.get_height(), " error=", error)
	get_tree().quit()
