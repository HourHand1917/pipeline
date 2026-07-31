# 横向卷轴探索系统

## 概述

基于 Godot 4.6 + C# 的横向卷轴探索框架。支持多张地图复用，提供角色移动、相机跟随、NPC/物体互动功能。

## 文件结构

```
features/exploration/
├── scenes/
│   ├── player.tscn              ← 角色场景（所有地图共用）
│   └── exploration_map.tscn     ← 示例地图
└── scripts/CS/
    ├── ExplorationManager.cs     ← 地图根节点，配置相机边界和玩家引用
    ├── PlayerController.cs       ← 角色移动 + 朝向翻转
    ├── CameraFollow.cs           ← 相机平滑跟随
    ├── states/
    │   ├── PlayerStateBase.cs     ← 状态基类（RefCounted）
    │   ├── PlayerIdleState.cs     ← 闲置
    │   ├── PlayerMoveState.cs     ← 移动
    │   └── PlayerStateMachine.cs  ← Flat FSM 状态机
    └── interactables/
        ├── InteractableBase.cs       ← 互动基类
        ├── WorkbenchInteractable.cs  ← 工作台
        ├── MerchantInteractable.cs   ← 商人
        ├── CampfireInteractable.cs   ← 篝火
        ├── ChestInteractable.cs      ← 宝箱
        └── HostileNPCInteractable.cs ← 敌对 NPC
```

## 架构

```
ExplorationManager (Node2D)       ← 场景根，配置相机边界
├── Background (ColorRect)        ← 地图背景（需设 mouse_filter=Ignore）
├── Ground (StaticBody2D)         ← 地面碰撞体
├── Player (场景实例)             ← 来自 player.tscn
│   ├── Sprite2D                  ← 角色占位图（64x64）
│   ├── CollisionShape2D          ← 碰撞体
│   ├── Camera2D (CameraFollow)   ← 平滑跟随相机
│   ├── Area2D (InteractionArea)  ← 互动检测区
│   └── StateMachine              ← Idle ↔ Move
├── 交互物 × N (Area2D)           ← 各类型交互物体
│   ├── Sprite2D                  ← 占位方块（64x64）
│   └── CollisionShape2D          ← 互动范围（圆形）
```

### 数据流

```
键盘 A/D → ExplorationManager._Input()
  → Player.OnMovePressed(dir)
    → StateMachine.RequestMove(dir)
      → PlayerMoveState 施加速度

鼠标靠近 → Area2D.MouseEntered
  → InteractableBase 变亮（玩家需在范围内）

鼠标点击 + 在范围内 → Area2D.input_event
  → InteractableBase.HandleInteract()
    → GD.Print("与「xxx」互动。")
```

## 可复用组件

### 角色（player.tscn）

CharacterBody2D，状态机驱动。美术资源到位后：

- 替换 Sprite2D 贴图
- FaceDirection() 自动处理左右翻转
- 移动速度在 Inspector 中可调（MoveSpeed）

### 相机（CameraFollow）

Player 子节点，自动平滑跟随。边界由 ExplorationManager 在运行时设置，每张地图独立配置。

### 交互物

5 种类型，拖入场景即用：

| 类型     | 颜色  | 行为                 |
| ------ | --- | ------------------ |
| 工作台    | 棕色  | 点击互动 - 打开合成界面（待实现） |
| 商人     | 金色  | 点击互动 - 打开商店界面（待实现） |
| 篝火     | 橙色  | 点击互动 - 休息回复（待实现）   |
| 宝箱     | 蓝色  | 点击互动 - 打开宝箱（待实现）   |
| 敌对 NPC | 红色  | 靠近自动触发战斗（待实现）      |

Inspector 中配置：

- `DisplayName`：控制台输出的名称
- `InteractionRadius`：互动检测半径（默认 120px）

### 状态机

RefCounted 实现的 Flat FSM。两个状态：

- **Idle**：静止，速度清零
- **Move**：持续施加速度 + 朝向翻转

新增状态时在 `PlayerStateMachine.cs` 中添加即可。

## 创建新地图

### 方法一：继承场景（推荐）

1. 右键 `exploration_map.tscn` → **New Inherited Scene**
2. 修改 Background 颜色或替换贴图
3. 调整 Ground 位置和宽度
4. 增删交互物，调整位置
5. 在根节点 Inspector 中设 `MapLeft/Right/Top/Bottom`
6. 保存

### 方法二：从零创建

1. 新建场景，根节点类型选 `ExplorationManager`
2. 添加 `MapLayer`（Node2D）子节点
3. 在 MapLayer 下添加：
   - `Background`（ColorRect），设置 `mouse_filter = Ignore`
   - `Ground`（StaticBody2D + CollisionShape2D，RectangleShape2D）
   - `Player` — 实例化 `res://features/exploration/scenes/player.tscn`
4. 在 MapLayer 下添加交互物（Area2D + 对应脚本 + Sprite2D + CollisionShape2D）
5. 在 ExplorationManager 中将 Player 引用拖入 `Player` 属性
6. 设置 `MapLeft/Right/Top/Bottom` 相机边界
7. 保存

### 地图配置检查清单

- [ ] `Player` 已拖入 ExplorationManager 的 `Player` 导出属性
- [ ] Background 的 `mouse_filter` 设为 **Ignore**
- [ ] Ground 有 CollisionShape2D + RectangleShape2D
- [ ] 每个交互物有：Area2D 脚本 + Sprite2D + CircleShape2D
- [ ] 交互物 `DisplayName` 已填写
- [ ] `MapLeft/Right/Top/Bottom` 与实际地图尺寸匹配
- [ ] 地图场景设为 F6 可直接运行

## 当前交互效果

| 交互方式            | 效果                         |
| --------------- | -------------------------- |
| 鼠标靠近交互物（玩家在范围内） | 方块变亮 1.5 倍                 |
| 鼠标离开            | 恢复原始亮度                     |
| 点击交互物（玩家在范围内）   | 控制台输出 "与「xxx」互动。"          |
| 靠近敌对 NPC        | 控制台输出 "遭遇「xxx」！进入战斗（待实现）。" |

## 后续扩展

- **动画**：PlayerMoveState.Enter() 中调用 AnimationPlayer，美术到位后接入
- **场景切换**：向 ExplorationManager 添加 `ChangeMap(string path)` 方法
- **互动 UI**：玩家靠近时显示提示文字，点击后弹出对应界面
- **地图传送**：添加 `PortalInteractable`，点击后切换到另一张地图
