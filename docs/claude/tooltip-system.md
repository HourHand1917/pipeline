# Tooltip 悬浮提示系统

> 最后更新：2026-08-06
> 关联：[tv-ui-and-tooltip-plan.md](tv-ui-and-tooltip-plan.md)

## 一、架构概览

```
┌─────────────────────────────────────────────────────────┐
│                     任何 Control                         │
│  btn.MouseEntered ──────────────────┐                   │
│  btn.MouseExited ──────────────┐    │                   │
│                                 │    │                   │
│  ┌──────────────────────────────┼────┼───────────────┐  │
│  │  TooltipService (Autoload)   │    │               │  │
│  │                              ▼    ▼               │  │
│  │  ShowFor(control, data) ← 绑定 mouse_entered/exited│  │
│  │  Show(data)             ← 立即显示                 │  │
│  │  HideTooltip()          ← 隐藏                    │  │
│  │  HideFor(control)       ← 解绑                    │  │
│  │                                                   │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │  TooltipPanel (PackedScene)                 │  │  │
│  │  │  ┌─ Icon (TextureRect)                      │  │  │
│  │  │  ├─ Title (Label)                           │  │  │
│  │  │  ├─ Description (Label)                     │  │  │
│  │  │  ├─ Details Grid (GridContainer, 2列)       │  │  │
│  │  │  └─ Anim (AnimationPlayer)                  │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────┐                               │
│  │  TooltipData (Resource)                              │
│  │  ├─ Title                                           │
│  │  ├─ Description                                     │
│  │  ├─ Icon (Texture2D)                                │
│  │  └─ Details (Dictionary<string,string>)             │
│  └──────────────────────┘                               │
└─────────────────────────────────────────────────────────┘
```

**三层分离：**

- `TooltipData`（DATA 层）——纯数据 Resource，谁产生数据谁填
- `TooltipService`（INFRASTRUCTURE 层）——Autoload，管理生命周期和定位
- `TooltipPanel`（PRESENTATION 层）——只管渲染，不管数据从哪来

## 二、涉及文件

| 文件                                         | 作用                                 | 谁维护              |
| ------------------------------------------ | ---------------------------------- | ---------------- |
| `Script/CS/TooltipData.cs`                 | 数据容器                               | 不动               |
| `Script/CS/TooltipService.cs`              | Autoload，Show/Hide/ShowFor/HideFor | 不动               |
| `Script/CS/TooltipPanel.cs`                | 渲染 TooltipData → 节点                | 不动               |
| `Scenes/resource_scene/tooltip_panel.tscn` | 面板布局 + AnimationPlayer             | **你维护**（改样式、调动画） |

## 三、API

### 一行绑定（最常用）

```csharp
TooltipService.Instance.ShowFor(control, data);
```

调用后该 Control 自动获得悬浮提示——鼠标移入延迟显示，移出隐藏。重复调用同一 Control 会更新数据。不需要时调 `HideFor(control)` 解绑。

### 手动控制

```csharp
// 立即显示（不管延迟）
TooltipService.Instance.Show(data);

// 立即隐藏
TooltipService.Instance.HideTooltip();
```

### 可调参数

在 `TooltipService` 的 Inspector 里：

- `ShowDelay`（默认 0.25s）——鼠标停留多久后显示
- `TooltipOffset`（默认 16,16）——相对鼠标的像素偏移

### TooltipData 字段

```csharp
new TooltipData {
	Title = "发条电池",           // 粗体标题
	Description = "造成 5 点伤害", // 灰字描述
	Icon = iconTexture,           // 左侧图标（可选）
	Details = new() {             // 键值对网格（可选）
		["射程"] = "1-3",
		["冷却"] = "2回合"
	}
};
```

所有字段都是可选的——传 null 或空字符串的部分自动隐藏。

## 四、数据流

```
1. 调用方组装 TooltipData
	   │
2. TooltipService.ShowFor(btn, data)
	   │  自动连接 btn.MouseEntered / MouseExited
	   │
3. 鼠标移入 btn
	   │  启动延迟计时器（ShowDelay 秒）
	   │
4. 计时器到
	   │  _panel.Render(data)  ← 填充节点
	   │  _anim.Play("fade_in") ← 播放淡入动画
	   │
5. 每帧 _process
	   │  面板跟随鼠标位置
	   │  检测屏幕边界，自动反向偏移
	   │
6. 鼠标移出 btn
	   │  _anim.Play("fade_out") ← 播放淡出动画
	   │  动画结束时 visible = false
```

## 五、如何在项目中接入

### 商店商品（已接入）

```csharp
// ShopUI.cs Refresh() — 每个商品按钮：
var data = new TooltipData {
	Title = entry.Call("get_display_name").AsString(),
	Description = itemRes.Get("description").AsString(),
	Icon = itemRes.Get("icon").As<Texture2D>(),
	Details = new() {
		["价格"] = $"${price}",
		["库存"] = $"{stock}"
	}
};
TooltipService.Instance.ShowFor(btn, data);
```

### Buff 图标

```csharp
// 战斗中给每个 Buff 图标挂 tooltip
var buffRes = ...; // BuffData 资源
var data = new TooltipData {
	Title = buffRes.Get("buff_name").AsString(),
	Description = buffRes.Get("description").AsString(),
	Icon = buffRes.Get("icon").As<Texture2D>(),
	Details = new() {
		["持续"] = $"{buffRes.Get("duration").AsInt32()} 回合",
		["层数"] = $"{stacks}"
	}
};
TooltipService.Instance.ShowFor(buffIcon, data);
```

### 背包物品

```csharp
// ItemPanel 里给每个物品槽挂 tooltip
var item = DataManager.Instance.GetItem(i);
var data = new TooltipData {
	Title = item.Get("display_name").AsString(),
	Description = item.Get("description").AsString(),
	Icon = item.Get("icon").As<Texture2D>(),
	Details = new() { ["类型"] = "道具" }
};
TooltipService.Instance.ShowFor(itemSlot, data);
```

### 卡牌信息

```csharp
// 战斗手牌 / 卡牌预览
var cardRes = ...; // CardData 资源
var data = new TooltipData {
	Title = cardRes.Get("display_name").AsString(),
	Description = cardRes.Get("description").AsString(),
	Details = new() {
		["射程"] = cardRes.Call("range_text").AsString(),
		["冷却"] = $"{cardRes.Get("cooldown_turns").AsInt32()} 回合"
	}
};
TooltipService.Instance.ShowFor(cardBtn, data);
```

### 宝箱 / 掉落奖励

```csharp
// 奖励选择界面
foreach (var reward in rewards) {
	var data = new TooltipData {
		Title = reward.Get("display_name").AsString(),
		Description = reward.Get("description").AsString(),
		Icon = reward.Get("icon").As<Texture2D>()
	};
	TooltipService.Instance.ShowFor(rewardSlot, data);
}
```

## 六、数据来源速查表

| 来源   | GDScript 类型 | Title                | Description     | Icon     | 特有 Detail                        |
| ---- | ----------- | -------------------- | --------------- | -------- | -------------------------------- |
| 道具   | `ItemData`  | `display_name`       | `description`   | `icon`   | —                                |
| 卡牌   | `CardData`  | `display_name`       | `description`   | —        | `range_text()`, `cooldown_turns` |
| Buff | `BuffData`  | `buff_name`          | `description`   | `icon`   | `duration`                       |
| 商店商品 | `ShopEntry` | `get_display_name()` | 物品的 description | 物品的 icon | `price`, `stock`                 |

## 七、定制 TooltipPanel 外观

打开 `Scenes/resource_scene/tooltip_panel.tscn`：

- **改配色**：选 `TooltipPanel` 节点，在 Inspector → Theme Overrides → Panel 里调 `StyleBoxFlat` 的 `bg_color` / `border_color`
- **改字体大小**：选 `Title` / `Desc` 节点调 `font_size`
- **改动画**：选 `Anim` 节点，编辑 `fade_in` / `fade_out` 的时长和缓动曲线
- **改面板宽度**：调 `TooltipPanel` 的 `custom_minimum_size.x`

## 八、注意事项

1. **不要直接 new TooltipPanel**——它由 TooltipService 在 `_Ready()` 里实例化，全局唯一
2. **ShowFor 要配对 HideFor**——如果控件会被销毁，在销毁前调用 `HideFor(control)` 解绑，避免引用已释放的对象
3. **Details 值用 string**——int/float 需要 `.ToString()` 或 `$"{x}"` 转字符串
4. **跨语言安全**——用 `itemRes.Get("field")` / `itemRes.Call("method")` 访问 GDScript 资源，不要强转类型
