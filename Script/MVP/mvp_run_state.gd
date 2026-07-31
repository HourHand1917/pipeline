class_name MvpRunState
extends Node

signal state_changed

const SAVE_PATH := "user://mvp_save.json"

@export_group("新游戏默认值")
@export_range(1, 999, 1) var default_max_hp: int = 30
@export_range(0, 999, 1) var default_money: int = 0
@export_range(0, 99, 1) var default_water: int = 1

var current_hp: int = 30
var max_hp: int = 30
var shield: int = 0
var money: int = 0
var water: int = 1
var map_progress: int = 0
var board_level: int = 0
var boss_defeated: bool = false
var opened_event_ids: Array[String] = []
var card_counts: Dictionary = {}
var saved_build: Array = []
var last_exploration_x: float = 120.0

func _ready() -> void:
	if card_counts.is_empty():
		new_run(false)

func new_run(save_immediately: bool = true) -> void:
	max_hp = default_max_hp
	current_hp = max_hp
	shield = 0
	money = default_money
	water = default_water
	map_progress = 0
	board_level = 0
	boss_defeated = false
	opened_event_ids = []
	card_counts = {
		"revolver": 1,
		"shield": 1,
		"battery": 1,
		"medkit": 1,
		"knockback_gun": 1,
		"teleport_shoes": 1,
		"rapier": 1,
		"poison_sword": 1,
		"cleanse_pill": 1,
		"strength_glasses": 1,
	}
	saved_build = [
		{"card_id": "revolver", "anchor": [0, 0], "rotation": 0},
		{"card_id": "shield", "anchor": [2, 0], "rotation": 0},
		{"card_id": "battery", "anchor": [0, 1], "rotation": 0},
		{"card_id": "medkit", "anchor": [1, 1], "rotation": 0},
	]
	last_exploration_x = 120.0
	if save_immediately:
		save_game()
	state_changed.emit()

func board_size() -> Vector2i:
	match board_level:
		1:
			return Vector2i(3, 3)
		2:
			return Vector2i(4, 3)
		_:
			return Vector2i(3, 2)

func heal_full() -> void:
	current_hp = max_hp
	shield = 0
	state_changed.emit()

func damage(amount: int) -> void:
	var blocked := mini(shield, maxi(0, amount))
	shield -= blocked
	current_hp = maxi(0, current_hp - maxi(0, amount - blocked))
	state_changed.emit()

func add_card(card_id: StringName, count: int = 1) -> void:
	var key := String(card_id)
	card_counts[key] = int(card_counts.get(key, 0)) + maxi(0, count)
	state_changed.emit()

func mark_event(event_id: String) -> void:
	if event_id != "" and event_id not in opened_event_ids:
		opened_event_ids.append(event_id)
	state_changed.emit()

func is_event_opened(event_id: String) -> bool:
	return event_id in opened_event_ids

func set_build(entries: Array) -> void:
	saved_build = []
	for entry in entries:
		saved_build.append(entry.duplicate(true))
	state_changed.emit()

func save_game() -> bool:
	var file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if file == null:
		push_error("无法写入存档：" + SAVE_PATH)
		return false
	file.store_string(JSON.stringify(to_dictionary(), "\t"))
	return true

func load_game() -> bool:
	if not FileAccess.file_exists(SAVE_PATH):
		return false
	var file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if file == null:
		return false
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		return false
	from_dictionary(parsed)
	state_changed.emit()
	return true

func has_save() -> bool:
	return FileAccess.file_exists(SAVE_PATH)

func clear_save() -> void:
	if FileAccess.file_exists(SAVE_PATH):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(SAVE_PATH))

func to_dictionary() -> Dictionary:
	return {
		"current_hp": current_hp,
		"max_hp": max_hp,
		"shield": shield,
		"money": money,
		"water": water,
		"map_progress": map_progress,
		"board_level": board_level,
		"boss_defeated": boss_defeated,
		"opened_event_ids": opened_event_ids,
		"card_counts": card_counts,
		"saved_build": saved_build,
		"last_exploration_x": last_exploration_x,
	}

func from_dictionary(data: Dictionary) -> void:
	max_hp = maxi(1, int(data.get("max_hp", default_max_hp)))
	current_hp = clampi(int(data.get("current_hp", max_hp)), 0, max_hp)
	shield = maxi(0, int(data.get("shield", 0)))
	money = maxi(0, int(data.get("money", 0)))
	water = maxi(0, int(data.get("water", 0)))
	map_progress = clampi(int(data.get("map_progress", 0)), 0, 3)
	board_level = clampi(int(data.get("board_level", 0)), 0, 2)
	boss_defeated = bool(data.get("boss_defeated", false))
	opened_event_ids = []
	for value in data.get("opened_event_ids", []):
		opened_event_ids.append(String(value))
	card_counts = Dictionary(data.get("card_counts", {})).duplicate(true)
	saved_build = Array(data.get("saved_build", [])).duplicate(true)
	last_exploration_x = float(data.get("last_exploration_x", 120.0))
