# DataManager 背包简化方案

## 现状

两个字典协作，存在设计不一致：

- `CardDataLog`（图鉴）：永不清除，类图鉴
- `CardsCount`（背包）：消费到 0 时删除条目

用户的意图是纯背包——卡消耗完就从字典里消失，不需要图鉴。

## 方案：单字典

```csharp
// 一条记录 = 卡牌数据 + 持有数量
private Dictionary<StringName, InventoryEntry> _inventory = new();

private struct InventoryEntry
{
    public Resource CardData;
    public int Count;
}
```

### 改造前后对比

**AcquireCard 之前**：两个字典各写各的，数据源永远缓存
**AcquireCard 之后**：一个字典，首次获取写入 Resource，后续只改 Count

**ConsumeCard 之前**：只删 `CardsCount`，`CardDataLog` 残留
**ConsumeCard 之后**：Count 归零时整个条目移除，干净

**GetCard 之前**：从 `CardDataLog` 查
**GetCard 之后**：从 `_inventory` 查

### 不需要改的地方

- `SaveBuild` / `LoadBuild`：存的是 `card_id` 字符串，不依赖卡牌是否在背包里
- `GetRules` / `GetRecommendedLoadout`：独立的规则系统，不动

### 风险

- 消耗掉后再获得同一张卡，Resource 需要重新传入（`AcquireCard(Resource, count)` 本来就是传 Resource 的，没问题）
- 如果其他地方直接用 `CardDataLog` 或 `CardsCount` 字典，需要改为用 `_inventory`（项目里目前只有 DataManager 自己用）
