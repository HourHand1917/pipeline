extends RefCounted
class_name PipelineCardCatalogLoader

const DEFAULT_CATALOG_PATH := "res://card/catalog/all_cards.tres"


static func load_default() -> PipelineCardCatalog:
	return load(DEFAULT_CATALOG_PATH) as PipelineCardCatalog


static func load_all_cards() -> Array[CardData]:
	var catalog := load_default()
	if catalog == null:
		return []
	return catalog.cards.duplicate()


static func get_card(card_id: StringName) -> CardData:
	var catalog := load_default()
	if catalog == null:
		return null
	return catalog.get_card(card_id)
