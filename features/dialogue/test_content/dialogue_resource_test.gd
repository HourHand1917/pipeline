extends Node

const TIMELINE_PATH := "res://features/dialogue/test_content/timelines/水龙头与上层工程师.dtl"
const CHARACTER_PATHS := [
	"res://features/dialogue/test_content/characters/鼠鼠.dch",
	"res://features/dialogue/test_content/characters/reb.dch",
	"res://features/dialogue/test_content/characters/RUBBER.dch",
]


func _ready() -> void:
	var failures: Array[String] = []
	Engine.set_meta(&"dch_directory", {
		"鼠鼠": CHARACTER_PATHS[0],
		"reb": CHARACTER_PATHS[1],
		"RUBBER": CHARACTER_PATHS[2],
	})
	Engine.set_meta(&"dtl_directory", {
		"水龙头与上层工程师": TIMELINE_PATH,
	})

	for character_path: String in CHARACTER_PATHS:
		var character := load(character_path) as DialogicCharacter
		if character == null:
			failures.append("character failed to load: " + character_path)
		elif character.display_name.is_empty():
			failures.append("character has no display name: " + character_path)

	var timeline := load(TIMELINE_PATH) as DialogicTimeline
	if timeline == null:
		failures.append("timeline failed to load")
	else:
		timeline.process()
		var text_count := 0
		var choice_count := 0
		var wait_count := 0
		var end_count := 0
		for event: DialogicEvent in timeline.events:
			match event.event_name:
				"Text": text_count += 1
				"Choice": choice_count += 1
				"Wait": wait_count += 1
				"End": end_count += 1
		if text_count < 80:
			failures.append("expected at least 80 text events, got %d" % text_count)
		if choice_count != 1:
			failures.append("expected exactly one choice, got %d" % choice_count)
		if wait_count != 4:
			failures.append("expected four waits, got %d" % wait_count)
		if end_count != 1:
			failures.append("expected one explicit end, got %d" % end_count)

	var demo := load("res://features/dialogue/test_content/dialogue_content_demo.tscn") as PackedScene
	if demo == null:
		failures.append("direct-run demo scene failed to load")
	else:
		var instance := demo.instantiate()
		var npc := instance.get_node_or_null("NPC")
		if npc == null:
			failures.append("demo has no configured NPC")
		elif npc.get("Timeline") == null:
			failures.append("demo NPC has no dragged timeline")
		if instance.get_node_or_null("NPCDialogueCanvas") == null:
			failures.append("demo has no NPCDialogueCanvas")
		instance.free()
	Engine.remove_meta(&"dch_directory")
	Engine.remove_meta(&"dtl_directory")

	if failures.is_empty():
		print("DIALOGUE_CONTENT_RESOURCE_TEST_PASS")
		get_tree().quit(0)
	else:
		for failure: String in failures:
			push_error(failure)
		print("DIALOGUE_CONTENT_RESOURCE_TEST_FAIL count=", failures.size())
		get_tree().quit(1)
