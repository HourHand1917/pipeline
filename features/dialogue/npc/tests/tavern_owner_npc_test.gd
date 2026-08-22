extends Node

const NPC_SCENE := "res://features/dialogue/npc/scenes/tavern_owner_npc.tscn"
const TIMELINE := "res://features/dialogue/npc/timelines/酒吧老板商店对话.dtl"
const OWNER := "res://features/dialogue/npc/characters/酒吧老板.dch"

var checks := 0

func _ready() -> void:
	call_deferred("_run")


func _run() -> void:
	var scene := load(NPC_SCENE) as PackedScene
	_check(scene != null, "drag-and-drop NPC scene loads")
	var npc := scene.instantiate()
	add_child(npc)
	await get_tree().process_frame
	await get_tree().process_frame

	_check(npc is FriendlyNPC, "NPC reuses the production FriendlyNPC dialogue stack")
	_check(npc.get_node_or_null("Sprite") is Sprite2D, "Sprite slot is ready for NPC art")
	_check(npc.get_node_or_null("DetectionRange") is CollisionShape2D, "range collider is configured")
	_check(npc.get_node_or_null("ClickZone/ClickShape") is CollisionShape2D, "click collider is configured")
	_check(npc.get_node_or_null("Blink") is BlinkComponent, "range blink/highlight is preconfigured")
	_check(str(npc.get("PersistenceId")) == "tavern_owner_dialogue",
		"drag-and-drop scene has a stable persistence id")
	_check(npc.get("DialogueTimeline") is DialogicTimeline, "default Timeline is loaded automatically")
	_check(npc.get("DialogueCharacter") == load(OWNER), "owner character is configured")
	_check(npc.get("PlayerDialogueCharacter") != null, "RUBBER is configured on the player side")
	var extra_characters: Array = npc.get("AdditionalPlayerDialogueCharacters")
	_check(extra_characters.size() == 1, "reb is configured on the player side")
	_check(bool(npc.get("PurchaseStateReady")), "purchase variables initialize without project edits")
	_check(Dialogic.VAR.get_variable("tavern_faucet_purchased", null, true) != null,
		"one-time faucet state exists")
	_check(Dialogic.VAR.get_variable("tavern_intel_purchased", null, true) != null,
		"paid intel state exists")

	var timeline := load(TIMELINE) as DialogicTimeline
	_check(timeline != null and timeline.events.size() >= 30, "full supplied dialogue parses")
	var source := FileAccess.get_file_as_string(TIMELINE)
	_check("label tavern_menu" in source, "repeatable purchase menu loops through a label")
	_check("tavern_purchase_drink" in source, "repeatable drink hook exists")
	_check("tavern_purchase_faucet" in source and "tavern_faucet_purchased" in source,
		"one-time faucet hook and condition exist")
	_check("tavern_purchase_intel" in source and "再次查看情报（免费）" in source,
		"paid-once then free intel choices exist")
	_check("暴躁的家伙" in source and "麻烦的家伙" not in source,
		"latest supplied tavern wording is preserved")
	_check("- 离开" in source and "[end_timeline]" in source, "leave choice ends the Timeline")

	var canvas: Node = (load("res://features/dialogue/scenes/npc_dialogue_canvas.tscn") as PackedScene).instantiate()
	add_child(canvas)
	await get_tree().process_frame
	var choices: Array[Node] = canvas.get_node("Root/ChoiceList").get_children()
	_check(choices.size() >= 4 and choices[0] is DialogicNode_ChoiceButton,
		"options reuse the existing full-bubble Dialogic choice buttons")

	canvas.queue_free()
	npc.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	print("TAVERN_OWNER_NPC_TEST_PASS checks=%d" % checks)
	get_tree().quit(0)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		push_error("TAVERN_OWNER_NPC_TEST_FAIL: " + message)
		get_tree().quit(1)
