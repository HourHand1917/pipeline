# NPC Dialogic 对话组件

本目录只提供项目适配层；`res://addons/dialogic/` 是下载版 Dialogic 的原样副本，本功能不修改插件源码。

## 使用

1. 将 `scenes/npc_dialogue_canvas.tscn` 拖入地图根节点；一张地图只放一个。
2. 将 `scenes/npc_interactables.tscn` 拖入地图，给 `Sprite` 设置角色贴图。
3. 在 NPC Inspector 的 `Timeline` 中拖入已配置好的 Dialogic `.dtl`。
4. 把 NPC 自带的 `BubbleAnchor` 移到角色头顶，并把对应 `.dch` 拖进 `Dialogic Character`。
5. 其他会说话的角色，只需把 `scenes/dialogue_speaker_anchor.tscn` 拖到角色节点下，再选择对应 `.dch`。
6. 玩家进入检测范围后按 `E`，或沿用基类的范围内鼠标点击，即可开始对话。

不需要填写 NodePath、注册角色或编写脚本。Canvas 会按 Dialogic 当前说话者自动寻找对应锚点；未配置锚点时仍会回退到启动对话的 NPC 头顶。

NPC 只调用 Dialogic 的 `start_timeline()`。文本、等待、条件、跳转、变量、信号和选项分支仍完全由 Dialogic 处理。

## Canvas 行为

- 当前内容由官方 `DialogicNode_DialogText` 显示，保留逐字、BBCode 与点击推进。
- 每个角色从自己的 `DialogueSpeakerAnchor` 位置发出气泡。
- 每出现一条新文本，上一条会冻结为历史气泡；新气泡向上浮入，并把全部历史气泡继续向上顶。
- 历史气泡的 Y 坐标只会减小，切换说话者和角色移动都不会让旧气泡向下回弹。
- `[n+]` 追加内容保持在当前气泡内。
- 每个选项都是独立完整气泡，没有外层大框。
- 选项场景根节点为 `NPCDialogueChoiceBubble`，直接继承官方 `DialogicNode_ChoiceButton`；它没有重写 `_pressed()`，点击及分支仍由 Dialogic 处理。
- Canvas 预置 8 个选项气泡，Dialogic 自动填充文本、显示、禁用和选择状态。
- 当前说话气泡按角色锚点定位，并在屏幕左右边缘自动限位。

### Inspector 可调项

- `Max Visible Bubbles`：最多保留多少条历史气泡，默认 8，范围 1–64。
- `Min Bubble Width` / `Max Bubble Width`：短句会收窄，长句达到最大宽度后自动换行。
- `Bubble Spacing`：历史气泡之间的间距。
- `Bubble Tween Time`：向上浮动时长。
- `Bubble Offset`：所有气泡相对角色锚点的统一偏移。

## 可复用资源

- `scenes/dialogue_choice_bubble.tscn`：可复用的独立选项气泡。
- `scenes/dialogue_speaker_anchor.tscn`：拖到每个说话角色头顶的零代码锚点。
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
