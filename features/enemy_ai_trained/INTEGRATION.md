# 拖拽接入

- Boom：`enemy_data/boom_trained.tres`，8 HP，地图上限8。
- Rocky：`enemy_data/rocky_trained.tres`，20 HP，地图上限9。
- Sharkk：`enemy_data/sharkk_trained.tres`，40 HP，地图上限9。
- Core-00 二阶段：`enemy_data/core00_body_trained.tres`，50 HP，地图上限13。

场景根节点导出 `actions`、`board_min_cell`、`board_max_cell`、`blocked_cells` 与权重参数，拖入后可以在 Inspector 微调。Core-00 一阶段继续使用原流程。
