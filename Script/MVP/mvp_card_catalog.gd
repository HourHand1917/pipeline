class_name MvpCardCatalog
extends Resource

@export var cards: Array[Resource] = []

func get_card(card_id: StringName) -> Resource:
	for card in cards:
		if card != null and card.card_id == card_id:
			return card
	return null

func ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for card in cards:
		if card != null:
			result.append(card.card_id)
	return result
