@tool
extends AnimatedSprite2D

## 可复用的 NPC 序列帧动画机。只需配置帧目录即可自动按文件名顺序组装动画。
## 导出版本中 res:// 目录扫描不可用（pck 内没有原始 png 条目），
## 因此发布前请用 gen_baked_frames.gd 烘焙 SpriteFrames 并拖入 baked_frames。
@export_dir var frames_directory := "":
	set(value):
		frames_directory = value
		if is_inside_tree():
			_rebuild_frames()

@export var baked_frames: SpriteFrames

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
	# 优先使用烘焙资源：导出版本没有目录扫描能力，编辑器里也可直接预览
	if baked_frames != null:
		sprite_frames = baked_frames
		animation = &"idle"
		if baked_frames.has_animation(&"idle"):
			baked_frames.set_animation_speed(&"idle", frames_per_second)
		if not Engine.is_editor_hint() or preview_in_editor:
			play(&"idle")
		return

	if frames_directory.is_empty():
		return

	# 导出版本中 res:// 位于 pck 内，dir_exists_absolute/get_files_at 只认真实文件系统；
	# 必须用 DirAccess.open（编辑器与导出版均可用）。
	var dir := DirAccess.open(frames_directory)
	if dir == null:
		return

	var names := dir.get_files()
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
