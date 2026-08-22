extends SceneTree

const MANIFEST_PATH := "res://anime/import_manifest.json"

func _initialize() -> void:
	call_deferred("_build_all")


func _build_all() -> void:
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
	if not parsed is Dictionary:
		push_error("ANIME_BUILD_FAIL: import_manifest.json is invalid")
		quit(1)
		return

	var manifest := parsed as Dictionary
	var assets_root := str(manifest.get("assets_root", "res://anime_assets/frames"))
	var output_root := str(manifest.get("output_root", "res://anime/generated"))
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(output_root))

	var actor_count := 0
	var clip_count := 0
	var frame_count := 0
	for actor_value: Variant in manifest.get("actors", []):
		if not actor_value is Dictionary:
			continue
		var actor := actor_value as Dictionary
		var actor_id := str(actor.get("id", "")).strip_edges()
		if actor_id.is_empty():
			continue
		var sprite_frames := SpriteFrames.new()
		sprite_frames.clear_all()
		var actor_clip_count := 0
		var actor_frame_count := 0

		for clip_value: Variant in actor.get("clips", []):
			if not clip_value is Dictionary:
				continue
			var clip := clip_value as Dictionary
			var animation_name := StringName(str(clip.get("name", "")).strip_edges())
			var folder := str(clip.get("folder", "")).strip_edges()
			if animation_name.is_empty() or folder.is_empty():
				continue
			var files := _png_files("%s/%s" % [assets_root.trim_suffix("/"), folder])
			if files.is_empty():
				continue
			sprite_frames.add_animation(animation_name)
			sprite_frames.set_animation_speed(animation_name, float(clip.get("fps", 24.0)))
			sprite_frames.set_animation_loop(animation_name, bool(clip.get("loop", false)))
			for texture_path: String in files:
				var texture := load(texture_path) as Texture2D
				if texture == null:
					push_error("ANIME_BUILD_FAIL: cannot load %s" % texture_path)
					quit(1)
					return
				sprite_frames.add_frame(animation_name, texture)
			actor_clip_count += 1
			actor_frame_count += files.size()

		if actor_clip_count == 0:
			sprite_frames.add_animation(&"idle")
			sprite_frames.set_animation_speed(&"idle", 24.0)
			sprite_frames.set_animation_loop(&"idle", true)

		var save_path := "%s/%s_frames.tres" % [output_root.trim_suffix("/"), actor_id]
		var save_error := ResourceSaver.save(sprite_frames, save_path)
		if save_error != OK:
			push_error("ANIME_BUILD_FAIL: cannot save %s (%s)" % [save_path, save_error])
			quit(1)
			return
		actor_count += 1
		clip_count += actor_clip_count
		frame_count += actor_frame_count
		print("ANIME_BUILD_ACTOR id=%s clips=%d frames=%d" % [actor_id, actor_clip_count, actor_frame_count])

	print("ANIME_BUILD_PASS actors=%d clips=%d frames=%d" % [actor_count, clip_count, frame_count])
	quit(0)


func _png_files(folder_path: String) -> Array[String]:
	var result: Array[String] = []
	var directory := DirAccess.open(folder_path)
	if directory == null:
		return result
	for file_name: String in directory.get_files():
		if file_name.get_extension().to_lower() == "png":
			result.append("%s/%s" % [folder_path.trim_suffix("/"), file_name])
	result.sort_custom(_natural_frame_less)
	return result


func _natural_frame_less(left: String, right: String) -> bool:
	var left_name := left.get_file().get_basename()
	var right_name := right.get_file().get_basename()
	var left_number := int(left_name.get_slice("_", 0))
	var right_number := int(right_name.get_slice("_", 0))
	if left_number == right_number:
		return left_name.naturalnocasecmp_to(right_name) < 0
	return left_number < right_number
