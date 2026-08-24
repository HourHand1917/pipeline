extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var game_state := get_root().get_node_or_null("GameState")
	_assert(game_state != null, "GameState Autoload缺失")
	game_state.call("Reset")
	var packed := load("res://features/exploration/scenes/f1/f1_2.tscn") as PackedScene
	_assert(packed != null, "f1_2场景无法加载")
	var scene := packed.instantiate()
	get_root().add_child(scene)
	await process_frame
	await process_frame
	var mousy := scene.get_node("MapLayer/MousyPostbattleNPC") as Area2D
	var rocky := scene.get_node("MapLayer/RockyPrebattleNPC") as Area2D
	var player := scene.get_node("Player") as Node2D
	var dead := scene.get_node("MapLayer/DeadRocky") as AnimatedSprite2D
	_assert(mousy.global_position.x < player.global_position.x, "鼠鼠不在玩家左侧")
	_assert(player.global_position.x < rocky.global_position.x, "Rocky不在玩家右侧")
	_assert(not dead.visible, "战前不应显示死亡Rocky")
	_assert(not mousy.get_node("ClickZone").input_pickable, "战前鼠鼠不应可点击")
	scene.queue_free()
	await process_frame

	game_state.call("SetObjectState", "f1_2", "enemy_rocky_dialogue", {"defeated": true})
	scene = packed.instantiate()
	get_root().add_child(scene)
	await process_frame
	await process_frame
	mousy = scene.get_node("MapLayer/MousyPostbattleNPC") as Area2D
	rocky = scene.get_node("MapLayer/RockyPrebattleNPC") as Area2D
	dead = scene.get_node("MapLayer/DeadRocky") as AnimatedSprite2D
	_assert(not rocky.visible, "战后战前Rocky仍可见")
	_assert(dead.visible and dead.animation == &"death" and dead.frame == 92, "死亡Rocky未停在死亡末帧")
	_assert(mousy.get_node("ClickZone").input_pickable, "战后鼠鼠不可点击")
	print("F1_ROCKY_ENCOUNTER_FLOW_TEST_PASS layout=mousy-player-rocky postbattle=dead_rocky+mousy")
	scene.queue_free()
	game_state.call("Reset")
	quit(0)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("F1_ROCKY_ENCOUNTER_FLOW_TEST_FAIL: %s" % message)
	quit(1)
