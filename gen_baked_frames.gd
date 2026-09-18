extends SceneTree
## 一次性工具：把三个 NPC 的序列帧目录烘焙为 SpriteFrames 资源。
## 用法：godot --headless --path . -s gen_baked_frames.gd
func _init() -> void:
	var dirs := {
		"mousy": "res://features/dialogue/npc/animations/frames/mousy",
		"upper_couple": "res://features/dialogue/npc/animations/frames/upper_couple",
		"richard": "res://features/dialogue/npc/animations/frames/richard",
	}
	for key in dirs:
		var dir := DirAccess.open(dirs[key])
		if dir == null:
			print("打开失败: ", dirs[key])
			continue
		var png_names: Array[String] = []
		for n in dir.get_files():
			if n.to_lower().ends_with(".png"):
				png_names.append(n)
		png_names.sort()
		if png_names.is_empty():
			print("没有帧: ", dirs[key])
			continue
		var sf := SpriteFrames.new()
		sf.add_animation(&"idle")
		sf.set_animation_speed(&"idle", 24.0)
		sf.set_animation_loop(&"idle", true)
		for n in png_names:
			var tex := load(dirs[key].path_join(n)) as Texture2D
			if tex != null:
				sf.add_frame(&"idle", tex)
		var out_path := "res://features/dialogue/npc/animations/%s_frames.tres" % key
		var err := ResourceSaver.save(sf, out_path)
		print(out_path, " 帧数=", sf.get_frame_count(&"idle"), " err=", err)
	quit()
