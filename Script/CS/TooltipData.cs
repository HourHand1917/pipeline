using Godot;
using Godot.Collections;

/// <summary>
/// 悬浮提示数据。可在 Inspector 里内联创建，也可运行时动态构建。
/// Details 示例：{"射程":"1-3", "冷却":"2回合", "价格":"$20"}
/// </summary>
[GlobalClass]
public partial class TooltipData : Resource
{
    [Export] public string Title { get; set; }
    [Export(PropertyHint.MultilineText)] public string Description { get; set; }
    [Export] public Texture2D Icon { get; set; }
    [Export] public Dictionary<string, string> Details { get; set; } = new();
}
