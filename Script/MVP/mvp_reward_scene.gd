class_name MvpRewardScene
extends "res://Script/MVP/mvp_scene_base.gd"

@export_group("奖励数值 - Inspector 可调")
@export_range(0, 999, 1) var chest_money: int = 12
@export var chest_card_id: StringName = &"battery"
@export_range(0, 999, 1) var battle_money: int = 20
@export var battle_card_id: StringName = &"medkit"
@export var normal_battle_unlocks_board: bool = true

var kind: String
var applied: bool = false
var description_label: Label

func _ready() -> void:
	set_full_rect(self)
	kind = String(scene_context.get("kind", "chest"))
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("07131b")
	add_child(background)
	add_screen_frame(self, Color("6ef3ff"))
	var center := CenterContainer.new()
	set_full_rect(center)
	add_child(center)
	var panel := make_panel(Color(0.035, 0.085, 0.095, 0.98))
	panel.custom_minimum_size = Vector2(840, 690)
	center.add_child(panel)
	var box := VBoxContainer.new()
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 26)
	panel.add_child(box)
	box.add_child(make_title("获得奖励", 46))
	var icon := Label.new()
	icon.text = "◆"
	icon.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	icon.add_theme_font_size_override("font_size", 96)
	icon.modulate = Color("6ef3ff")
	box.add_child(icon)
	description_label = Label.new()
	description_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	description_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	description_label.add_theme_font_size_override("font_size", 24)
	description_label.custom_minimum_size = Vector2(700, 190)
	description_label.text = _description()
	box.add_child(description_label)
	box.add_child(make_button("收下奖励", _accept, Vector2(380, 66)))

func _description() -> String:
	match kind:
		"battle":
			var board_text := "\n构筑板升级到 3×3" if normal_battle_unlocks_board and run_state.board_level < 1 else ""
			return "击败巡逻怪物\n金钱 +%d\n卡牌「%s」+1%s" % [battle_money, _card_name(battle_card_id), board_text]
		"boss":
			return "Boss 已被击败\n管线重新运转\nMVP 流程通关"
		_:
			return "打开物资箱\n金钱 +%d\n卡牌「%s」+1" % [chest_money, _card_name(chest_card_id)]

func _accept() -> void:
	if applied:
		return
	applied = true
	match kind:
		"battle":
			run_state.money += battle_money
			run_state.add_card(battle_card_id)
			if normal_battle_unlocks_board:
				run_state.board_level = maxi(run_state.board_level, 1)
			run_state.map_progress = maxi(run_state.map_progress, 2)
		"boss":
			run_state.boss_defeated = true
		_:
			run_state.money += chest_money
			run_state.add_card(chest_card_id)
			run_state.mark_event(String(scene_context.get("event_id", "chest_01")))
			run_state.map_progress = maxi(run_state.map_progress, 1)
	run_state.save_game()
	if kind == "boss":
		send_command("victory")
	else:
		send_command("map")

func _card_name(card_id: StringName) -> String:
	var card: Resource = card_catalog.get_card(card_id)
	return card.display_name if card != null else String(card_id)
