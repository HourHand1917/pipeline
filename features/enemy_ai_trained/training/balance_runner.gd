extends Node

const ROOT := "res://features/enemy_ai_trained/training/"


func _ready() -> void:
	call_deferred(&"_run")


func _run() -> void:
	var config := _json("locked_balance_profile.json")
	var strategies := _json("strategy_profiles.json")
	if config.is_empty() or strategies.is_empty():
		push_error("BALANCE_PROXY_FAIL missing config")
		get_tree().quit(1)
		return
	var rng := RandomNumberGenerator.new()
	rng.seed = int(config.seed)
	var iterations := int(config.iterations_per_matchup)
	var results := {}
	for tier in ["base", "upgraded"]:
		var wins := 0
		var total := 0
		for encounter_id in config.encounters:
			for strategy_id in strategies:
				var probability := clampf(float(config.encounters[encounter_id][tier]) + float(strategies[strategy_id]), 0.01, 0.99)
				for _game in range(iterations):
					wins += 1 if rng.randf() < probability else 0
					total += 1
		results[tier] = {"wins": wins, "total": total, "rate": float(wins) / float(total)}
	var base_rate := float(results.base.rate)
	var upgraded_rate := float(results.upgraded.rate)
	var target: Dictionary = config.get("targets", {}) as Dictionary
	var passed := base_rate >= float(target.base_enemy_win_min) and base_rate <= float(target.base_enemy_win_max) and upgraded_rate >= float(target.upgraded_enemy_win_min) and upgraded_rate <= float(target.upgraded_enemy_win_max)
	print("BALANCE_PROXY_%s base_enemy=%.4f upgraded_enemy=%.4f samples=%d" % ["PASS" if passed else "FAIL", base_rate, upgraded_rate, int(results.base.total) + int(results.upgraded.total)])
	get_tree().quit(0 if passed else 1)


func _json(file_name: String) -> Dictionary:
	var file := FileAccess.open(ROOT + file_name, FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	return parsed as Dictionary if parsed is Dictionary else {}
