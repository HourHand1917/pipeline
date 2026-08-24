# NPC 序列帧动画

三套动画均为 1080×1080、47 帧、24 FPS 循环待机：

- `frames/mousy`：鼠鼠，配置到三个鼠鼠对话预制体。
- `frames/richard`：赌场机器人 Richard，配置到赌场荷官预制体。
- `frames/upper_couple`：源目录名为“瓶盖&水管”，实际画面是富婆与细狗，配置到上层双人 NPC。

动画机脚本 `npc_frame_animation.gd` 会按文件名自然顺序读取 PNG，自动生成 `idle` 动画。选中 NPC 的 `Sprite` 节点即可在 Inspector 调整：

- `Frames Directory`
- `Frames Per Second`
- `Loop Animation`
- `Position`、`Scale`、`Flip H`

保持节点名为 `Sprite`，现有交互和 BlinkComponent 会继续工作。
