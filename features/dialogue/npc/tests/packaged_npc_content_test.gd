extends Node

const CHARACTER_SETTING := "dialogic/directories/dch_directory"

const CASES := [
	{
		"section": 1,
		"scene": "res://features/dialogue/npc/scenes/shark_gang_sign_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/鲨牙帮告示牌.dtl",
		"type": "friendly",
		"first": "告示牌: 此处为鲨牙帮地盘！禁止私自拾荒！",
		"last": "reb: 进入状态。出发。",
		"minimum_events": 10,
		"characters": ["告示牌", "RUBBER", "reb"],
	},
	{
		"section": 2,
		"scene": "res://features/dialogue/npc/scenes/rocky_prebattle_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/Rocky战前.dtl",
		"type": "hostile",
		"first": "鼠鼠: 救救我！那群暴徒要杀了我！杀了我！",
		"last": "RUBBER: reb 你……算了，小老鼠，你先退后。看来要。",
		"minimum_events": 8,
		"characters": ["Rocky", "鼠鼠", "RUBBER", "reb"],
		"battle_scene": "res://Scenes/game_scene/rocky_battle_scene.tscn",
		"battle_rules": "res://features/enemy_ai_node/rules/rocky_boom_rules.tres",
		"return_spawn": "RockyReturnSpawn",
	},
	{
		"section": 3,
		"scene": "res://features/dialogue/npc/scenes/mousy_postbattle_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/鼠鼠战后.dtl",
		"type": "friendly",
		"first": "Rocky: 你们给我等着！鲨牙帮不会放过你们的——！",
		"last": "鼠鼠: 好的。谢谢，谢谢！",
		"minimum_events": 15,
		"characters": ["Rocky", "鼠鼠", "RUBBER", "reb"],
	},
	{
		"section": 4,
		"scene": "res://features/dialogue/npc/scenes/home_mousy_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/home鼠鼠.dtl",
		"type": "friendly",
		"first": "鼠鼠: 我检查完了，检查完了……是我以前参与设计过的型号。目前运行没什么问题，只是机械效率太低了。",
		"last": "_: （背景音乐响起。）",
		"minimum_events": 67,
		"characters": ["鼠鼠", "RUBBER", "reb"],
		"contains": ["对 reb 的改装你也得帮我参谋参谋", "战斗前，你可以自己组建卡组"],
	},
	{
		"section": 6,
		"scene": "res://features/dialogue/npc/scenes/sharkk_prebattle_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/Sharkk战前.dtl",
		"type": "hostile",
		"first": "Sharkk: 啊——这不是打败 Rocky 的那两位吗？我还在想，你们什么时候会游到我这来。",
		"last": "RUBBER: 哎……上擂台！",
		"minimum_events": 18,
		"characters": ["Sharkk", "RUBBER", "reb"],
		"contains": ["[shake]就留下你的命。连同你那只漂亮的手。[/shake]"],
		"battle_scene": "res://Scenes/game_scene/boom_battle_scene.tscn",
		"battle_rules": "res://features/enemy_ai_node/rules/sharkk_rules.tres",
		"return_spawn": "f2_4_sharkk_return",
	},
	{
		"section": 7,
		"scene": "res://features/dialogue/npc/scenes/post_sharkk_mousy_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/Sharkk战后鼠鼠.dtl",
		"type": "friendly",
		"first": "鼠鼠: 我回来了，回来了！",
		"last": "RUBBER: 那你的数据库该更新了。",
		"minimum_events": 24,
		"characters": ["鼠鼠", "RUBBER", "reb"],
	},
	{
		"section": 8,
		"scene": "res://features/dialogue/npc/scenes/upper_couple_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/上层富婆细狗.dtl",
		"type": "friendly",
		"first": "富婆: AUTORA 集团的病毒炸弹爆炸那么久了，墙外那群废物怎么还能进来？",
		"last": "RUBBER: 现在不是时候……我们先去赌场。",
		"minimum_events": 17,
		"characters": ["富婆", "细狗", "RUBBER", "reb"],
	},
	{
		"section": 9,
		"scene": "res://features/dialogue/npc/scenes/casino_dealer_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/无人赌场荷官.dtl",
		"type": "friendly",
		"first": "RUBBER: 这么大的赌场，真的一个人都没有……",
		"last": "reb: 收到。我们一起走吧。",
		"minimum_events": 26,
		"characters": ["荷官", "RUBBER", "reb"],
	},
	{
		"section": 10,
		"scene": "res://features/dialogue/npc/scenes/code00_prebattle_npc.tscn",
		"timeline": "res://features/dialogue/npc/timelines/Code_00战前.dtl",
		"type": "hostile",
		"first": "Code_00: 一个生物……一个老型号的AI。我的两只手，被你们弄坏了。",
		"last": "Code_00: [shake]嘿！[/shake]",
		"minimum_events": 15,
		"characters": ["Code_00", "RUBBER", "reb"],
		"battle_scene": "res://Scenes/game_scene/boom_battle_scene.tscn",
		"battle_rules": "res://features/enemy_ai_node/rules/core00_phase_two_rules.tres",
		"return_spawn": "boom_return",
	},
]

var checks := 0


func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	for spec: Dictionary in CASES:
		await _test_case(spec)

	print("PACKAGED_NPC_CONTENT_TEST_PASS checks=%d sections=%d" % [checks, CASES.size() + 1])
	call_deferred("_finish")


func _test_case(spec: Dictionary) -> void:
	var scene_path: String = spec.scene
	var scene := load(scene_path) as PackedScene
	_check(scene != null, "section %d scene loads: %s" % [spec.section, scene_path])
	if scene == null:
		return

	# Packaged NPC registers every carried character before loading its Timeline.
	var npc := scene.instantiate()
	add_child(npc)
	await get_tree().process_frame
	await get_tree().process_frame

	_check(npc is NPCBase, "%s reuses NPCBase" % scene_path)
	var visual := npc.get_node_or_null("Sprite")
	_check(visual is Sprite2D or visual is AnimatedSprite2D,
		"%s has a replaceable static or animated visual" % scene_path)
	var has_visual_asset := (visual is Sprite2D and (visual as Sprite2D).texture != null) \
		or (visual is AnimatedSprite2D and (visual as AnimatedSprite2D).sprite_frames != null)
	_check(has_visual_asset, "%s has a configured visible asset" % scene_path)
	_check(npc.get_node_or_null("DetectionRange") is CollisionShape2D,
		"%s has DetectionRange" % scene_path)
	_check(npc.get_node_or_null("ClickZone/ClickShape") is CollisionShape2D,
		"%s has ClickZone/ClickShape" % scene_path)
	_check(npc.get_node_or_null("Blink") is BlinkComponent, "%s has Blink" % scene_path)
	_check(npc.get_node_or_null("AnimationPlayer") is AnimationPlayer,
		"%s reserves AnimationPlayer" % scene_path)
	_check(not str(npc.get("PersistenceId")).is_empty(), "%s has PersistenceId" % scene_path)
	_check(npc.get("DialogueTimeline") is DialogicTimeline,
		"%s auto-loads its configured Timeline" % scene_path)

	var timeline_path: String = spec.timeline
	var source := FileAccess.get_file_as_string(timeline_path)
	_check(not source.is_empty(), "%s source is readable" % timeline_path)
	_check(source.begins_with(spec.first), "%s keeps the supplied first line" % timeline_path)
	_check(source.strip_edges().ends_with(spec.last), "%s keeps the supplied final line" % timeline_path)
	for marker: String in spec.get("contains", []):
		_check(marker in source, "%s keeps required line: %s" % [timeline_path, marker])

	var timeline := npc.get("DialogueTimeline") as DialogicTimeline
	if timeline != null:
		_check(timeline.events.size() >= int(spec.minimum_events),
			"%s keeps the complete supplied section" % timeline_path)

	var registered_characters: Dictionary = ProjectSettings.get_setting(CHARACTER_SETTING, {})
	for identifier: String in spec.characters:
		_check(registered_characters.has(identifier),
			"%s registers character '%s' in memory" % [scene_path, identifier])

	var player_character := npc.get("PlayerDialogueCharacter") as Resource
	_check(player_character != null and player_character.resource_path.ends_with("/RUBBER.dch"),
		"%s routes RUBBER to the player anchor" % scene_path)
	var player_extras: Array = npc.get("AdditionalPlayerDialogueCharacters")
	_check(player_extras.any(func(character: Resource) -> bool:
		return character != null and character.resource_path.ends_with("/reb.dch")),
		"%s routes reb to the player anchor" % scene_path)

	if spec.type == "hostile":
		_check(npc is HostileNPC, "%s is a HostileNPC" % scene_path)
		_check(str(npc.get("BattleScenePath")) == str(spec.battle_scene),
			"%s has the intended battle scene" % scene_path)
		_check(str(npc.get("BattleRulesPath")) == str(spec.battle_rules),
			"%s has the intended battle rules" % scene_path)
		_check(str(npc.get("ReturnSpawnId")) == str(spec.return_spawn),
			"%s has the intended return spawn" % scene_path)
	else:
		_check(npc is FriendlyNPC, "%s is a FriendlyNPC" % scene_path)

	npc.queue_free()
	await get_tree().process_frame


func _finish() -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	get_tree().quit(0)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		push_error("PACKAGED_NPC_CONTENT_TEST_FAIL: " + message)
		get_tree().quit(1)
