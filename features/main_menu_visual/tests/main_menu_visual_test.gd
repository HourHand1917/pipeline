extends SceneTree


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed := load("res://Scenes/game_scene/main_menu.tscn") as PackedScene
	_assert(packed != null, "主菜单无法加载")
	var menu := packed.instantiate()
	get_root().add_child(menu)
	await process_frame
	await process_frame
	var visual := menu.get_node("MainMenuVisual")
	_assert(visual != null, "主菜单没有挂载动态视觉层")
	_assert(int(visual.get("BaseFrameCount")) == 47, "底图序列应为47帧")
	_assert(int(visual.get("RubberFrameCount")) == 47, "Rubber序列应为47帧")
	_assert(int(visual.get("DesktopFrameCount")) == 95, "桌面序列应为95帧")
	_assert(visual.get_node("DesignCanvas/Logo") is Sprite2D, "主菜单Logo未配置")
	_assert(menu.get_node("MenuContainer/Start") is Button, "原开始游戏按钮丢失")
	_assert(menu.get_node("MenuContainer/Load") is Button, "原读档按钮丢失")
	print("MAIN_MENU_VISUAL_TEST_PASS base=47 rubber=47 desktop=95 legacy_buttons=preserved")
	menu.queue_free()
	quit(0)


func _assert(condition: bool, message: String) -> void:
	if condition:
		return
	push_error("MAIN_MENU_VISUAL_TEST_FAIL: %s" % message)
	quit(1)
