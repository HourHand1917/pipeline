extends SceneTree

const ENEMY_FRAME_RESOURCES := [
	"res://anime/generated/boom_frames.tres",
	"res://anime/generated/rocky_frames.tres",
	"res://anime/generated/sharkk_frames.tres",
	"res://anime/generated/core_hand_frames.tres",
	"res://anime/generated/core_body_frames.tres",
]


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var checks := 0
	for resource_path: String in ENEMY_FRAME_RESOURCES:
		var frames := load(resource_path) as SpriteFrames
		_assert(frames != null, "无法加载敌人动画：%s" % resource_path)
		for animation_name: StringName in frames.get_animation_names():
			_assert(
				is_equal_approx(frames.get_animation_speed(animation_name), 30.0),
				"%s/%s 不是 30 FPS" % [resource_path, animation_name]
			)
			checks += 1

	var boom := load("res://anime/generated/boom_frames.tres") as SpriteFrames
	_assert(boom.has_animation(&"attack"), "Boom 缺少 attack 动画")
	_assert(boom.get_frame_count(&"attack") == 41, "Boom attack 应为 41 帧")
	var boom_profile := load("res://anime/profiles/boom.tres")
	_assert(boom_profile != null, "Boom Profile 无法加载")
	_assert(boom_profile.resolve_action(&"boom_attack") == &"attack", "boom_attack 未映射到 attack")
	_assert(boom_profile.visual_offset == Vector2(0, -56), "Boom 脚底校准偏移错误")
	checks += 5

	print("ENEMY_ANIMATION_30FPS_TEST_PASS checks=%d boom_attack_frames=41" % checks)
	quit(0)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("ENEMY_ANIMATION_30FPS_TEST_FAIL: %s" % message)
	quit(1)
