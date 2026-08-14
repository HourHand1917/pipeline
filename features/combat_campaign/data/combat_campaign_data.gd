extends Resource
class_name CombatCampaignData

@export_group("Identity")
@export var id: StringName = &"pipeline_combat_campaign"
@export var display_name: String = "Pipeline Combat Campaign"

@export_group("Sequence")
@export_range(1, 99, 1, "or_greater") var battle_count: int = 4
@export var waves: Array[CombatCampaignWaveData] = []


func is_configuration_valid() -> bool:
	if id.is_empty() or battle_count <= 0 or waves.is_empty():
		return false
	var previous_battle := 0
	var previous_wave := 0
	var seen_battles: Dictionary = {}
	for wave in waves:
		if wave == null or not wave.is_configuration_valid():
			return false
		if wave.battle_number < previous_battle:
			return false
		if wave.battle_number == previous_battle:
			if wave.wave_number != previous_wave + 1:
				return false
		else:
			if wave.battle_number != previous_battle + 1 or wave.wave_number != 1:
				return false
		previous_battle = wave.battle_number
		previous_wave = wave.wave_number
		seen_battles[wave.battle_number] = true
	return seen_battles.size() == battle_count


func wave_at(index: int) -> CombatCampaignWaveData:
	if index < 0 or index >= waves.size():
		return null
	return waves[index]

