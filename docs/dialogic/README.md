# NPC Dialogic 对话组件

本目录只提供项目适配层；`res://addons/dialogic/` 是下载版 Dialogic 的原样副本，本功能不修改插件源码。

## 使用

1. 将 `scenes/npc_dialogue_canvas.tscn` 拖入地图根节点；一张地图只放一个。
2. 将 `scenes/npc_interactables.tscn` 拖入地图，给 `Sprite` 设置角色贴图。
3. 在 NPC Inspector 的 `Timeline` 中拖入已配置好的 Dialogic `.dtl`。
4. 玩家进入检测范围后按 `E`，或沿用基类的范围内鼠标点击，即可开始对话。

NPC 只调用 Dialogic 的 `start_timeline()`。文本、等待、条件、跳转、变量、信号和选项分支仍完全由 Dialogic 处理。

## Canvas 行为

- 当前内容由官方 `DialogicNode_DialogText` 显示，保留逐字、BBCode 与点击推进。
- 每出现一条新文本，上一条会冻结为历史气泡；新气泡从下方淡入并把历史内容顶高。
- `[n+]` 追加内容保持在当前气泡内。
- 每个选项都是独立完整气泡，没有外层大框。
- 选项场景根节点为 `NPCDialogueChoiceBubble`，直接继承官方 `DialogicNode_ChoiceButton`；它没有重写 `_pressed()`，点击及分支仍由 Dialogic 处理。
- Canvas 预置 8 个选项气泡，Dialogic 自动填充文本、显示、禁用和选择状态。
- Canvas 跟随 NPC 的 `BubbleAnchor`，并在屏幕边缘自动限位。

## 可复用资源

- `scenes/dialogue_choice_bubble.tscn`：可复用的独立选项气泡。
- `scripts/dialogue_choice_bubble.gd`：只负责编号、状态色与入场表现的 Dialogic 按钮子类。
- `themes/wasteland_industrial_dialogue_theme.tres`：废土工业主题，可复用于其他按钮或对话控件。

## 约束

- 不要同时放两个 `NPCDialogueCanvas`，否则插件会找到重复的文本/选项节点。
- NPC Timeline 为空时不会启动，并在输出面板给出提示。
- 本组件使用预置 Canvas，因此 NPC 调用 `start_timeline()`；不要改成 `start()`，否则 Dialogic 会再创建一套默认布局。
- `ChoiceList` 必须留在可见场景树中。无选项时只使用透明度隐藏，否则 Dialogic 无法发现按钮。

## 测试

- `tests/npc_dialogue_runtime_test.tscn`：验证继承关系、无外框、复用主题、鼠标选择与 Dialogic 分支。
- `tests/npc_dialogue_visual_test.tscn`：输出新版废土工业 UI 预览。
