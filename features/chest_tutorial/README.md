# 宝箱三步引导

入口：`res://features/chest_tutorial/scenes/chest_tutorial.tscn`

直接拖到含宝箱和 `ExplorationHUD` 的探索场景根节点。默认自动寻找
`PersistenceId = chest_f1_1` 的宝箱，依次教学“点击宝箱”“获取物资”“关闭奖励页”。

- 第一步：聚光宝箱点击区，玩家点开宝箱后进入第二步。
- 第二步：聚光清单本体（标题 + 战利品列表），与绘制出来的 UI 对齐；
  返回键此时锁定。给 `DataManager`（全局背包/存档）拍快照，玩家领取任意
  物资（瓶盖/水龙头/卡牌/道具）使背包数值上涨即进入第三步。若玩家一口气
  把物资领完，清单自动合上，教程随之结束。
- 第三步：聚光清单的返回键，玩家点击返回结束教程。

Inspector 可配置宝箱 ID、标题、说明、遮罩透明度、框边距以及是否每个存档只显示一次。
聚光洞内始终是原宝箱、原清单槽位与原返回按钮，不替换任何交互逻辑。

自动验收：

`godot --headless --path <project> --scene res://features/chest_tutorial/tests/chest_tutorial_smoke.tscn`

测试会加载正式 `f1_1`，通过真实 Viewport 鼠标事件依次点击宝箱、战利品槽位与
返回按钮，验证三步提示、输入放行、背包检测和移动解锁。
