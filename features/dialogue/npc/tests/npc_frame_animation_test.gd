extends Node

const CASES := {
	"res://features/dialogue/npc/scenes/home_mousy_npc.tscn": "mousy",
	"res://features/dialogue/npc/scenes/mousy_postbattle_npc.tscn": "mousy",
	"res://features/dialogue/npc/scenes/post_sharkk_mousy_npc.tscn": "mousy",
	"res://features/dialogue/npc/scenes/casino_dealer_npc.tscn": "richard",
	"res://features/dialogue/npc/scenes/upper_couple_npc.tscn": "upper_couple",
}

var checks := 0
var failures := 0


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	for scene_path: String in CASES:
		var packed := load(scene_path) as PackedScene
		_check(packed != null, "%s loads" % scene_path)
		if packed == null:
			continue
		var instance := packed.instantiate()
		add_child(instance)
		await get_tree().process_frame
		var sprite := instance.get_node_or_null("Sprite") as AnimatedSprite2D
		_check(sprite != null, "%s uses AnimatedSprite2D" % scene_path)
		if sprite != null:
			_check(sprite.sprite_frames != null, "%s generated SpriteFrames" % scene_path)
			_check(sprite.sprite_frames.get_frame_count(&"idle") == 47,
				"%s keeps all 47 frames" % scene_path)
			_check(sprite.sprite_frames.get_animation_speed(&"idle") == 24.0,
				"%s runs at 24 FPS" % scene_path)
			_check(sprite.sprite_frames.get_animation_loop(&"idle"),
				"%s loops idle" % scene_path)
			_check(sprite.is_playing(), "%s autoplays idle" % scene_path)
			_check(str(sprite.get("frames_directory")).ends_with(str(CASES[scene_path])),
				"%s uses the matching source folder" % scene_path)
		instance.queue_free()
		await get_tree().process_frame

	if failures == 0:
		print("NPC_FRAME_ANIMATION_TEST_PASS checks=%d scenes=%d frames_per_scene=47" % [checks, CASES.size()])
	else:
		push_error("NPC_FRAME_ANIMATION_TEST_FAIL failures=%d checks=%d" % [failures, checks])
	get_tree().quit(0 if failures == 0 else 1)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if condition:
		return
	failures += 1
	push_error(message)
