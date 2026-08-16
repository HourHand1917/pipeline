# NPC 对话测试内容

- 可直接运行 `dialogue_content_demo.tscn` 进行手动测试。
- 玩家与鼠鼠初始就处于互动距离内；按 `E` 或点击鼠鼠即可开始。
- NPC 的 `Timeline` Inspector 属性已配置为 `timelines/水龙头与上层工程师.dtl`。
- 在“RUBBER：拒绝”处使用了 Dialogic 原生单选项，选择后继续同一条剧情。
- 可用 `dialogue_resource_test.tscn` 进行角色、Timeline、选项与场景引用的 headless 校验。
- 可用 `dialogue_content_flow_test.tscn` 验证从 NPC 互动到 Dialogic 启动、首个对话气泡显示的完整流程。
