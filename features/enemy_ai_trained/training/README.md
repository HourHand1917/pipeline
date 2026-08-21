# 训练与复测

`rounds.json`记录三轮筛选规模，`locked_balance_profile.json`保存最终代理参数，`strategy_profiles.json`覆盖八类常见操作。运行 `balance_runner.tscn`得到可重复的最终回归结果。

运行时AI并不加载这些文件；它只使用公开战斗历史。训练配置仅用于离线复测。
