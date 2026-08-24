extends SceneTree

const PREFABS := {
	"boom": "res://features/dialogue/npc/scenes/boom_enemy_npc.tscn",
	"rocky": "res://features/dialogue/npc/scenes/rocky_prebattle_npc.tscn",
	"sharkk": "res://features/dialogue/npc/scenes/sharkk_prebattle_npc.tscn",
	"core00_phase_one": "res://features/dialogue/npc/scenes/core00_phase_one_enemy_npc.tscn",
}

var checks := 0
var failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	for enemy_id: String in PREFABS:
		var packed := load(PREFABS[enemy_id]) as PackedScene
		_check(packed != null, "%s prefab loads" % enemy_id)
		if packed == null:
			continue
		var npc := packed.instantiate()
		root.add_child(npc)
		_check(npc is HostileNPC, "%s keeps the original HostileNPC behaviour" % enemy_id)
		_check(npc.get_node_or_null("DetectionRange") != null, "%s includes its range" % enemy_id)
		_check(npc.get_node_or_null("ClickZone/ClickShape") != null, "%s includes its click zone" % enemy_id)
		_check(npc.get_node_or_null("Blink") != null, "%s includes appearance feedback" % enemy_id)
		_check(npc.find_child("ReturnSpawn", true, false) is SpawnPoint, "%s includes a return spawn" % enemy_id)
		if enemy_id == "core00_phase_one":
			for hand_name: String in ["TrueHand", "FalseHand"]:
				var hand := npc.get_node("Sprite/%s" % hand_name) as AnimatedSprite2D
				_check(hand != null and hand.sprite_frames != null, "%s configures %s" % [enemy_id, hand_name])
				_check(hand != null and hand.autoplay != &"", "%s %s auto-plays idle" % [enemy_id, hand_name])
		else:
			var sprite := npc.get_node("Sprite") as AnimatedSprite2D
			_check(sprite != null and sprite.sprite_frames != null, "%s uses battle SpriteFrames on the map" % enemy_id)
			_check(sprite != null and sprite.autoplay == &"idle", "%s map appearance auto-plays battle idle" % enemy_id)
		npc.free()

	var map_cases := {
		"res://features/exploration/scenes/f1/f1_1.tscn": "MapLayer/Boom/Sprite",
		"res://features/exploration/scenes/f1/f1_2.tscn": "MapLayer/EnemyRocky/Sprite",
		"res://features/exploration/scenes/f2/f2_4.tscn": "MapLayer/Sharkk/Sprite",
	}
	for scene_path: String in map_cases:
		var map := (load(scene_path) as PackedScene).instantiate()
		root.add_child(map)
		var sprite := map.get_node(map_cases[scene_path]) as AnimatedSprite2D
		_check(sprite != null and sprite.autoplay == &"idle",
			"%s shows the battle idle sequence in exploration" % scene_path.get_file())
		map.free()

	var sharkk := load("res://anime/profiles/sharkk.tres") as BattleAnimationProfile
	_check(sharkk != null and not sharkk.source_faces_right,
		"Sharkk source orientation is calibrated left and runtime facing points toward the player")

	if failures.is_empty():
		print("ENEMY_ANIMATION_PREFAB_TEST_PASS checks=%d prefabs=4 maps=3" % checks)
		quit(0)
	else:
		for failure: String in failures:
			push_error("ENEMY_ANIMATION_PREFAB_TEST_FAIL: %s" % failure)
		quit(1)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
