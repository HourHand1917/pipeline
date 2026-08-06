class_name ShopData
extends Resource

## 商店名称
@export var shop_name: String = "商店"

## 商品列表 — Inspector 里点 Add Element → New ShopEntry 内联创建
@export var entries: Array[ShopEntry] = []
