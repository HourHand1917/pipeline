# NPC Dialogic 对话配置指南

## 1. 可直接运行的示例

打开并运行：

`res://features/dialogue/examples/npc_dialogue_configuration_example.tscn`

示例包含：

- FriendlyNPC：点击后开始对话，结束后打开商店。
- HostileNPC：进入警戒范围后开始对话，结束后进入 Boom 战斗。
- 玩家/NPC 双锚点微信式气泡。
- 完整气泡选项和鼠标点击。
- Dialogic Signal 调用 AnimationPlayer。
- 显式放置的 `NPCDialogueCanvas`，可以直接调参。

操作：

1. 对话前可用 A/D 站到鼠鼠左边或右边，再用鼠标左键点击鼠鼠。
2. 对话开始后玩家立即停止且不能移动，非对话区域的鼠标点击会被拦截。
3. 对话使用空格、Enter 或左键继续；点击整个选项气泡进入 Dialogic 分支。
4. 观察 RUBBER 与 reb：玩家在 NPC 左侧时尾巴在左，走到右侧再开始时尾巴翻到右侧。
5. 友好 NPC 对话结束后打开商店；关闭后可继续用 A/D 走到 Boom。
6. Boom 进入范围后自动对话，对话结束后进入战斗。

示例 Timeline：

`res://features/dialogue/examples/npc_dialogue_example_timeline.dtl`

自动验收场景：

`res://features/dialogue/examples/npc_dialogue_modal_tail_test.tscn`

该测试会验证 RUBBER/reb 路由、左右尾巴翻转、移动锁、全屏鼠标拦截以及对话结束后的完整恢复。

## 2. NPC 必须的节点结构

FriendlyNPC 和 HostileNPC 都继承了 `InteractableBase`。节点名和类型必须与下面一致：

```text
YourNPC (Area2D，挂 FriendlyNPC.cs 或 HostileNPC.cs)
├─ Sprite (Sprite2D)
├─ DetectionRange (CollisionShape2D)
└─ ClickZone (Area2D)
   └─ ClickShape (CollisionShape2D)
```

- `Sprite`：NPC 的显示图。
- `DetectionRange`：玩家进入后才能交互。
- `ClickZone/ClickShape`：鼠标可点击区域。
- `ClickZone` 建议设置 `Collision Layer = 2`、`Collision Mask = 0`。
- `Blink` 是可选子节点，不影响 Dialogic 对话。

缺少上述固定子节点时，`InteractableBase._Ready()` 会因找不到节点而报错。

## 3. 最少对话配置

在 NPC Inspector 的 **Dialogic 对话** 分组中：

1. `Dialogue Timeline`：拖入 Dialogic `.dtl` 文件。
2. `Dialogue Character`：拖入该 NPC 的 `.dch` 文件。
3. `Player Dialogue Character`：拖入 Timeline 中代表玩家的 `.dch`。
4. `Additional Player Dialogue Characters`：把 reb 等同属主角一侧的 `.dch` 加入数组。
5. `Dialogue Bubble Offset`：调整 NPC 头顶锚点，默认 `(0, -96)`。
6. `Player Dialogue Bubble Offset`：调整玩家头顶锚点，默认 `(0, -160)`。

NPCBase 会自动在 NPC 和 `PlayerController.Instance` 下生成 Marker2D 锚点，不需要手动新建锚点节点。

> `Player Dialogue Character` 必须与 Timeline 里玩家台词所用的 Character 一致。像 reb 这样也应从主角位置发言的角色，请加入 `Additional Player Dialogue Characters`。两处都没配置的角色会进入 NPC 一侧。

## 4. 微信式双锚点气泡

- NPC 与玩家气泡分别使用各自的横向位置。
- 两个锚点在屏幕中使用统一高度。
- 每条新消息从当前说话者头顶出现。
- 无论谁发言，新消息都会将所有历史消息向上推。
- 历史气泡不会向下回落。
- 玩家在 NPC 左侧时，RUBBER、reb 和选项气泡的尾巴位于左侧；玩家移动到 NPC 右侧后自动翻到右侧。
- NPC 气泡尾巴使用相反方向，始终与当前说话者的屏幕左右位置一致。
- 选项使用 Dialogic 官方 `DialogicNode_ChoiceButton`，整个气泡都可点击。

主角侧可配置多个 Character；没有列入主角侧数组的其他角色和旁白显示在 NPC 一侧。

## 5. 对话期间暂停与输入独占

- Timeline 启动时，NPCBase 调用玩家现有的 `LockMovement()`，玩家会立即停止。
- 对话结束或 NPC 离开场景时只释放自己持有的一次移动锁，不会破坏其他系统的锁定状态。
- 独立 Canvas 会临时升到游戏 UI 上层，并创建透明全屏鼠标拦截层。
- 鼠标只能推进 Dialogic 文本或点击官方气泡选项；不会误点 NPC、地图或其他界面。
- 没有暂停 SceneTree，因此逐字文本、气泡 Tween、AnimationPlayer 演出和 Dialogic 分支保持正常。

## 6. Canvas 配置

Canvas 场景：

`res://features/dialogue/scenes/npc_dialogue_canvas.tscn`

Canvas 可以不放入关卡；第一次对话时 NPCBase 会自动实例化。如需调整外观，建议像示例一样在关卡根节点下显式放置一份。

| 参数 | 用途 |
| --- | --- |
| `Max Visible Bubbles` | 全局最大可见消息数，超出后清理最早气泡 |
| `Bubble Width` | 对话和选项气泡的最大宽度 |
| `Bubble Tween Time` | 气泡上冒动画时间 |
| `Bubble Spacing` | 历史气泡之间的垂直间距 |
| `Screen Margin` | 原 Canvas 布局的屏幕边距 |

同一场景不要放多份 `NPCDialogueCanvas`，否则多个 Canvas 会同时监听 Dialogic 全局信号。

当前气泡外观已经封装为统一的深蓝通讯屏样式：亮青边框、深蓝横纹内屏、青白文字与双层尾巴。对话和选项共用同一视觉语言；扫描纹资源位于 `res://features/dialogue/shaders/dialogue_scanlines.gdshader`。通常只需调整 Canvas 的宽度和间距，不需要修改 Shader 或 Dialogic 插件。

## 7. FriendlyNPC：对话后购买物品

固定在对话结束后开店：

1. 勾选 `Open Shop After Dialogue`。
2. 将场景里的 `ShopUI` 节点拖入 `Shop UI`。
3. 可选：将 `ExplorationHUD` 拖入 `Hud`，开店时会自动隐藏 HUD，关店后恢复。

仅在特定 Dialogic 分支开店：

1. 不勾选 `Open Shop After Dialogue`。
2. 在 Dialogic Timeline 的目标分支添加 **Logic → Signal**。
3. Signal 的 String 参数填写 `open_shop`。
4. Timeline 正常结束后打开商店。

`ShopUI.tscn` 已预置 `default_shop.tres`。自己新建 ShopUI 时，请确保其 `ShopManager` 已配置 ShopData。

## 8. HostileNPC：对话后进入战斗

配置：

1. 拖入 `Dialogue Timeline`。
2. 设置 `Battle Scene Path`，例如 `res://Scenes/game_scene/boom_battle_scene.tscn`。
3. 设置 `Battle Rules Path`，例如 `res://features/enemy_ai_node/rules/boom_rules.tres`。
4. 设置关卡内唯一的 `Persistence Id`。
5. 设置 `Return Spawn Id`，它必须与返回地图中某个 `SpawnPoint.SpawnId` 一致。
6. `DetectionRange.Shape` 建议使用 CircleShape2D；`Aggro Radius` 会同步这个圆的半径。

行为：

- 配置 Timeline：玩家进入范围后自动开始对话，Timeline 结束后进入战斗。
- 没有 Timeline：保持原功能，进入范围立即进入战斗。
- HostileNPC 不需要鼠标点击。

## 9. Dialogic 调用 AnimationPlayer

1. 将 NPC 或关卡的 AnimationPlayer 拖入 `Dialogue Animation Player`。
2. 在 Timeline 中添加 **Logic → Signal**。
3. 将 String 参数设为：

```text
animation:动画名
```

示例：

```text
animation:npc_nod
```

也支持 Dictionary：

```json
{"animation":"npc_nod"}
```

AnimationPlayer 中必须存在同名动画。一个 AnimationPlayer 可以同时绑定 NPC、玩家、相机或环境节点的动画轨道。

## 10. 对话操作和常见问题

- FriendlyNPC 当前使用基类的交互方式：玩家进入 `DetectionRange` 后鼠标左键点击。
- HostileNPC 进入范围自动开始，不响应鼠标点击。
- Timeline 推进使用 Dialogic 默认输入：Enter、Space 或左键。
- 选项可直接点击整个气泡。
- `NpcName` 用于日志和警告；气泡里的名字、颜色和台词仍由 Dialogic Timeline/Character 决定。
- 对话期间玩家移动被锁定，非对话鼠标输入被全屏透明拦截层阻止。
- 同一时刻只允许一个 NPC 占用 Dialogic Timeline，避免多个 NPC 同时开始。

## 11. 可连接的 NPC 信号

- `DialogueStarted`：Timeline 开始。
- `DialogueFinished`：Timeline 结束。
- `DialoguePluginSignal(argument)`：收到 Dialogic Signal Event。

如果后续需要任务、好感、镜头或其他演出逻辑，优先连接上述信号，不需要修改 Dialogic 插件。
