class_name ShopEntry
extends Resource

## 要卖的道具 (ItemData) 或卡牌 (CardData) 资源
@export var item_res: Resource

@export var price: int = 10


## 从物品资源自动读取显示名
func get_display_name() -> String:
	if item_res == null:
		return "空商品"
	var name_str = item_res.get("display_name")
	if name_str == null or str(name_str).is_empty():
		return "未命名"
	return str(name_str)


## 根据资源脚本自动判断：CardData → true，ItemData → false
func is_card() -> bool:
	if item_res == null:
		return false
	var scr = item_res.get_script()
	if scr == null:
		return false
	return "card_data" in scr.resource_path
