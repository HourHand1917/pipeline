# 战斗动画序列帧目录

这里仅存放战斗动画原始序列帧。每组动画使用固定画布、透明 PNG、连续数字文件名：

```text
anime_assets/frames/<角色>/<动作>/0001_0.png
anime_assets/frames/<角色>/<动作>/0002_4.png
```

导入器只按文件名前面的数字排序，后缀 `_0`、`_4`、`_5` 不参与排序。不要逐帧裁边，否则播放时会抖动。

当前已导入 783 帧：Rubber 103 帧、Boom 148 帧、Core-00 双手 532 帧。Rocky、Sharkk、Core-00 二阶段本体目录可按相同规则补入素材，再运行 `res://anime/tools/build_spriteframes.gd`。

源 PNG 保留完整 1080×1080 / 1080×1350 画质；对应 `.png.import` 将运行时纹理限制为最长边 256 像素，避免 783 帧在显存中展开到数 GB。战斗显示大小由 Profile 的 `target_visual_height` 独立控制。
