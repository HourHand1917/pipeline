extends Node

## Core-00 playable card battle endurance test.
##
## Run with:
##   godot --headless --path <project> \
##     res://features/core00_ai/card_battle/core00_battle_soak.tscn -- \
##     --soak-seconds=20 --soak-seed=20260813
##
## The harness uses the same public actions as the playable scene: charge cards,
## move one cell, use cards and end the player turn.  A large test-only shield
## keeps a session alive long enough to exercise all fourteen cards and both
## Core-00 phases repeatedly.

const BATTLE_SCRIPT := preload("res://features/core00_ai/card_battle/core00_card_battle.gd")

const DEFAULT_SOAK_SECONDS := 30
const DEFAULT_SOAK_SEED := 20260813
const STEPS_PER_FRAME := 12
const MAX_TURNS_PER_GAME := 180
const TEST_SHIELD := 3000
const EXPECTED_CARD_COUNT := 14

const TARGET_TRUE := &"true_hand"
const TARGET_FALSE := &"false_hand"
const TARGET_BODY := &"body"

var battle: Core00CardBattle
var rng := RandomNumberGenerator.new()

var soak_seconds := DEFAULT_SOAK_SECONDS
var soak_seed := DEFAULT_SOAK_SEED
var started_msec := 0
var deadline_msec := 0
var next_progress_msec := 0
var progress_interval_msec := 5000

var total_player_turns := 0
var games_started := 0
var games_completed := 0
var victories := 0
var defeats := 0
var timeouts := 0
var phase_one_visits := 0
var phase_two_visits := 0
var jam_events_seen := 0
var invariant_checks := 0
var operation_checks := 0
var failure_messages: PackedStringArray = []
var failure_details: PackedStringArray = []
var coverage: Dictionary = {}
var action_coverage: Dictionary = {}

var game_turns := 0
var game_card_order: Array[StringName] = []
var game_card_cursor := 0
var focus_card_id: StringName = &""
var last_phase := -1
var _finishing := false


func _ready() -> void:
	_parse_arguments()
	Engine.max_fps = 60
	rng.seed = soak_seed
	started_msec = Time.get_ticks_msec()
	deadline_msec = started_msec + soak_seconds * 1000
	progress_interval_msec = clampi(int(soak_seconds * 100), 1000, 30000)
	next_progress_msec = started_msec + progress_interval_msec

	battle = BATTLE_SCRIPT.new() as Core00CardBattle
	battle.name = "SoakBattle"
	battle.build_visual_ui = false
	add_child(battle)
	battle.boss.card_jam_requested.connect(_on_card_jam_requested)

	for card in battle.cards:
		coverage[card.id] = 0
	_check(battle.cards.size() == EXPECTED_CARD_COUNT,
		"卡牌目录应有%d张，实际%d张" % [EXPECTED_CARD_COUNT, battle.cards.size()])
	_start_new_game()
	print("CORE00压力测试启动：时长=%d秒，种子=%d，卡牌=%d张" % [
		soak_seconds, soak_seed, battle.cards.size(),
	])


func _process(_delta: float) -> void:
	if _finishing:
		return
	var now := Time.get_ticks_msec()
	if now >= deadline_msec:
		_finish_soak()
		return
	for _step in range(STEPS_PER_FRAME):
		_run_one_player_turn()
		if _finishing or Time.get_ticks_msec() >= deadline_msec:
			break
	if now >= next_progress_msec:
		_print_progress(now)
		next_progress_msec = now + progress_interval_msec


func _parse_arguments() -> void:
	var arguments := OS.get_cmdline_user_args()
	if arguments.is_empty():
		arguments = OS.get_cmdline_args()
	for argument in arguments:
		var value := String(argument)
		if value.begins_with("--soak-seconds="):
			soak_seconds = maxi(1, int(value.get_slice("=", 1)))
		elif value.begins_with("--soak-seed="):
			soak_seed = int(value.get_slice("=", 1))


func _start_new_game() -> void:
	battle.starting_player_position = rng.randi_range(4, 8)
	battle.reset_battle()
	battle.player_shield = TEST_SHIELD
	games_started += 1
	game_turns = 0
	game_card_cursor = 0
	focus_card_id = &""
	last_phase = -1
	game_card_order.clear()
	for card in battle.cards:
		game_card_order.append(card.id)
	_shuffle_card_order(game_card_order)
	_observe_phase()
	_check_invariants("新对局")


func _shuffle_card_order(order: Array[StringName]) -> void:
	for index in range(order.size() - 1, 0, -1):
		var swap_index := rng.randi_range(0, index)
		var temporary := order[index]
		order[index] = order[swap_index]
		order[swap_index] = temporary


func _run_one_player_turn() -> void:
	if battle.battle_over:
		_close_game()
		return
	game_turns += 1
	total_player_turns += 1
	_observe_phase()
	_check_invariants("玩家回合开始")

	# One turn may use several fully charged cards when batteries provide extra
	# light points.  The operation cap prevents a malformed card from looping.
	var body_damage_card_used := false
	for _operation in range(8):
		if battle.battle_over:
			break
		if focus_card_id.is_empty():
			focus_card_id = _choose_focus_card()
		if focus_card_id.is_empty():
			break
		var card := battle.get_card_data(focus_card_id)
		var runtime := battle.get_card_state(focus_card_id)
		if card == null or runtime.is_empty():
			_fail("无法读取聚焦卡牌：%s" % String(focus_card_id))
			focus_card_id = &""
			break
		if int(runtime.get("cooldown_remaining", 0)) > 0:
			focus_card_id = &""
			continue

		var target_id := _choose_target()
		if card.has_damage_effect() and not target_id.is_empty():
			_prepare_range(card, target_id)

		var capacity := maxi(1, card.shape_offsets.size())
		var missing := capacity - int(runtime.get("charge", 0))
		if missing > 0 and battle.light_points > 0:
			var charged := battle.charge_card(card.id, mini(missing, battle.light_points))
			operation_checks += 1
			_check(bool(charged.get("ok", false)),
				"%s 点亮失败：%s" % [card.display_name, String(charged.get("reason", ""))])

		runtime = battle.get_card_state(card.id)
		if int(runtime.get("charge", 0)) >= capacity:
			if not card.has_damage_effect() or _card_is_in_range(card, target_id):
				var used := battle.use_card(card.id, target_id)
				operation_checks += 1
				_check(bool(used.get("ok", false)),
					"%s 使用失败：%s" % [card.display_name, String(used.get("reason", ""))])
				if bool(used.get("ok", false)):
					coverage[card.id] = int(coverage.get(card.id, 0)) + 1
					if battle.boss.phase == Core00EnermyMannager.Phase.BODY and card.has_damage_effect():
						body_damage_card_used = true
					focus_card_id = &""
					if game_card_cursor < game_card_order.size() and game_card_order[game_card_cursor] == card.id:
						game_card_cursor += 1
					_check_invariants("使用卡牌后")
					if body_damage_card_used:
						# Core-00 reacts to 8+ damage received in one player turn.
						# Stop after one body hit so the simulator also validates the
						# intended counter-play instead of feeding infinite teleports.
						break
					continue
		# No light or not in range: enemy gets its turn.
		break

	if not battle.battle_over:
		var end_result := battle.end_player_turn()
		operation_checks += 1
		_check(bool(end_result.get("ok", false)), "结束玩家回合失败")
		var enemy_result := end_result.get("enemy_result", {}) as Dictionary
		var action_id := StringName(enemy_result.get("id", &"unknown"))
		action_coverage[action_id] = int(action_coverage.get(action_id, 0)) + 1
		_observe_phase()
		_check_invariants("敌人行动后")

	if game_turns >= MAX_TURNS_PER_GAME and not battle.battle_over:
		timeouts += 1
		_fail("第%d局超过%d回合仍未结束" % [games_started, MAX_TURNS_PER_GAME])
		_start_new_game()
	elif battle.battle_over:
		_close_game()


func _choose_focus_card() -> StringName:
	# First use every card once in the current game.  Cooling cards are skipped
	# temporarily and revisited after the cursor has wrapped.
	if game_card_cursor < game_card_order.size():
		for offset in range(game_card_order.size()):
			var index := (game_card_cursor + offset) % game_card_order.size()
			var card_id := game_card_order[index]
			var state := battle.get_card_state(card_id)
			if int(state.get("cooldown_remaining", 0)) <= 0:
				if index != game_card_cursor:
					var temporary := game_card_order[game_card_cursor]
					game_card_order[game_card_cursor] = card_id
					game_card_order[index] = temporary
				return card_id

	# Once all fourteen have been used, pick the strongest currently usable
	# damage card.  Range preparation happens separately through normal movement.
	var best_id: StringName = &""
	var best_score := -1.0
	var reaction_budget := 999
	if battle.boss.phase == Core00EnermyMannager.Phase.BODY:
		reaction_budget = maxi(0, 7 - battle.boss.damage_received_this_player_turn)
	for card in battle.cards:
		if not card.has_damage_effect():
			continue
		var state := battle.get_card_state(card.id)
		if int(state.get("cooldown_remaining", 0)) > 0:
			continue
		var card_damage := _card_total_damage(card)
		if card_damage > reaction_budget:
			continue
		var score := float(card_damage) / float(maxi(1, card.shape_offsets.size()))
		# Small seeded jitter exercises alternatives without overwhelming power.
		score += rng.randf_range(0.0, 0.25)
		if score > best_score:
			best_score = score
			best_id = card.id
	if not best_id.is_empty():
		return best_id
	# While all safe attacks cool down, continue exercising a ready utility
	# card instead of submitting an invalid attack.
	for card in battle.cards:
		if card.has_damage_effect():
			continue
		var state := battle.get_card_state(card.id)
		if int(state.get("cooldown_remaining", 0)) <= 0:
			return card.id
	return &""


func _card_total_damage(card: CardData) -> int:
	var total := 0
	for effect in card.effects:
		if effect != null and effect.type == CombatEffectData.Type.DAMAGE:
			total += maxi(0, effect.amount)
	if card.effects.is_empty() and card.effect_type == "damage":
		total += maxi(0, card.effect_value)
	return total


func _choose_target() -> StringName:
	var targets := battle.get_active_target_ids()
	if targets.is_empty():
		return &""
	if battle.boss.phase == Core00EnermyMannager.Phase.HANDS \
		and battle.boss.true_death_loop_active \
		and targets.has(TARGET_FALSE):
		return TARGET_FALSE
	var best := targets[0]
	var best_distance := battle.get_target_distance(best)
	for target_id in targets:
		var distance := battle.get_target_distance(target_id)
		if distance < best_distance or (distance == best_distance and rng.randi_range(0, 1) == 1):
			best = target_id
			best_distance = distance
	return best


func _prepare_range(card: CardData, target_id: StringName) -> void:
	if target_id.is_empty() or not battle.free_move_available:
		return
	var distance := battle.get_target_distance(target_id)
	if distance >= card.min_range and distance <= card.max_range:
		return
	var actor := _target_actor(target_id)
	if actor.is_empty():
		return
	var target_position := int(actor.get("position", battle.player_position))
	var direction := signi(target_position - battle.player_position)
	if distance < card.min_range:
		direction *= -1
	if direction == 0:
		direction = 1 if battle.player_position <= 6 else -1
	var moved := battle.move_player_adjacent(direction)
	operation_checks += 1
	if not bool(moved.get("ok", false)):
		# A body can pin one side of the board.  Try the opposite legal direction;
		# this is still the same one-cell public movement action.
		moved = battle.move_player_adjacent(-direction)
		operation_checks += 1
	_check_invariants("移动后")


func _card_is_in_range(card: CardData, target_id: StringName) -> bool:
	if target_id.is_empty():
		return false
	var distance := battle.get_target_distance(target_id)
	return distance >= card.min_range and distance <= card.max_range


func _target_actor(target_id: StringName) -> Dictionary:
	match target_id:
		TARGET_TRUE:
			return battle.boss.true_hand
		TARGET_FALSE:
			return battle.boss.false_hand
		TARGET_BODY:
			return battle.boss.body
	return {}


func _observe_phase() -> void:
	var phase := int(battle.boss.phase)
	if phase == last_phase:
		return
	last_phase = phase
	if phase == Core00EnermyMannager.Phase.HANDS:
		phase_one_visits += 1
	elif phase == Core00EnermyMannager.Phase.BODY:
		phase_two_visits += 1


func _close_game() -> void:
	games_completed += 1
	if battle.battle_result == &"victory":
		victories += 1
	elif battle.battle_result == &"defeat":
		defeats += 1
	else:
		_fail("对局结束但结果无效：%s" % String(battle.battle_result))
	_start_new_game()


func _check_invariants(context: String) -> void:
	if battle == null or battle.boss == null:
		_fail("%s：战斗或Boss实例为空" % context)
		return
	invariant_checks += 1
	_check(battle.player_hp >= 0 and battle.player_hp <= battle.PLAYER_MAX_HP,
		"%s：玩家生命越界 %d" % [context, battle.player_hp])
	_check(battle.player_shield >= 0 and battle.player_shield <= TEST_SHIELD + 1000,
		"%s：玩家护盾越界 %d" % [context, battle.player_shield])
	_check(battle.player_position >= 1 and battle.player_position <= battle.BOARD_CELLS,
		"%s：玩家位置越界 %d" % [context, battle.player_position])
	_check(battle.light_points >= 0 and battle.light_points <= 20,
		"%s：亮格点越界 %d" % [context, battle.light_points])
	_check(battle.player_turn >= 1, "%s：玩家回合数非法" % context)
	_check(battle.card_runtime.size() == EXPECTED_CARD_COUNT,
		"%s：运行时卡牌数量不是%d" % [context, EXPECTED_CARD_COUNT])

	for card in battle.cards:
		var state := battle.get_card_state(card.id)
		var capacity := maxi(1, card.shape_offsets.size())
		var charge := int(state.get("charge", -1))
		var cooldown := int(state.get("cooldown_remaining", -1))
		_check(charge >= 0 and charge <= capacity,
			"%s：《%s》充能越界 %d/%d" % [context, card.display_name, charge, capacity])
		_check(cooldown >= 0 and cooldown <= maxi(99, card.cooldown_turns),
			"%s：《%s》冷却越界 %d" % [context, card.display_name, cooldown])

	var phase := int(battle.boss.phase)
	_check(phase >= Core00EnermyMannager.Phase.HANDS and phase <= Core00EnermyMannager.Phase.DEFEATED,
		"%s：Boss阶段非法 %d" % [context, phase])
	_check_actor(battle.boss.true_hand, 21, "True手", context)
	_check_actor(battle.boss.false_hand, 21, "False手", context)
	_check_actor(battle.boss.body, 50, "本体", context)
	_check(battle.boss.player_hp == battle.player_hp,
		"%s：玩家生命未与Boss管理器同步 %d/%d" % [context, battle.player_hp, battle.boss.player_hp])
	if phase == Core00EnermyMannager.Phase.HANDS:
		_check(not bool(battle.boss.body.get("active", false)), "%s：第一阶段本体不应激活" % context)
	elif phase == Core00EnermyMannager.Phase.BODY:
		_check(not bool(battle.boss.true_hand.get("active", true)), "%s：第二阶段True手仍激活" % context)
		_check(not bool(battle.boss.false_hand.get("active", true)), "%s：第二阶段False手仍激活" % context)
		_check(bool(battle.boss.body.get("active", false)), "%s：第二阶段本体未激活" % context)
		_check(int(battle.boss.body.get("position", -1)) != battle.player_position,
			"%s：本体与玩家重叠在%d格" % [context, battle.player_position])
	elif phase == Core00EnermyMannager.Phase.DEFEATED:
		_check(not bool(battle.boss.body.get("active", true)), "%s：击败后本体仍激活" % context)


func _check_actor(actor: Dictionary, expected_max_hp: int, label: String, context: String) -> void:
	_check(not actor.is_empty(), "%s：%s数据为空" % [context, label])
	if actor.is_empty():
		return
	var hp := int(actor.get("hp", -1))
	var max_hp := int(actor.get("max_hp", -1))
	var shield := int(actor.get("shield", -1))
	var position := int(actor.get("position", -1))
	_check(max_hp == expected_max_hp, "%s：%s最大生命错误 %d" % [context, label, max_hp])
	_check(hp >= 0 and hp <= max_hp, "%s：%s生命越界 %d/%d" % [context, label, hp, max_hp])
	_check(shield >= 0 and shield <= 10000, "%s：%s护盾越界 %d" % [context, label, shield])
	_check(position >= 1 and position <= battle.BOARD_CELLS,
		"%s：%s位置越界 %d" % [context, label, position])


func _on_card_jam_requested(_turns: int) -> void:
	jam_events_seen += 1


func _print_progress(now_msec: int) -> void:
	var elapsed := float(now_msec - started_msec) / 1000.0
	var covered := 0
	for card_id in coverage:
		if int(coverage[card_id]) > 0:
			covered += 1
	print("CORE00压力测试进度：%.1f/%d秒，对局完成=%d（胜%d/负%d），玩家回合=%d，卡牌覆盖=%d/%d，阶段二=%d，失败=%d" % [
		elapsed, soak_seconds, games_completed, victories, defeats, total_player_turns,
		covered, EXPECTED_CARD_COUNT, phase_two_visits, failure_messages.size(),
	])


func _finish_soak() -> void:
	_finishing = true
	var elapsed := float(Time.get_ticks_msec() - started_msec) / 1000.0
	_check(games_completed >= 2, "至少应完成2局，实际%d局" % games_completed)
	_check(phase_one_visits > 0, "未覆盖Core-00第一阶段")
	_check(phase_two_visits > 0, "未覆盖Core-00第二阶段")
	_check(jam_events_seen > 0, "未覆盖Core-00卡牌干扰意图")
	for card in battle.cards:
		_check(int(coverage.get(card.id, 0)) > 0,
			"未实际使用卡牌《%s》（%s）" % [card.display_name, String(card.id)])

	var status := "PASS" if failure_messages.is_empty() else "FAIL"
	print("CORE00压力测试最终摘要：%s；实际%.2f秒；种子=%d；开始%d局；完成%d局；胜%d/负%d/超时%d；回合%d；不变量批次%d；操作检查%d；卡牌覆盖=%s；敌人意图覆盖=%s；干扰=%d；失败=%d" % [
		status, elapsed, soak_seed, games_started, games_completed, victories, defeats,
		timeouts, total_player_turns, invariant_checks, operation_checks,
		str(coverage), str(action_coverage), jam_events_seen, failure_messages.size(),
	])
	if not failure_messages.is_empty():
		for message in failure_messages:
			printerr("[压力测试失败] %s" % message)
	get_tree().quit(0 if failure_messages.is_empty() else 1)


func _check(condition: bool, message: String) -> void:
	if not condition:
		_fail(message)


func _fail(message: String) -> void:
	# Keep the first occurrence of each failure so a long soak remains readable.
	if not failure_messages.has(message):
		failure_messages.append(message)
		var detail := "%s | game=%d turn=%d | %s" % [
			message,
			games_started,
			game_turns,
			str(battle.get_battle_snapshot()) if battle != null else "battle=null",
		]
		failure_details.append(detail)
		printerr("[压力测试失败] %s" % detail)
