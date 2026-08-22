using Godot;

/// <summary>
/// Boom 战斗教学的 Inspector 配置资源。
/// 策划可以直接在 .tres 窗口修改六步标题、说明和聚光视觉参数，
/// 不需要改动教程状态机代码。
/// </summary>
[GlobalClass]
public partial class BoomBattleTutorialConfig : Resource
{
    [ExportGroup("启用条件")]
    [Export] public bool Enabled { get; set; } = true;
    [Export] public bool RequireSingleBoomEncounter { get; set; } = true;

    [ExportGroup("聚光外观")]
    [Export(PropertyHint.Range, "0,40,1")] public float TargetPadding { get; set; } = 12f;
    [Export(PropertyHint.Range, "0,0.9,0.01")] public float DimOpacity { get; set; } = 0.66f;
    [Export(PropertyHint.Range, "320,900,10")] public float BubbleWidth { get; set; } = 520f;
    [Export(PropertyHint.Range, "110,300,2")] public float BubbleHeight { get; set; } = 154f;
    [Export(PropertyHint.Range, "8,100,1")] public float BubbleGap { get; set; } = 34f;
    [Export(PropertyHint.Range, "0,80,1")] public float ScreenMargin { get; set; } = 24f;
    [Export] public Color DimColor { get; set; } = new(0.015f, 0.025f, 0.035f, 1f);
    [Export] public Color HighlightColor { get; set; } = new("74e8ec");
    [Export] public Color StepColor { get; set; } = new("f1c453");
    [Export] public Color TitleColor { get; set; } = new("74e8ec");
    [Export] public Color TextColor { get; set; } = Colors.White;
    [Export] public Color BubbleColor { get; set; } = new("101b20f2");

    [ExportGroup("步骤 1 · 点亮格子")]
    [Export] public string LightCellsTitle { get; set; } = "点亮格子";
    [Export(PropertyHint.MultilineText)] public string LightCellsInstruction { get; set; }
        = "点击高亮格子，消耗能量点亮卡牌。";

    [ExportGroup("步骤 2 · 使用卡牌")]
    [Export] public string UseCardsTitle { get; set; } = "使用已点亮卡牌";
    [Export(PropertyHint.MultilineText)] public string UseCardsInstruction { get; set; }
        = "卡牌全部点亮后，再点击卡牌即可使用。";

    [ExportGroup("步骤 3 · 点击移动")]
    [Export] public string MoveTitle { get; set; } = "点击格子移动";
    [Export(PropertyHint.MultilineText)] public string MoveInstruction { get; set; }
        = "点击轨道上的空格，移动玩家位置。";

    [ExportGroup("步骤 4 · 切换面板")]
    [Export] public string SwitchPanelTitle { get; set; } = "切换敌人面板";
    [Export(PropertyHint.MultilineText)] public string SwitchPanelInstruction { get; set; }
        = "点击上箭头，切换到敌人信息面板。";

    [ExportGroup("步骤 5 · 查看敌人")]
    [Export] public string InspectEnemyTitle { get; set; } = "观察血量与意图";
    [Export(PropertyHint.MultilineText)] public string InspectEnemyInstruction { get; set; }
        = "点击 Boom 脚下，查看血量与行动意图。";

    [ExportGroup("步骤 6 · 结束回合")]
    [Export] public string EndTurnTitle { get; set; } = "结束玩家回合";
    [Export(PropertyHint.MultilineText)] public string EndTurnInstruction { get; set; }
        = "行动完成后，点击结束回合，让敌人开始行动。";
}
