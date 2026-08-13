extends Control

## A self-contained playable/debuggable Core-00 fight.  It intentionally uses
## no existing BattleManager, so it can be exported and tested independently.

@onready var manager = $Core00EnermyMannager
@onready var phase_label: Label = %PhaseLabel
@onready var hp_label: Label = %HpLabel
@onready var boss_label: Label = %BossLabel
@onready var logic_label: Label = %LogicLabel
@onready var intent_label: Label = %IntentLabel
@onready var log_label: RichTextLabel = %LogLabel
@onready var target_option: OptionButton = %TargetOption
@onready var damage_option: OptionButton = %DamageOption
@onready var cells: HBoxContainer = %Cells
@onready var move_left_button: Button = %MoveLeft
@onready var move_right_button: Button = %MoveRight
@onready var attack_button: Button = %Attack
@onready var end_turn_button: Button = %EndTurn

var _selected_target: StringName = &"true_hand"
var _cell_buttons: Array[Button] = []
var _combat_over: bool = false
var _demo_cards: Array = [
	{"name": "拳套", "cooldown_remaining": 0},
	{"name": "线圈铳", "cooldown_remaining": 0},
	{"name": "机械鞋", "cooldown_remaining": 0},
]


func _ready() -> void:
	for cell_index in range(1, 13):
		var button := Button.new()
		button.custom_minimum_size = Vector2(112, 126)
		button.text = str(cell_index)
		button.focus_mode = Control.FOCUS_NONE
		button.pressed.connect(_on_cell_clicked.bind(cell_index))
		cells.add_child(button)
		_cell_buttons.append(button)
	for damage in [3, 5, 9, 12, 20]:
		damage_option.add_item("%d 伤害" % damage, damage)
	damage_option.select(1)
	target_option.item_selected.connect(_on_target_selected)
	move_left_button.pressed.connect(_move_player.bind(-1))
	move_right_button.pressed.connect(_move_player.bind(1))
	attack_button.pressed.connect(_attack_target)
	end_turn_button.pressed.connect(_end_turn)
	manager.phase_changed.connect(_on_phase_changed)
	manager.player_damage_requested.connect(_on_player_damage_requested)
	manager.card_jam_requested.connect(_on_card_jam_requested)
	manager.boss_defeated.connect(_on_boss_defeated)
	manager.reset_encounter(6, 60)
	_rebuild_target_options()
	_log("战斗开始。先移动观察奇偶格，再选择 True 或 False 进行测试攻击。")
	_refresh()


func _unhandled_key_input(event: InputEvent) -> void:
	if not event.is_pressed() or event.is_echo() or _combat_over:
		return
	if event.keycode in [KEY_A, KEY_LEFT]:
		_move_player(-1)
	elif event.keycode in [KEY_D, KEY_RIGHT]:
		_move_player(1)
	elif event.keycode == KEY_SPACE:
		_end_turn()


func _move_player(direction: int) -> void:
	if _combat_over:
		return
	var state: Dictionary = manager.get_state()
	var minimum := 2 if state["phase"] == manager.Phase.HANDS else 1
	var maximum := 11 if state["phase"] == manager.Phase.HANDS else 12
	var destination := clampi(int(state["player_position"]) + direction, minimum, maximum)
	if destination == int(state["player_position"]):
		_log("边界：这个方向已经没有可移动格。")
		return
	manager.set_debug_snapshot({"player_position": destination})
	_log("玩家移动到 %d 号格（%s格）。" % [destination, "偶数" if destination % 2 == 0 else "奇数"])
	_refresh()


func _on_cell_clicked(cell_index: int) -> void:
	var current := int(manager.get_state()["player_position"])
	if absi(cell_index - current) != 1:
		_log("本演示每次只能移动 1 格；请点击相邻格。")
		return
	_move_player(1 if cell_index > current else -1)


func _attack_target() -> void:
	if _combat_over:
		return
	var damage := damage_option.get_item_id(damage_option.selected)
	var dealt: int = manager.take_damage(_selected_target, damage)
	_log("玩家攻击 %s：输入 %d，实际削减耐久 %d。" % [_target_display_name(_selected_target), damage, dealt])
	_rebuild_target_options()
	_refresh()


func _end_turn() -> void:
	if _combat_over:
		return
	var preview: Dictionary = manager.refresh_intent_preview()
	_log("敌人执行：%s" % String(preview.get("intent_text", "等待")))
	var result: Dictionary = manager.advance_enemy_turn_for_test()
	if int(result.get("pending_card_jam_turns", 0)) > 0:
		manager.begin_player_turn(_demo_cards)
		_log("过载干扰生效：%s" % _card_cooldown_text())
	_rebuild_target_options()
	_refresh()
	if int(manager.get_state()["player_hp"]) <= 0:
		_combat_over = true
		_log("[color=#ff7777]玩家生命归零。按“重新开始”再次挑战。[/color]")
		_set_action_buttons_enabled(false)


func _on_target_selected(index: int) -> void:
	_selected_target = StringName(target_option.get_item_metadata(index))
	_refresh()


func _on_phase_changed(next_phase: int) -> void:
	if next_phase == manager.Phase.BODY:
		_selected_target = &"body"
		_log("[color=#71e7ff]双手均被摧毁。Core-00 本体出现：50 HP。[/color]")
	_rebuild_target_options()
	_refresh()


func _on_player_damage_requested(amount: int, source_id: StringName) -> void:
	_log("[color=#ffad79]Core-00 的 %s 对玩家造成 %d 伤害。[/color]" % [String(source_id), amount])


func _on_card_jam_requested(turns: int) -> void:
	_log("[color=#df8cff]检测到全卡干扰：下一玩家回合至少冷却 %d。[/color]" % turns)


func _on_boss_defeated() -> void:
	_combat_over = true
	_log("[color=#7affb2]Core-00 被击败！完整两阶段演示结束。[/color]")
	_set_action_buttons_enabled(false)


func _restart() -> void:
	_combat_over = false
	_demo_cards = [
		{"name": "拳套", "cooldown_remaining": 0},
		{"name": "线圈铳", "cooldown_remaining": 0},
		{"name": "机械鞋", "cooldown_remaining": 0},
	]
	manager.reset_encounter(6, 60)
	_selected_target = &"true_hand"
	_rebuild_target_options()
	_set_action_buttons_enabled(true)
	log_label.clear()
	_log("战斗已重新开始。")
	_refresh()


func _refresh() -> void:
	if not is_instance_valid(manager) or _cell_buttons.is_empty():
		return
	var state: Dictionary = manager.get_state()
	var preview: Dictionary = manager.refresh_intent_preview()
	phase_label.text = "PHASE %d · 回合 %d" % [int(state["phase"]), int(state["round_number"])]
	hp_label.text = "玩家 HP  %d / 60" % int(state["player_hp"])
	logic_label.text = "逻辑标记  %s    卡牌  %s" % [String(state["player_logic_state"]).to_upper(), _card_cooldown_text()]
	if state["phase"] == manager.Phase.HANDS:
		var true_state: Dictionary = state["true_hand"]
		var false_state: Dictionary = state["false_hand"]
		boss_label.text = "TRUE  %d/21  +%d盾    ·    FALSE  %d/21  +%d盾" % [
			int(true_state["hp"]), int(true_state["shield"]),
			int(false_state["hp"]), int(false_state["shield"]),
		]
	else:
		var body_state: Dictionary = state["body"]
		boss_label.text = "CORE-00 本体  %d/50  +%d盾    ·    距离 %d" % [
			int(body_state["hp"]), int(body_state["shield"]), int(state["distance"]),
		]
	intent_label.text = "预告意图\n%s" % String(preview.get("intent_text", "-"))
	for index in range(_cell_buttons.size()):
		var cell_number := index + 1
		var marks: Array[String] = [str(cell_number)]
		if cell_number == int(state["player_position"]):
			marks.append("◆ 玩家")
		if state["phase"] == manager.Phase.HANDS:
			if bool(state["true_hand"]["active"]) and cell_number == int(state["true_hand"]["position"]):
				marks.append("T")
			if bool(state["false_hand"]["active"]) and cell_number == int(state["false_hand"]["position"]):
				marks.append("F")
		elif state["phase"] == manager.Phase.BODY and cell_number == int(state["body"]["position"]):
			marks.append("● BOSS")
		_cell_buttons[index].text = "\n".join(marks)
		_cell_buttons[index].disabled = _combat_over
	var phase_one: bool = int(state["phase"]) == int(manager.Phase.HANDS)
	move_left_button.disabled = _combat_over or int(state["player_position"]) <= (2 if phase_one else 1)
	move_right_button.disabled = _combat_over or int(state["player_position"]) >= (11 if phase_one else 12)


func _rebuild_target_options() -> void:
	if not is_instance_valid(target_option):
		return
	var state: Dictionary = manager.get_state()
	target_option.clear()
	if state["phase"] == manager.Phase.HANDS:
		if bool(state["true_hand"]["active"]):
			_add_target("True 手", &"true_hand")
		if bool(state["false_hand"]["active"]):
			_add_target("False 手", &"false_hand")
	else:
		_add_target("Core-00 本体", &"body")
	for index in range(target_option.item_count):
		if StringName(target_option.get_item_metadata(index)) == _selected_target:
			target_option.select(index)
			return
	if target_option.item_count > 0:
		target_option.select(0)
		_selected_target = StringName(target_option.get_item_metadata(0))


func _add_target(label: String, id: StringName) -> void:
	var index := target_option.item_count
	target_option.add_item(label)
	target_option.set_item_metadata(index, id)


func _target_display_name(id: StringName) -> String:
	match id:
		&"true_hand": return "True 手"
		&"false_hand": return "False 手"
		&"body": return "Core-00 本体"
		_: return String(id)


func _card_cooldown_text() -> String:
	var parts: Array[String] = []
	for card: Dictionary in _demo_cards:
		parts.append("%s:%d" % [String(card.get("name", "?")), int(card.get("cooldown_remaining", 0))])
	return " / ".join(parts)


func _set_action_buttons_enabled(enabled: bool) -> void:
	move_left_button.disabled = not enabled
	move_right_button.disabled = not enabled
	attack_button.disabled = not enabled
	end_turn_button.disabled = not enabled


func _log(message: String) -> void:
	log_label.append_text("• %s\n" % message)
	log_label.scroll_to_line(maxi(0, log_label.get_line_count() - 1))
