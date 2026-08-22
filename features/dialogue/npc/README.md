# 剧情 NPC 拖拽包

本目录把 `pipeline对话 (2).txt` 的 10 段内容全部封装为可直接拖入探索场景的 NPC。
每个场景都已经带好：

- `Sprite` 图片槽（默认是项目图标占位图）。
- `DetectionRange` 互动/警戒范围。
- `ClickZone/ClickShape` 鼠标点击范围。
- `Blink` 范围闪烁与悬停高亮。
- `AnimationPlayer` 对话演出预留。
- Dialogic Timeline、角色注册、RUBBER/reb 双锚点气泡。
- 唯一 `PersistenceId`。

不需要修改 Dialogic 插件，也不需要手动摆 Canvas。`NPCBase` 会自动创建或复用项目现有
微信式气泡 Canvas；对白与选项都使用同一套完整气泡。

## 最快使用方法

1. 从下表把需要的 `.tscn` 拖进探索场景。
2. 选中它的 `Sprite` 子节点，把正式图片拖到 `Texture`。
3. 把 NPC 移到目标位置；根据美术大小调整 `Sprite` 的 Position/Scale。
4. 如有需要，调整 `DetectionRange`、`ClickZone/ClickShape` 和根节点两个 Bubble Offset。

做到这里就能使用。友好 NPC 在玩家进入范围后用鼠标左键点击；敌对 NPC 进入
`AggroRadius` 后自动播放战前对话，正常结束后进入已经配置好的战斗。

## 10 段内容与场景

| 段落 | 可拖拽场景 | 行为 |
| --- | --- | --- |
| 1 鲨牙帮告示牌 | `scenes/shark_gang_sign_npc.tscn` | 点击告示牌对话 |
| 2 Rocky 战前 | `scenes/rocky_prebattle_npc.tscn` | 自动对话，结束进入 Rocky + Boom 战斗 |
| 3 Rocky 战后鼠鼠 | `scenes/mousy_postbattle_npc.tscn` | 点击鼠鼠对话 |
| 4 Home 初次安置鼠鼠 | `scenes/home_mousy_npc.tscn` | 点击鼠鼠长对话，包含改装与组卡说明 |
| 5 酒吧老板商店 | `scenes/tavern_owner_npc.tscn` | 点击老板，显示四类气泡选项 |
| 6 Sharkk 战前 | `scenes/sharkk_prebattle_npc.tscn` | 自动对话，结束进入 Sharkk 战斗 |
| 7 Sharkk 战后鼠鼠 | `scenes/post_sharkk_mousy_npc.tscn` | 点击鼠鼠，说明商店、行商与上层管道 |
| 8 富婆与细狗 | `scenes/upper_couple_npc.tscn` | 一张双人图即可；两名 NPC 共用 NPC 侧锚点 |
| 9 无人赌场荷官 | `scenes/casino_dealer_npc.tscn` | 点击荷官对话 |
| 10 Code_00 | `scenes/code00_prebattle_npc.tscn` | 自动对话，结束进入 Core-00 二阶段本体战 |

第 10 段开场明确说明“两只手已经被弄坏”，因此默认接
`core00_phase_two_rules.tres`。若策划要把它放到另一阶段，只需在 Inspector 修改
`BattleRulesPath`，不必改 Timeline 或脚本。

Rocky 与 Sharkk 的战斗路径、规则和返回点已按正式地图配置。把敌对封装放进正式地图时，
应当**替换**地图里原来的同名敌人，不要并排保留两个实例，否则会重复触发战斗。

## 多阶段摆放注意

- 第 2/3 段是 Rocky 战前与战后两个状态。
- 第 4/7 段是 Home 鼠鼠在不同剧情阶段的两个状态。

它们作为独立资源提供，方便策划按剧情状态替换。不要在同一位置同时摆出两个阶段的 NPC。
本包不擅自修改全局剧情进度系统。

## 气泡与操作

- RUBBER 与 reb 自动显示在玩家锚点一侧。
- 当前互动 NPC（以及富婆/细狗组合）显示在 NPC 锚点一侧。
- 新消息会把全部历史消息向上顶，不会向下回落。
- 选项是可点击的完整气泡，显示在玩家锚点附近。
- 对话期间玩家移动被锁定，鼠标只能操作对话。
- 按 `Esc` 会真正取消对话并恢复输入；取消不会开商店或进入战斗。
- 敌对 NPC 取消后，必须先离开警戒范围再进入，才会重新触发。

NPC 场景会在当前运行实例中注册自己携带的 Dialogic Character，然后才加载 Timeline。
它不会保存 ProjectSettings，所以不会改写 `project.godot`。所有角色名大小写已统一为
`RUBBER`、`reb`、`鼠鼠`、`Rocky`、`Sharkk`、`Code_00` 等正式标识。

## 对话动画

每个场景都预留了 `AnimationPlayer`，并已拖到根节点的 `DialogueAnimationPlayer`。
在 Timeline 中添加 Dialogic `Signal`：

- 字符串：`animation:动画名`
- 或字典 JSON：`{"animation":"动画名"}`

就能播放该 AnimationPlayer 中同名动画。一个 AnimationPlayer 的轨道可以同时控制 NPC、
玩家、道具或场景节点。

## 酒吧老板购买接口

酒吧选项已经完整实现：

- “硬骨头”酒可重复选择。
- 水龙头只能选择一次，之后隐藏。
- 情报第一次购买，之后变成“再次查看情报（免费）”。
- 离开正常结束 Timeline。

`TavernOwnerNPC` 把 Dialogic Signal 转发为
`TavernPurchaseRequested(purchaseId, repeatable)`：

- `hard_bone_drink`，可重复。
- `faucet`，不可重复。
- `upper_route_intel`，不可重复。

策划没有提供价格，因此封装不擅自扣瓶盖或发货；正式经济系统连接这个信号即可。
对话、条件状态和气泡选项可以独立运行。

## 测试

- `tests/packaged_npc_content_test.tscn`：检查除酒吧外的 9 段 Timeline、角色注册、
  Sprite/碰撞/Blink/AnimationPlayer/PersistenceId，以及三场战斗配置。
- `tests/tavern_owner_npc_test.tscn`：检查第 5 段四类购买分支、最新文本、Blink 和气泡选项。
- `../tests/npc_dialogue_runtime_test.tscn`：检查 Canvas、双锚点和完整气泡选项。
- `../tests/npc_dialogue_history_escape_test.tscn`：检查历史气泡姓名快照、ESC 真取消、
  取消后不开店/不开战。

可单独运行的酒吧示例仍在：`demo/tavern_owner_npc_demo.tscn`。
