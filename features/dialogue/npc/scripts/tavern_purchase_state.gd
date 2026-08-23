extends Node

const FAUCET_VARIABLE := "tavern_faucet_purchased"
const INTEL_VARIABLE := "tavern_intel_purchased"
const DRINK_RESULT_VARIABLE := "tavern_drink_purchase_result"


func _ready() -> void:
	# Dialogic's runtime variables are intentionally local to this self-contained NPC.
	# Add only missing keys so purchases survive repeated conversations in one game run.
	if not Dialogic.VAR.has(FAUCET_VARIABLE):
		Dialogic.VAR.var_storage[FAUCET_VARIABLE] = false
	if not Dialogic.VAR.has(INTEL_VARIABLE):
		Dialogic.VAR.var_storage[INTEL_VARIABLE] = false
	if not Dialogic.VAR.has(DRINK_RESULT_VARIABLE):
		Dialogic.VAR.var_storage[DRINK_RESULT_VARIABLE] = 0
