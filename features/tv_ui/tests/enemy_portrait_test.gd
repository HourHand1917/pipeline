extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed := load("res://Scenes/resource_scene/enemymassage.tscn") as PackedScene
	_assert(packed != null, "敌人信息面板无法加载")
	var panel := packed.instantiate()
	get_root().add_child(panel)
	var icon := panel.get_node("enemymassagebox/statsbox/enemyicon") as TextureRect
	_assert(icon != null, "敌人头像控件不存在")
	_assert(icon.expand_mode == TextureRect.EXPAND_IGNORE_SIZE, "头像未启用自适应尺寸")
	_assert(icon.stretch_mode == TextureRect.STRETCH_KEEP_ASPECT_CENTERED, "头像未保持比例居中")
	for property_name: String in ["boomPortrait", "rockyPortrait", "sharkkPortrait", "coreHandPortrait", "coreBodyPortrait"]:
		_assert(panel.get(property_name) is Texture2D, "%s 未配置" % property_name)
	var source := FileAccess.get_file_as_string("res://Script/CS/EnemyBasicNode.cs")
	_assert(source.contains("debugButton.Visible = false"), "左上角信息按钮仍会显示")
	print("ENEMY_PORTRAIT_TEST_PASS portraits=5 core_right_hand=mirrored debug_button=hidden")
	panel.queue_free()
	quit(0)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("ENEMY_PORTRAIT_TEST_FAIL: %s" % message)
	quit(1)
