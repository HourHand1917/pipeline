# TV UI & Tooltip 系统工程规划

> 写给 Claude Code 自己的执行文档。
> 生成日期：2026-08-06
> 关联：[persistence-system.md](persistence-system.md)

---

## 一、实施顺序

**先做 Tooltip，再做 TV UI。**

理由：
1. Tooltip 是独立系统，零外部依赖，改现有代码最少
2. TV 面板（背包、商店、Buff）都需要 tooltip——先有了它，做 TV 时直接挂
3. Tooltip 范围小（~3 个文件），快速出成果，验证架构思路正确后信心更足
4. TV UI 依赖 tooltip，反过来 tooltip 不依赖 TV

```
Tooltip (1-2天)  →  TV UI (3-5天)
					   ├── 改 itempanel 适配 TV
					   ├── 改 enemymassage 适配 TV
					   ├── 新建 PlayerTV 容器
					   └── 新建 MapPanel
```

---

## 二、Tooltip 系统

### 2.1 架构

```
INFRASTRUCTURE              DATA                  PRESENTATION
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ TooltipService    │    │ TooltipData      │    │ TooltipPanel      │
│ (Autoload)        │◄──►│ (Resource)       │───►│ (CanvasLayer子)   │
│                   │    │                  │    │                   │
│ Show(data,pos)    │    │ title            │    │ ┌─ Icon           │
│ Hide()            │    │ description      │    │ ├─ Title          │
│ ShowFor(c, data)  │    │ icon             │    │ ├─ Description    │
│ HideFor(c)        │    │ details{}        │    │ └─ Details Grid   │
│ SetMode(m)        │    └──────────────────┘    │                   │
│ _follow_mouse     │                            │ AnimationPlayer   │
└──────────────────┘                            └──────────────────┘
		 ↑
  任意 Control
  mouse_entered → Show()
  mouse_exited  → Hide()
```

### 2.2 新增文件

| 文件 | 类型 | 说明 |
|---|---|---|
| `Script/CS/TooltipData.cs` | C# Resource | 纯数据：title/description/icon/details |
| `Script/CS/TooltipService.cs` | C# Autoload (CanvasLayer) | 管理 tooltip 生命周期、定位、延迟 |
| `Scenes/resource_scene/tooltip_panel.tscn` | PackedScene | 浮层 UI，AnimationPlayer 做淡入淡出 |
| `Script/CS/TooltipPanel.cs` | C# Control | 接收 TooltipData 渲染到对应节点 |

### 2.3 TooltipData 设计

```csharp
[GlobalClass]
public partial class TooltipData : Resource
{
	[Export] public string Title { get; set; }
	[Export] public string Description { get; set; }
	[Export] public Texture2D Icon { get; set; }
	[Export] public Godot.Collections.Dictionary<string, string> Details { get; set; }
	// Details 示例：{"射程":"1-3", "冷却":"2回合", "价格":"$20"}
}
```

设计师可在任何 .tres 里内联创建 TooltipData 作为子资源，也可以运行时动态构建。

### 2.4 TooltipService API

```csharp
// 核心
void Show(TooltipData data)                    // 在当前鼠标位置显示
void Hide()                                     // 隐藏

// 便捷：给 Control 绑定悬浮行为，自动连接 mouse_entered/exited
void ShowFor(Control c, TooltipData data)       // 绑定（调用后该 Control 悬浮即显）
void HideFor(Control c)                         // 解绑

// 编辑器支持
[Export] float ShowDelay = 0.3f;               // 延迟显示，避免快速扫过闪烁
[Export] Vector2 Offset = new(16, 16);         // 鼠标偏移
```

### 2.5 TooltipPanel 动画（参考 main_menu 的 AnimationPlayer 模式）

```
AnimationLibrary
├── RESET
│     visible = false, modulate.a = 0
├── fade_in
│     frame 0: visible = true
│     frame 0-0.15: modulate.a 0→1（Tween 式 ease）
└── fade_out
	  frame 0: visible = true（动画期间仍可见）
	  frame 0-0.1: modulate.a 1→0
	  frame 0.1: visible = false
```

### 2.6 使用示例

```csharp
// 商店物品
var d = new TooltipData {
	Title = entry.GetDisplayName(),
	Description = itemRes.Get("description").AsString(),
	Icon = itemRes.Get("icon").As<Texture2D>(),
	Details = new() { ["价格"] = $"${price}", ["库存"] = $"{stock}" }
};
TooltipService.Instance.ShowFor(btn, d);

// Buff 图标
TooltipService.Instance.ShowFor(buffIcon, buff.GetTooltipData());

// 宝箱奖励
TooltipService.Instance.ShowFor(rewardSlot, rewardItem.GetTooltipData());
```

### 2.7 接入现有系统

不改动任何现有数据类。运行时组装 TooltipData：

| 来源 | Title | Description | Icon | Details |
|---|---|---|---|---|
| ItemData | `display_name` | `description` | `icon` | `effects` 摘要 |
| CardData | `display_name` | `description` | — | 射程、冷却、效果 |
| BuffData | `buff_name` | `description` | `icon` | 持续回合、层数 |
| ShopEntry | `get_display_name()` | 物品的 description | 物品的 icon | 价格、库存 |

---

## 三、TV UI 系统

### 3.1 架构

```
PlayerTV (Control)                        ← 独立 PackedScene，嵌入探索/战斗场景
├── TVFrame (TextureRect)                 ← 电视机外壳美术
├── UpButton (Button)                     ← 上一面板
├── DownButton (Button)                   ← 下一面板
├── PanelStack
│   ├── MapPanel (Control)                ← 地图面板（新建）
│   ├── InventoryPanel (Control)          ← 改自 itempanel.tscn
│   └── EnemyPanel (Control)              ← 改自 enemymassage.tscn
├── PlayerTVAnim (AnimationPlayer)
└── PlayerTV.cs (脚本)
```

### 3.2 模式切换

```csharp
public enum TVMode { Exploration, Battle }

public void SetMode(TVMode mode)
{
	_mode = mode;
	// 向下传递给各面板
	MapPanel?.SetMode(mode);
	InventoryPanel?.SetMode(mode);
	EnemyPanel?.SetMode(mode);
}
```

每个面板根据 mode 调整行为：

| 面板 | Exploration | Battle |
|---|---|---|
| MapPanel | 可点击交互、选择路线 | 只读，仅显示当前位置 |
| InventoryPanel | 隐藏"使用"按钮，只允许丢弃 → 触发信号 `ItemDiscarded` | "使用" + "丢弃" 均显示 → 触发 `ItemUsed` / `ItemDiscarded` |
| EnemyPanel | `Visible = false` | 显示敌人血量/护盾/Buff列表/意图文字 |

### 3.3 面板切换

上下按钮控制当前显示的面板索引，面板之间做切换动画。

```
Up/Down → _currentIndex 循环 → 播放面板切换动画
```

**切换动画**（AnimationPlayer tracks）：

每个面板有两种动画状态——active 和 inactive。切换时旧面板滑出 + 新面板滑入。

```
show_map_panel:   MapPanel 滑入（右→中），其他面板滑出
show_inv_panel:   InventoryPanel 滑入，其他滑出
show_enemy_panel: EnemyPanel 滑入，其他滑出
```

或者更简单：用 modulate + visible 做淡入淡出切换（和主界面一样的模式）。

### 3.4 TV 容器动画（参考 main_menu.tscn 的动画模式）

和主界面完全一样的套路——RESET + 滑入 + 滑出 + Method Track：

```
AnimationLibrary
├── RESET
│     位置：屏幕右下角（anchor R=1.0, B=1.0）
│     modulate.a = 0（不可见）
│     所有面板 visible = false, MapPanel 默认激活
│
├── show_tv（从边缘滑入）
│     frame 0:
│       visible = true
│       method track → EnableButtons(true)
│     frame 0 → 0.5:
│       position: 从屏幕外(右) → 停靠位置
│       modulate.a: 0 → 1
│
├── hide_tv（滑出到边缘）
│     frame 0:
│       method track → EnableButtons(false)
│     frame 0 → 0.5:
│       position: 停靠位置 → 屏幕外(右)
│       modulate.a: 1 → 0
│     frame 0.5:
│       visible = false
│
├── switch_panel（面板切换）
│     frame 0:
│       旧面板: modulate.a 1 → 0
│       新面板: modulate.a 0 → 1, visible = true
│     frame 0.3:
│       旧面板: visible = false
```

### 3.5 面板之间的通信（遵循 skill 的 Signal Up, Call Down）

```
PlayerTV (Orchestrator)
  │
  │  Call Down（方法调用）              Signal Up（信号上报）
  │
  ├─→ MapPanel.SetMode(mode)           MapPanel.RouteSelected(routeId) ──→
  ├─→ MapPanel.ShowPlayerPos(pos)
  │
  ├─→ InventoryPanel.SetMode(mode)     InventoryPanel.ItemUsed(idx) ──→
  ├─→ InventoryPanel.Refresh(items)    InventoryPanel.ItemDiscarded(idx) ──→
  │
  └─→ EnemyPanel.SetMode(mode)         （纯展示，无上报信号）
	  EnemyPanel.UpdateEnemy(data)
```

### 3.6 新增 / 修改文件

| 文件 | 操作 | 说明 |
|---|---|---|
| `features/tv_ui/PlayerTV.cs` | **新建** | 容器脚本：面板切换、mode管理、TV动画触发 |
| `features/tv_ui/PlayerTV.tscn` | **新建** | TV PackedScene |
| `features/tv_ui/MapPanel.cs` | **新建** | 地图面板脚本 |
| `features/tv_ui/MapPanel.tscn` | **新建** | 地图面板场景 |
| `features/tv_ui/InventoryPanel.cs` | **新建** | 改自 ItemPanel.cs，加 mode 支持 |
| `Scenes/resource_scene/itempanel.tscn` | **修改** | 改尺寸为自适应，%UniqueName 替代 GetChild |
| `Script/CS/ItemPanel.cs` | **修改** | 加 SetMode，去硬编码，接收数据而非拉取 |
| `features/tv_ui/EnemyPanel.cs` | **新建** | 敌人面板脚本 |
| `Scenes/resource_scene/enemymassage.tscn` | **修改** | 加脚本、改尺寸自适应 |
| `home.tscn` | **修改** | UILayer 下嵌入 PlayerTV 实例 |
| 战斗场景 | **修改** | UILayer 下嵌入 PlayerTV 实例（SetMode(Battle)） |

### 3.7 需要修改的现有代码问题（itempanel）

| 问题 | 修改方式 |
|---|---|
| `itemGrid.GetChild(i)` 硬索引 | 改用 `%UniqueName`：`%itemslot0` ~ `%itemslot3` |
| 直接耦合 `DataManager.Instance` | `Refresh()` 改为接收参数 `Refresh(items, bottleCap, faucet)` |
| 尺寸是硬编码全屏数值 | 改用 anchor + container 自适应；TV面板需等比缩小 |
| 4 硬编码 | 引用 `DataManager.MaxItemSlots` |

### 3.8 需要修改的现有代码问题（enemymassage）

| 问题 | 修改方式 |
|---|---|
| 无脚本 | 新建 `EnemyPanel.cs`，更新血量/护盾条、buff网格、意图文字 |
| 固定像素尺寸 | 改为 anchor 自适应，嵌入 TV 后自动缩放 |
| TextureProgressBar 无逻辑 | 脚本中设置 `Value` / `MaxValue` |

---

## 四、工程 Checklist

### Phase 1: Tooltip（先做）
- [ ] 创建 `TooltipData.cs`
- [ ] 创建 `TooltipPanel.tscn` + `TooltipPanel.cs`（含 AnimationPlayer）
- [ ] 创建 `TooltipService.cs`（Autoload，注册到 project.godot）
- [ ] 接入商店：ShopUI 每个按钮加 tooltip
- [ ] 验证：鼠标悬浮商品按钮 → 淡入提示框 → 移开淡出

### Phase 2: TV UI
- [ ] 改 `enemymassage.tscn`：加 EnemyPanel.cs、自适应尺寸
- [ ] 改 `itempanel.tscn`：%UniqueName、接收数据、自适应尺寸
- [ ] 改 `ItemPanel.cs`：加 SetMode、去直接耦合
- [ ] 新建 `MapPanel.tscn` + `MapPanel.cs`
- [ ] 新建 `PlayerTV.tscn` + `PlayerTV.cs`（含 AnimationPlayer）
- [ ] 探索场景嵌入 PlayerTV：`home.tscn` → UILayer
- [ ] 战斗场景嵌入 PlayerTV：对应 .tscn → UILayer，SetMode(Battle)
- [ ] 给 TV 内所有物品/Buff/商店入口按钮接 tooltip
