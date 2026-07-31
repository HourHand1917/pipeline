class_name MvpBattleScene
extends "res://Script/MVP/mvp_scene_base.gd"

const BoardModel = preload("res://Script/MVP/mvp_board_model.gd")

@export_group("战斗数值 - Inspector 可调")
@export_range(1, 20, 1) var energy_per_turn: int = 3
@export_range(0, 10, 1) var move_energy_cost: int = 1
@export_range(3, 20, 1) var track_cell_count: int = 9
@export_range(0, 20, 1) var player_start_position: int = 1
@export_range(0, 20, 1) var enemy_start_position: int = 6
@export_range(1, 999, 1) var normal_enemy_hp: int = 24
@export_range(1, 999, 1) var boss_enemy_hp: int = 45
@export_range(0, 99, 1) var normal_enemy_damage: int = 5
@export_range(0, 99, 1) var boss_enemy_damage: int = 8
@export_range(0, 20, 1) var enemy_attack_range: int = 2
@export_range(0, 20, 1) var enemy_move_amount: int = 1
@export var preserve_partial_charge: bool = true
@export var clear_player_shield_each_round: bool = true

@export_group("敌人 AI - Inspector 可展开编辑")
@export var enemy_ai_profiles: Array[Resource] = []
@export var boss_ai_profile: Resource
@export_range(0, 20, 1) var debug_enemy_profile_index: int = 0
@export var allow_runtime_ai_switch: bool = true

@export_group("视觉")
@export var backdrop_texture: Texture2D
@export var charged_color: Color = Color("6dfff0")
@export var empty_color: Color = Color("17313a")

var is_boss: bool
var event_id: String
var board: RefCounted
var player_hp: int
var player_max_hp: int
var player_shield: int
var enemy_hp: int
var enemy_max_hp: int
var enemy_shield: int
var enemy_damage: int
var energy: int
var round_number: int = 1
var player_position: int
var enemy_position: int
var charged_cells: Dictionary = {}
var cooldowns: Dictionary = {}
var battle_ended: bool = false
var player_buffs: Dictionary = {}
var player_debuffs: Dictionary = {}
var enemy_debuffs: Dictionary = {}
var active_ai_profile: Resource
var player_facing: int = 1
var enemy_facing: int = -1
var flip_animation_pending: bool = false

var player_status: Label
var enemy_status: Label
var turn_status: Label
var distance_status: Label
var track_box: HBoxContainer
var grid: GridContainer
var ready_box: VBoxContainer
var log_label: RichTextLabel
var grid_buttons: Array[Button] = []
var ai_selector: OptionButton
var ai_summary_label: Label
var player_actor_label: Label
var enemy_actor_label: Label

func _ready() -> void:
	set_full_rect(self)
	is_boss = bool(scene_context.get("boss", false))
	event_id = String(scene_context.get("event_id", "battle"))
	var board_dimensions: Vector2i = run_state.board_size()
	board = BoardModel.new(board_dimensions.x, board_dimensions.y, card_catalog, run_state.saved_build)
	if board.entries.is_empty():
		_add_emergency_card()
	player_hp = run_state.current_hp
	player_max_hp = run_state.max_hp
	player_shield = run_state.shield
	_select_initial_ai()
	enemy_max_hp = int(active_ai_profile.max_hp) if active_ai_profile != null else (boss_enemy_hp if is_boss else normal_enemy_hp)
	enemy_hp = enemy_max_hp
	enemy_shield = int(active_ai_profile.starting_shield) if active_ai_profile != null else 0
	enemy_damage = int(active_ai_profile.base_damage) if active_ai_profile != null else (boss_enemy_damage if is_boss else normal_enemy_damage)
	energy = energy_per_turn
	player_position = clampi(player_start_position, 0, track_cell_count - 1)
	enemy_position = clampi(enemy_start_position, 0, track_cell_count - 1)
	if player_position == enemy_position:
		enemy_position = mini(track_cell_count - 1, player_position + 1)
	_update_facing(false)
	_build_ui()
	_log("遭遇「%s」。点击构筑格消耗能量并充能；卡牌全部占格点亮后可发动。" % _enemy_name())
	_refresh_all()

func _build_ui() -> void:
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("050b10")
	add_child(background)
	add_screen_frame(self, charged_color)
	if backdrop_texture != null:
		var texture := TextureRect.new()
		set_full_rect(texture)
		texture.texture = backdrop_texture
		texture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		texture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		texture.modulate = Color(1, 1, 1, 0.15)
		add_child(texture)
	var margin := MarginContainer.new()
	set_full_rect(margin)
	margin.add_theme_constant_override("margin_left", 48)
	margin.add_theme_constant_override("margin_right", 390)
	margin.add_theme_constant_override("margin_top", 34)
	margin.add_theme_constant_override("margin_bottom", 34)
	add_child(margin)
	var root_box := VBoxContainer.new()
	root_box.add_theme_constant_override("separation", 12)
	margin.add_child(root_box)
	root_box.add_child(make_title("回合战斗 · %s" % ("Boss" if is_boss else "普通敌人"), 34))
	var status_bar := HBoxContainer.new()
	status_bar.alignment = BoxContainer.ALIGNMENT_CENTER
	status_bar.add_theme_constant_override("separation", 28)
	root_box.add_child(status_bar)
	player_status = Label.new()
	enemy_status = Label.new()
	turn_status = Label.new()
	distance_status = Label.new()
	for label in [player_status, enemy_status, turn_status, distance_status]:
		label.add_theme_font_size_override("font_size", 20)
		status_bar.add_child(label)
	var track_panel := make_panel(Color(0.035, 0.09, 0.11, 0.94))
	root_box.add_child(track_panel)
	track_box = HBoxContainer.new()
	track_box.alignment = BoxContainer.ALIGNMENT_CENTER
	track_box.add_theme_constant_override("separation", 6)
	track_panel.add_child(track_box)
	var content := HBoxContainer.new()
	content.size_flags_vertical = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", 18)
	root_box.add_child(content)
	var board_panel := make_panel()
	board_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_child(board_panel)
	var board_box := VBoxContainer.new()
	board_box.alignment = BoxContainer.ALIGNMENT_CENTER
	board_box.add_theme_constant_override("separation", 10)
	board_panel.add_child(board_box)
	var board_title := Label.new()
	board_title.text = "构筑充能板"
	board_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	board_title.add_theme_font_size_override("font_size", 23)
	board_box.add_child(board_title)
	grid = GridContainer.new()
	grid.columns = board.width
	grid.add_theme_constant_override("h_separation", 7)
	grid.add_theme_constant_override("v_separation", 7)
	board_box.add_child(grid)
	for index in range(board.width * board.height):
		var cell := Vector2i(index % board.width, index / board.width)
		var button := Button.new()
		button.custom_minimum_size = Vector2(138, 138)
		button.pressed.connect(func(): _charge_cell(cell))
		grid.add_child(button)
		grid_buttons.append(button)
	var move_bar := HBoxContainer.new()
	move_bar.alignment = BoxContainer.ALIGNMENT_CENTER
	move_bar.add_theme_constant_override("separation", 8)
	board_box.add_child(move_bar)
	move_bar.add_child(make_button("后退 1 格", func(): _move_player(-1), Vector2(130, 44)))
	move_bar.add_child(make_button("前进 1 格", func(): _move_player(1), Vector2(130, 44)))
	move_bar.add_child(make_button("结束回合", _end_turn, Vector2(130, 44)))
	var side_panel := make_panel()
	side_panel.custom_minimum_size.x = 455
	content.add_child(side_panel)
	var side := VBoxContainer.new()
	side.add_theme_constant_override("separation", 10)
	side_panel.add_child(side)
	var ai_title := Label.new()
	ai_title.text = "敌人 AI 调试"
	ai_title.add_theme_font_size_override("font_size", 20)
	side.add_child(ai_title)
	ai_selector = OptionButton.new()
	ai_selector.custom_minimum_size.y = 38
	var profiles := _available_ai_profiles()
	for profile in profiles:
		ai_selector.add_item(String(profile.display_name))
		if profile == active_ai_profile:
			ai_selector.select(ai_selector.item_count - 1)
	ai_selector.disabled = not allow_runtime_ai_switch
	ai_selector.item_selected.connect(_switch_ai_profile)
	side.add_child(ai_selector)
	ai_summary_label = Label.new()
	ai_summary_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	ai_summary_label.custom_minimum_size.y = 54
	side.add_child(ai_summary_label)
	var ready_title := Label.new()
	ready_title.text = "已就绪卡牌"
	ready_title.add_theme_font_size_override("font_size", 22)
	side.add_child(ready_title)
	ready_box = VBoxContainer.new()
	ready_box.custom_minimum_size.y = 150
	side.add_child(ready_box)
	log_label = RichTextLabel.new()
	log_label.bbcode_enabled = true
	log_label.custom_minimum_size = Vector2(390, 210)
	log_label.scroll_following = true
	side.add_child(log_label)
	side.add_child(make_button("调试：敌人受 10 伤害", func(): _damage_enemy(10), Vector2(260, 40)))
	side.add_child(make_button("调试：立即执行敌人 AI", _enemy_turn, Vector2(260, 40)))
	side.add_child(make_button("撤退到地图", _retreat, Vector2(260, 40)))

func _charge_cell(cell: Vector2i) -> void:
	if battle_ended:
		return
	var entry_index: int = board.entry_index_at(cell)
	if entry_index < 0:
		_log("这个格子没有卡牌。")
		return
	if int(cooldowns.get(entry_index, 0)) > 0:
		_log("该卡牌仍在冷却。")
		return
	if charged_cells.has(cell):
		_log("这个格子已经点亮。")
		return
	if energy <= 0:
		_log("本回合能量不足。")
		return
	energy -= 1
	charged_cells[cell] = true
	_log("点亮格 %d,%d，剩余能量 %d。" % [cell.x, cell.y, energy])
	_refresh_all()

func _ready_entry_indices() -> Array[int]:
	var result: Array[int] = []
	for index in range(board.entries.size()):
		if int(cooldowns.get(index, 0)) > 0:
			continue
		var is_card_ready := true
		for cell in board.cells_for_entry(board.entries[index]):
			if not charged_cells.has(cell):
				is_card_ready = false
				break
		if is_card_ready:
			result.append(index)
	return result

func _activate_card(index: int) -> void:
	if battle_ended or index not in _ready_entry_indices():
		return
	var entry: Dictionary = board.entries[index]
	var card: Resource = card_catalog.get_card(StringName(entry.get("card_id", "")))
	if card == null:
		return
	var distance := _distance()
	if _card_requires_current_range(card) and (distance < card.min_range or distance > card.max_range):
		_log("「%s」射程 %d-%d，当前距离 %d，无法命中。" % [card.display_name, card.min_range, card.max_range, distance])
		return
	var direct_damage := int(card.power) + _strength_amount()
	match card.effect_type:
		"damage":
			_damage_enemy(direct_damage, false)
			_log("发动「%s」，对敌人造成 %d 伤害。" % [card.display_name, direct_damage])
		"shield":
			player_shield += card.power
			_log("发动「%s」，获得 %d 护盾。" % [card.display_name, card.power])
		"energy":
			energy += card.power
			_log("发动「%s」，恢复 %d 能量。" % [card.display_name, card.power])
		"heal":
			var old_hp := player_hp
			player_hp = mini(player_max_hp, player_hp + card.power)
			_log("发动「%s」，恢复 %d 生命。" % [card.display_name, player_hp - old_hp])
		"knockback":
			_damage_enemy(direct_damage, false)
			_move_enemy_away(int(card.move_amount))
			_log("发动「%s」，造成 %d 伤害并击退 %d 格。" % [card.display_name, direct_damage, card.move_amount])
		"teleport_through":
			if not _teleport_player_through(int(card.move_amount)):
				_log("怪物身后没有落脚点，瞬移失败。")
				return
			_log("发动「%s」，跨越怪物并翻转双方朝向。" % card.display_name)
		"advance_attack":
			_move_player_toward(int(card.move_amount))
			if _distance() >= int(card.min_range) and _distance() <= int(card.max_range):
				_damage_enemy(direct_damage, false)
				_log("发动「%s」，前进后造成 %d 伤害。" % [card.display_name, direct_damage])
			else:
				_log("「%s」完成突进，但距离 %d 未进入刺击射程。" % [card.display_name, _distance()])
		"poison":
			_damage_enemy(direct_damage, false)
			enemy_debuffs["poison"] = {
				"amount": maxi(int(card.status_amount), int(enemy_debuffs.get("poison", {}).get("amount", 0))),
				"turns": maxi(int(card.status_turns), int(enemy_debuffs.get("poison", {}).get("turns", 0))),
			}
			_log("发动「%s」，造成 %d 伤害并施加毒 %d×%d回合。" % [card.display_name, direct_damage, card.status_amount, card.status_turns])
		"cleanse":
			var removed_count := player_debuffs.size()
			player_debuffs.clear()
			_log("发动「%s」，净化 %d 个Debuff。" % [card.display_name, removed_count])
		"strength":
			player_buffs["strength"] = {
				"amount": maxi(int(card.status_amount), int(player_buffs.get("strength", {}).get("amount", 0))),
				"turns": maxi(int(card.status_turns), int(player_buffs.get("strength", {}).get("turns", 0))),
			}
			_log("发动「%s」，力量 +%d，持续 %d 回合。" % [card.display_name, card.status_amount, card.status_turns])
	for cell in board.cells_for_entry(entry):
		charged_cells.erase(cell)
	cooldowns[index] = card.cooldown_turns
	_refresh_all()
	_check_end()

func _move_player(direction: int) -> void:
	if battle_ended:
		return
	if energy < move_energy_cost:
		_log("移动需要 %d 能量。" % move_energy_cost)
		return
	var target := clampi(player_position + direction, 0, track_cell_count - 1)
	if target == player_position or target == enemy_position:
		_log("边界或敌人阻挡，无法移动。")
		return
	energy -= move_energy_cost
	player_position = target
	_update_facing(false)
	_log("玩家移动到 %d，当前距离 %d。" % [player_position, _distance()])
	_refresh_all()

func _move_player_toward(amount: int) -> int:
	var moved := 0
	var direction := signi(enemy_position - player_position)
	for _step in range(maxi(0, amount)):
		var candidate := player_position + direction
		if candidate == enemy_position or candidate < 0 or candidate >= track_cell_count:
			break
		player_position = candidate
		moved += 1
	_update_facing(false)
	return moved

func _move_enemy_away(amount: int) -> int:
	var moved := 0
	var direction := signi(enemy_position - player_position)
	if direction == 0:
		direction = 1
	for _step in range(maxi(0, amount)):
		var candidate := enemy_position + direction
		if candidate < 0 or candidate >= track_cell_count or candidate == player_position:
			break
		enemy_position = candidate
		moved += 1
	_update_facing(false)
	return moved

func _move_enemy_toward(amount: int) -> int:
	var moved := 0
	var direction := signi(player_position - enemy_position)
	for _step in range(maxi(0, amount)):
		var candidate := enemy_position + direction
		if candidate < 0 or candidate >= track_cell_count or candidate == player_position:
			break
		enemy_position = candidate
		moved += 1
	_update_facing(false)
	return moved

func _move_enemy_away_from_player(amount: int) -> int:
	return _move_enemy_away(amount)

func _teleport_player_through(landing_distance: int) -> bool:
	var through_direction := signi(enemy_position - player_position)
	if through_direction == 0:
		return false
	var target := enemy_position + through_direction * maxi(1, landing_distance)
	if target < 0 or target >= track_cell_count or target == enemy_position:
		return false
	player_position = target
	_update_facing(true)
	return true

func _update_facing(animate_flip: bool) -> void:
	var old_player_facing := player_facing
	var old_enemy_facing := enemy_facing
	player_facing = signi(enemy_position - player_position)
	enemy_facing = signi(player_position - enemy_position)
	if player_facing == 0:
		player_facing = old_player_facing
	if enemy_facing == 0:
		enemy_facing = old_enemy_facing
	if animate_flip and (player_facing != old_player_facing or enemy_facing != old_enemy_facing):
		flip_animation_pending = true

func _end_turn() -> void:
	if battle_ended:
		return
	_log("玩家结束回合。")
	if not preserve_partial_charge:
		charged_cells.clear()
	_enemy_turn()
	if _check_end():
		return
	round_number += 1
	if clear_player_shield_each_round:
		player_shield = 0
	_tick_player_debuffs()
	if _check_end():
		return
	_tick_buff_durations()
	energy = energy_per_turn
	for key in cooldowns.keys():
		cooldowns[key] = maxi(0, int(cooldowns[key]) - 1)
	_log("第 %d 回合开始，能量恢复为 %d。" % [round_number, energy])
	_refresh_all()

func _enemy_turn() -> void:
	if battle_ended:
		return
	_tick_enemy_debuffs()
	if _check_end():
		return
	var behavior := "chaser"
	var move_amount := enemy_move_amount
	var preferred_min := 1
	var special_interval := 2
	if active_ai_profile != null:
		behavior = String(active_ai_profile.behavior)
		move_amount = int(active_ai_profile.move_amount)
		preferred_min = int(active_ai_profile.preferred_min_range)
		special_interval = maxi(1, int(active_ai_profile.special_every_n_turns))
	match behavior:
		"sniper":
			if _distance() < preferred_min:
				var retreated := _move_enemy_away_from_player(move_amount)
				_log("%s拉开距离，后退 %d 格。" % [_enemy_name(), retreated])
			elif _profile_attack_available():
				_enemy_attack(false)
			else:
				var moved := _move_enemy_toward(move_amount)
				_log("%s寻找射击位置，靠近 %d 格。" % [_enemy_name(), moved])
		"poisoner":
			if _profile_attack_available():
				_enemy_attack(true)
			else:
				var moved := _move_enemy_toward(move_amount)
				_log("%s靠近玩家 %d 格，准备施毒。" % [_enemy_name(), moved])
		"skirmisher":
			if _profile_attack_available():
				_enemy_attack(false)
				var retreated := _move_enemy_away_from_player(move_amount)
				_log("%s攻击后撤退 %d 格。" % [_enemy_name(), retreated])
			else:
				var moved := _move_enemy_toward(move_amount)
				_log("%s快速接近 %d 格。" % [_enemy_name(), moved])
		"guardian":
			if round_number % special_interval == 0:
				var shield_gain := int(active_ai_profile.shield_amount) if active_ai_profile != null else 4
				enemy_shield += shield_gain
				_log("%s进入防御姿态，获得 %d 护盾。" % [_enemy_name(), shield_gain])
			elif _profile_attack_available():
				_enemy_attack(false)
			else:
				var moved := _move_enemy_toward(move_amount)
				_log("%s稳步推进 %d 格。" % [_enemy_name(), moved])
		_:
			if _profile_attack_available():
				_enemy_attack(false)
			else:
				var moved := _move_enemy_toward(move_amount)
				_log("%s向玩家靠近 %d 格。" % [_enemy_name(), moved])
	_refresh_all()
	_check_end()

func _enemy_attack(apply_poison: bool) -> void:
	var blocked := mini(player_shield, enemy_damage)
	player_shield -= blocked
	var hp_damage := enemy_damage - blocked
	player_hp = maxi(0, player_hp - hp_damage)
	_log("%s攻击：护盾吸收 %d，生命损失 %d。" % [_enemy_name(), blocked, hp_damage])
	if apply_poison and active_ai_profile != null and int(active_ai_profile.poison_amount) > 0:
		var current: Dictionary = player_debuffs.get("poison", {})
		player_debuffs["poison"] = {
			"amount": maxi(int(active_ai_profile.poison_amount), int(current.get("amount", 0))),
			"turns": maxi(int(active_ai_profile.poison_turns), int(current.get("turns", 0))),
		}
		_log("玩家中毒：每回合 %d 点，持续 %d 回合。" % [active_ai_profile.poison_amount, active_ai_profile.poison_turns])

func _profile_attack_available() -> bool:
	var minimum := 0
	var maximum := enemy_attack_range
	if active_ai_profile != null:
		minimum = int(active_ai_profile.attack_min_range)
		maximum = int(active_ai_profile.attack_max_range)
	return _distance() >= minimum and _distance() <= maximum

func _damage_enemy(amount: int, check_now: bool = true) -> void:
	var incoming := maxi(0, amount)
	var blocked := mini(enemy_shield, incoming)
	enemy_shield -= blocked
	var hp_damage := incoming - blocked
	enemy_hp = maxi(0, enemy_hp - hp_damage)
	if blocked > 0:
		_log("%s护盾吸收 %d 点伤害。" % [_enemy_name(), blocked])
	_refresh_all()
	if check_now:
		_check_end()

func _tick_enemy_debuffs() -> void:
	if not enemy_debuffs.has("poison"):
		return
	var poison: Dictionary = enemy_debuffs["poison"]
	var amount := maxi(0, int(poison.get("amount", 0)))
	var turns := maxi(0, int(poison.get("turns", 0)))
	if amount > 0 and turns > 0:
		enemy_hp = maxi(0, enemy_hp - amount)
		turns -= 1
		_log("%s受到 %d 点毒伤，剩余 %d 回合。" % [_enemy_name(), amount, turns])
	if turns <= 0:
		enemy_debuffs.erase("poison")
	else:
		poison["turns"] = turns
		enemy_debuffs["poison"] = poison

func _tick_player_debuffs() -> void:
	if not player_debuffs.has("poison"):
		return
	var poison: Dictionary = player_debuffs["poison"]
	var amount := maxi(0, int(poison.get("amount", 0)))
	var turns := maxi(0, int(poison.get("turns", 0)))
	if amount > 0 and turns > 0:
		player_hp = maxi(0, player_hp - amount)
		turns -= 1
		_log("玩家受到 %d 点毒伤，剩余 %d 回合。" % [amount, turns])
	if turns <= 0:
		player_debuffs.erase("poison")
	else:
		poison["turns"] = turns
		player_debuffs["poison"] = poison

func _tick_buff_durations() -> void:
	var expired: Array[String] = []
	for key in player_buffs.keys():
		var buff: Dictionary = player_buffs[key]
		var turns := int(buff.get("turns", 0)) - 1
		if turns <= 0:
			expired.append(String(key))
		else:
			buff["turns"] = turns
			player_buffs[key] = buff
	for key in expired:
		player_buffs.erase(key)

func _check_end() -> bool:
	if battle_ended:
		return true
	if enemy_hp <= 0:
		battle_ended = true
		run_state.current_hp = player_hp
		run_state.shield = 0
		run_state.mark_event(event_id)
		if is_boss:
			run_state.boss_defeated = true
		else:
			run_state.map_progress = maxi(run_state.map_progress, 2)
		run_state.save_game()
		_log("战斗胜利。")
		send_command("reward", {"kind": "boss" if is_boss else "battle", "event_id": event_id})
		return true
	if player_hp <= 0:
		battle_ended = true
		run_state.current_hp = 0
		run_state.save_game()
		_log("玩家倒下。")
		send_command("game_over")
		return true
	return false

func _retreat() -> void:
	run_state.current_hp = player_hp
	run_state.shield = 0
	run_state.save_game()
	send_command("map")

func _distance() -> int:
	return absi(enemy_position - player_position)

func _refresh_all() -> void:
	if player_status == null:
		return
	player_status.text = "玩家 HP %d/%d　盾 %d%s" % [player_hp, player_max_hp, player_shield, _status_summary(player_buffs, player_debuffs)]
	enemy_status.text = "%s HP %d/%d　盾 %d%s" % [_enemy_name(), enemy_hp, enemy_max_hp, enemy_shield, _status_summary({}, enemy_debuffs)]
	turn_status.text = "回合 %d　能量 %d" % [round_number, energy]
	distance_status.text = "距离 |%d-%d| = %d" % [enemy_position, player_position, _distance()]
	_refresh_track()
	_refresh_grid()
	_refresh_ready()
	_refresh_ai_summary()

func _refresh_track() -> void:
	for child in track_box.get_children():
		child.queue_free()
	player_actor_label = null
	enemy_actor_label = null
	for index in range(track_cell_count):
		var label := Label.new()
		label.custom_minimum_size = Vector2(94, 64)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		if index == player_position:
			label.text = ("玩家▶\n%d" if player_facing > 0 else "◀玩家\n%d") % index
			player_actor_label = label
		elif index == enemy_position:
			label.text = ("%s▶\n%d" if enemy_facing > 0 else "◀%s\n%d") % [_enemy_name(), index]
			enemy_actor_label = label
		else:
			label.text = str(index)
		var style := StyleBoxFlat.new()
		style.bg_color = Color("266d7a") if index == player_position else (Color("8b3541") if index == enemy_position else Color("172832"))
		style.set_corner_radius_all(6)
		label.add_theme_stylebox_override("normal", style)
		track_box.add_child(label)
	if flip_animation_pending:
		flip_animation_pending = false
		call_deferred("_play_actor_flip_animation")

func _play_actor_flip_animation() -> void:
	for actor in [player_actor_label, enemy_actor_label]:
		if actor == null or not is_instance_valid(actor):
			continue
		actor.pivot_offset = actor.size * 0.5
		actor.scale = Vector2(0.08, 1.0)
		var tween := create_tween()
		tween.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		tween.tween_property(actor, "scale", Vector2.ONE, 0.24)

func _refresh_grid() -> void:
	var occupied: Dictionary = board.occupied_cells()
	for index in range(grid_buttons.size()):
		var cell := Vector2i(index % board.width, index / board.width)
		var button := grid_buttons[index]
		button.text = "空"
		button.disabled = not occupied.has(cell)
		button.modulate = empty_color
		if occupied.has(cell):
			var entry_index := int(occupied[cell])
			var entry: Dictionary = board.entries[entry_index]
			var card: Resource = card_catalog.get_card(StringName(entry.get("card_id", "")))
			button.text = card.display_name if card != null else "卡牌"
			if int(cooldowns.get(entry_index, 0)) > 0:
				button.text += "\n冷却 %d" % int(cooldowns[entry_index])
				button.disabled = true
				button.modulate = Color("59636a")
			elif charged_cells.has(cell):
				button.text += "\n已点亮"
				button.modulate = charged_color
			elif card != null:
				button.modulate = card.color.darkened(0.25)

func _refresh_ready() -> void:
	for child in ready_box.get_children():
		child.queue_free()
	var ready_indices := _ready_entry_indices()
	if ready_indices.is_empty():
		var label := Label.new()
		label.text = "暂无。点亮一张卡占用的全部格子。"
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		ready_box.add_child(label)
		return
	for index in ready_indices:
		var entry: Dictionary = board.entries[index]
		var card: Resource = card_catalog.get_card(StringName(entry.get("card_id", "")))
		var button := make_button("发动「%s」\n%s" % [card.display_name, card.description], func(): _activate_card(index), Vector2(370, 58))
		button.modulate = card.color
		ready_box.add_child(button)

func _log(text: String) -> void:
	if log_label != null:
		log_label.append_text("• " + text + "\n")

func _card_requires_current_range(card: Resource) -> bool:
	return String(card.effect_type) in ["damage", "knockback", "poison"]

func _strength_amount() -> int:
	if not player_buffs.has("strength"):
		return 0
	return maxi(0, int(player_buffs["strength"].get("amount", 0)))

func _status_summary(buffs: Dictionary, debuffs: Dictionary) -> String:
	var pieces: Array[String] = []
	if buffs.has("strength"):
		var strength: Dictionary = buffs["strength"]
		pieces.append("力量+%d(%d回合)" % [strength.get("amount", 0), strength.get("turns", 0)])
	if debuffs.has("poison"):
		var poison: Dictionary = debuffs["poison"]
		pieces.append("中毒%d×%d" % [poison.get("amount", 0), poison.get("turns", 0)])
	return "" if pieces.is_empty() else "　[" + "，".join(pieces) + "]"

func _available_ai_profiles() -> Array[Resource]:
	var result: Array[Resource] = []
	for profile in enemy_ai_profiles:
		if profile != null:
			result.append(profile)
	if boss_ai_profile != null and boss_ai_profile not in result:
		result.append(boss_ai_profile)
	return result

func _select_initial_ai() -> void:
	var profiles := _available_ai_profiles()
	if profiles.is_empty():
		active_ai_profile = null
		return
	if is_boss and boss_ai_profile != null:
		active_ai_profile = boss_ai_profile
		return
	var requested_id := StringName(scene_context.get("ai_profile_id", &""))
	if not String(requested_id).is_empty():
		for profile in profiles:
			if profile.ai_id == requested_id:
				active_ai_profile = profile
				return
	var requested_index := int(scene_context.get("ai_profile_index", debug_enemy_profile_index))
	active_ai_profile = profiles[clampi(requested_index, 0, profiles.size() - 1)]

func _switch_ai_profile(index: int) -> void:
	if not allow_runtime_ai_switch:
		return
	var profiles := _available_ai_profiles()
	if index < 0 or index >= profiles.size():
		return
	active_ai_profile = profiles[index]
	enemy_max_hp = int(active_ai_profile.max_hp)
	enemy_hp = enemy_max_hp
	enemy_shield = int(active_ai_profile.starting_shield)
	enemy_damage = int(active_ai_profile.base_damage)
	enemy_debuffs.clear()
	battle_ended = false
	_log("调试切换为「%s」，敌人数值与状态已重置。" % _enemy_name())
	_refresh_all()

func _enemy_name() -> String:
	if active_ai_profile != null:
		return String(active_ai_profile.display_name)
	return "Boss" if is_boss else "敌人"

func _refresh_ai_summary() -> void:
	if ai_summary_label == null:
		return
	if active_ai_profile == null:
		ai_summary_label.text = "使用 battle.tscn 根节点的兼容数值。"
		return
	ai_summary_label.text = "%s\n逻辑=%s｜攻击距离=%d-%d｜偏好距离=%d-%d｜移动=%d\n%s" % [
		active_ai_profile.display_name,
		active_ai_profile.behavior,
		active_ai_profile.attack_min_range,
		active_ai_profile.attack_max_range,
		active_ai_profile.preferred_min_range,
		active_ai_profile.preferred_max_range,
		active_ai_profile.move_amount,
		active_ai_profile.debug_description,
	]

func _add_emergency_card() -> void:
	if card_catalog.get_card(&"revolver") != null:
		board.add_entry(&"revolver", Vector2i.ZERO, 0)
