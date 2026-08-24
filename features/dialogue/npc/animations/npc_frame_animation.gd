@tool
extends AnimatedSprite2D

## 可复用的 NPC 序列帧动画机。只需配置帧目录即可自动按文件名顺序组装动画。
@export_dir var frames_directory := "":
	set(value):
		frames_directory = value
		if is_inside_tree():
			_rebuild_frames()

@export_range(1.0, 60.0, 1.0) var frames_per_second := 24.0:
	set(value):
		frames_per_second = value
		if sprite_frames != null and sprite_frames.has_animation(&"idle"):
			sprite_frames.set_animation_speed(&"idle", frames_per_second)

@export var loop_animation := true
@export var preview_in_editor := true


func _ready() -> void:
	_rebuild_frames()


func _rebuild_frames() -> void:
	if frames_directory.is_empty() or not DirAccess.dir_exists_absolute(frames_directory):
		return

	var names := DirAccess.get_files_at(frames_directory)
	var png_names: Array[String] = []
	for file_name in names:
		if file_name.to_lower().ends_with(".png"):
			png_names.append(file_name)
	png_names.sort()
	if png_names.is_empty():
		return

	var generated := SpriteFrames.new()
	generated.add_animation(&"idle")
	generated.set_animation_speed(&"idle", frames_per_second)
	generated.set_animation_loop(&"idle", loop_animation)
	for file_name in png_names:
		var texture := load(frames_directory.path_join(file_name)) as Texture2D
		if texture != null:
			generated.add_frame(&"idle", texture)

	sprite_frames = generated
	animation = &"idle"
	if not Engine.is_editor_hint() or preview_in_editor:
		play(&"idle")
