# 宝箱两步引导

入口：`res://features/chest_tutorial/scenes/chest_tutorial.tscn`

直接拖到含宝箱和 `ExplorationHUD` 的探索场景根节点。默认自动寻找
`PersistenceId = chest_f1_1` 的宝箱，依次教学“点击宝箱”和“关闭奖励页”。

Inspector 可配置宝箱 ID、标题、说明、遮罩透明度、框边距以及是否每个存档只显示一次。
聚光洞内始终是原宝箱与原关闭按钮，不替换任何交互逻辑。

自动验收：

`godot --headless --path <project> --scene res://features/chest_tutorial/tests/chest_tutorial_smoke.tscn`

测试会加载正式 `f1_1`，通过真实 Viewport 鼠标事件点击宝箱与关闭按钮，验证两步提示、输入放行和移动解锁。
