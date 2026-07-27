using Godot;

public partial class EffectResolver : Node
{
    [Signal] public delegate void EffectExecutedEventHandler(string cardName, string effectType, int value);

    /// <summary>
    /// 执行卡片效果
    /// </summary>
    public void Execute(GodotObject card, BattleManager battleManager)
    {
        var data = card.Get("data").As<GodotObject>();
        string effectType = (string)data.Get("effect_type");
        int effectValue = (int)data.Get("effect_value");
        string displayName = (string)data.Get("display_name");

        switch (effectType)
        {
            case "damage":
                battleManager.DamageEnemy(effectValue);
                break;

            case "shield":
                battleManager.AddPlayerShield(effectValue);
                break;

            case "energy":
                battleManager.AddPlayerEnergy(effectValue);
                break;

            case "heal":
                battleManager.HealPlayer(effectValue);
                break;

            default:
                GD.PushWarning($"未知效果类型：{effectType}");
                return;
        }

        EmitSignal(SignalName.EffectExecuted, displayName, effectType, effectValue);
    }
}