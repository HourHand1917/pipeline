using Godot;

public partial class EnemyBasicNode : EnemyBattle
{
    private Button debugButton;

	public override void _Ready()
	{
		base._Ready();

		debugButton = new Button();
		debugButton.Text = "信息";
		debugButton.Position = new Vector2(0, 0);
		debugButton.Size = new Vector2(80, 30);
		AddChild(debugButton);

		debugButton.Pressed += PrintDebugInfo;
	}

    private void PrintDebugInfo()
    {
        GD.Print($"=== {DisplayName} 状态 ===");
        GD.Print($"HP: {CurrentHp}/{MaxHp}");
        GD.Print($"护盾: {Shield}");
        GD.Print($"位置: {MapPosition}");
        GD.Print($"朝向: {(Facing == 0 ? "正方向" : "负方向")}");
        GD.Print($"存活: {IsAlive}");

        // Buff
        var stats = GetStats();
        if (stats != null)
        {
            var buffs = stats.Get("buffs").As<Godot.Collections.Array>();
            GD.Print($"Buff 数量: {buffs.Count}");
            for (int i = 0; i < buffs.Count; i++)
            {
                var bi = buffs[i].As<GodotObject>();
                if (bi == null) continue;
                var buff = bi.Get("buff").As<GodotObject>();
                int stacks = bi.Get("stacks").AsInt32();
                GD.Print($"  [{i}] {buff.Get("buff_name")} ×{stacks}");
            }
        }

        GD.Print("=========================");
    }
}