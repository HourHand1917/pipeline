using Godot;
using System.Collections.Generic;

public partial class DataManager : Node
{
    [Signal] public delegate void BuildSavedEventHandler();

    public static DataManager Instance { get; private set; }

    private Dictionary<StringName, GodotObject> cardDatabase = new();
    private Godot.Collections.Array<Godot.Collections.Dictionary> savedBuild = new();

    public override void _Ready()
    {
        Instance = this;
        CreateDefaultCards();
    }

    /// <summary>
    /// 创建默认卡片数据库
    /// </summary>
    private void CreateDefaultCards()
    {
        cardDatabase.Clear();

        cardDatabase["revolver"] = MakeCard(
            "revolver",
            "左轮枪",
            "✦",
            "攻击",
            new Vector2I[] { new(0, 0), new(1, 0) },
            "damage",
            3,
            1,
            "点亮2格后，对敌人造成3点伤害。"
        );

        cardDatabase["shield"] = MakeCard(
            "shield",
            "铁皮盾",
            "◆",
            "防御",
            new Vector2I[] { new(0, 0), new(0, 1) },
            "shield",
            3,
            1,
            "点亮2格后，获得3点护盾。"
        );

        cardDatabase["battery"] = MakeCard(
            "battery",
            "发条电池",
            "⚙",
            "能量",
            new Vector2I[] { new(0, 0) },
            "energy",
            2,
            1,
            "点亮1格后，获得2点能量。"
        );

        cardDatabase["medkit"] = MakeCard(
            "medkit",
            "急救包",
            "✚",
            "恢复",
            new Vector2I[] { new(0, 0), new(0, 1), new(1, 1) },
            "heal",
            2,
            2,
            "点亮L形3格后，恢复2点生命。"
        );
    }

    /// <summary>
    /// 创建一张 CardData（使用 Set 动态设置属性）
    /// </summary>
    private GodotObject MakeCard(
        StringName cardId,
        string cardName,
        string icon,
        string category,
        Vector2I[] shape,
        string effectType,
        int value,
        int cooldown,
        string description)
    {
        // GDScript 的 CardData.new() 等价于这样调用
        var card = GD.Load<GDScript>("res://Script/GD/resource/carddata.gd").New().As<GodotObject>();

        card.Set("id", cardId);
        card.Set("display_name", cardName);
        card.Set("icon_text", icon);
        card.Set("category", category);

        var shapeArray = new Godot.Collections.Array<Vector2I>();
        foreach (var s in shape)
            shapeArray.Add(s);
        card.Set("shape_offsets", shapeArray);

        card.Set("effect_type", effectType);
        card.Set("effect_value", value);
        card.Set("cooldown_turns", cooldown);
        card.Set("description", description);

        return card;
    }

    /// <summary>
    /// 根据 ID 获取卡片
    /// </summary>
    public GodotObject GetCard(StringName cardId)
    {
        if (cardDatabase.TryGetValue(cardId, out var card))
            return card;
        return null;
    }

    /// <summary>
    /// 获取所有卡片
    /// </summary>
    public List<GodotObject> GetAllCards()
    {
        var result = new List<GodotObject>();
        foreach (var value in cardDatabase.Values)
            result.Add(value);
        return result;
    }

    /// <summary>
    /// 保存当前构筑
    /// </summary>
    public void SaveBuild(List<GodotObject> runtimeCards)
    {
        savedBuild.Clear();

        foreach (var runtime in runtimeCards)
        {
            var data = runtime.Get("data").As<GodotObject>();
            var dict = new Godot.Collections.Dictionary
            {
                { "card_id", data.Get("id").AsStringName() },
                { "anchor", (Vector2I)runtime.Get("anchor_position") },
                { "rotation", (int)runtime.Get("rotation_steps") }
            };
            savedBuild.Add(dict);
        }

        EmitSignal(SignalName.BuildSaved);
    }

    /// <summary>
    /// 加载构筑（返回副本）
    /// </summary>
    public Godot.Collections.Array<Godot.Collections.Dictionary> LoadBuild()
    {
        var duplicate = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var dict in savedBuild)
        {
            var newDict = new Godot.Collections.Dictionary();
            foreach (var key in dict.Keys)
                newDict[key] = dict[key];
            duplicate.Add(newDict);
        }
        return duplicate;
    }
}