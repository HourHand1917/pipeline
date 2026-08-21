# Trained Enemy AI

独立、可拖拽的 Boom、Rocky、Sharkk 与 Core-00 二阶段 AI。模块只新增本目录文件，不替换战斗框架。

## 使用

在战斗地图的敌人数据槽中，拖入 `enemy_data/*_trained.tres`。也可只把 `scenes/*_trained_ai.tscn` 拖入现有 `EnemyDataNodeAdapter.ai_scene`。

AI只使用已经公开并结算的血量、护盾、位置、距离、伤害和行动类型历史。不会读取玩家手牌、牌库、背包、武器ID、强化标记、当前指令或未来随机数。

生产场景每场战斗生成一次随机种子，同一局内意图预览稳定；测试可调用 `lock_test_seed()` 固定种子。

## 验证

```text
godot --headless --path <project> res://features/enemy_ai_trained/tests/trained_ai_contract_test.tscn
godot --headless --path <project> res://features/enemy_ai_trained/training/balance_runner.tscn
```

注意：现有 `EffectResolver` 没有“随机选择若干未点亮格子”的接口。扬尘与禁用仍通过现有格子 Buff 接口结算；精确随机3/5格需要未来单独扩展结算器。
