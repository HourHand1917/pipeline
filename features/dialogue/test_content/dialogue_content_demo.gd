extends Node2D

const CHARACTER_DIRECTORY := {
	"鼠鼠": "res://features/dialogue/test_content/characters/鼠鼠.dch",
	"reb": "res://features/dialogue/test_content/characters/reb.dch",
	"RUBBER": "res://features/dialogue/test_content/characters/RUBBER.dch",
}
const TIMELINE_DIRECTORY := {
	"水龙头与上层工程师": "res://features/dialogue/test_content/timelines/水龙头与上层工程师.dtl",
}


func _enter_tree() -> void:
	# 这两个目录只在测试场景运行期注册，不改写 project.godot。
	Engine.set_meta(&"dch_directory", CHARACTER_DIRECTORY.duplicate())
	Engine.set_meta(&"dtl_directory", TIMELINE_DIRECTORY.duplicate())


func _exit_tree() -> void:
	Engine.remove_meta(&"dch_directory")
	Engine.remove_meta(&"dtl_directory")
