using Godot;

/// <summary>
/// 已配置好的酒吧老板 NPC。直接拖入探索场景即可使用。
/// 对话、分支、气泡选项和双锚点全部复用 FriendlyNPC/NPCBase/Dialogic；
/// 本脚本只负责注册本 NPC 角色、加载默认 Timeline 和初始化购买状态。
/// </summary>
[GlobalClass]
public partial class TavernOwnerNPC : FriendlyNPC
{
    [Signal]
    public delegate void TavernPurchaseRequestedEventHandler(StringName purchaseId, bool repeatable);

    private const string FaucetVariable = "tavern_faucet_purchased";
    private const string IntelVariable = "tavern_intel_purchased";

    [ExportGroup("酒吧老板默认内容")]
    [Export(PropertyHint.File, "*.dtl")]
    public string DefaultTimelinePath { get; set; }
        = "res://features/dialogue/npc/timelines/酒吧老板商店对话.dtl";

    public bool PurchaseStateReady { get; private set; }

    public override void _Ready()
    {
        PackagedDialogueRegistration.RegisterConfiguredCharacters(this);
        if (DialogueTimeline == null && !string.IsNullOrEmpty(DefaultTimelinePath))
            DialogueTimeline = GD.Load<Resource>(DefaultTimelinePath);
        VerifyPurchaseVariables();
        base._Ready();
    }

    protected override void OnDialogueSignalReceived(Variant argument)
    {
        base.OnDialogueSignalReceived(argument);
        if (argument.VariantType != Variant.Type.String &&
            argument.VariantType != Variant.Type.StringName)
            return;

        switch (argument.AsString())
        {
            case "tavern_purchase_drink":
                EmitSignal(SignalName.TavernPurchaseRequested, new StringName("hard_bone_drink"), true);
                break;
            case "tavern_purchase_faucet":
                EmitSignal(SignalName.TavernPurchaseRequested, new StringName("faucet"), false);
                break;
            case "tavern_purchase_intel":
                EmitSignal(SignalName.TavernPurchaseRequested, new StringName("upper_route_intel"), false);
                break;
        }
    }

    private void VerifyPurchaseVariables()
    {
        Node dialogic = GetNodeOrNull<Node>("/root/Dialogic");
        GodotObject variables = dialogic?.Get("VAR").AsGodotObject();
        if (variables == null)
        {
            GD.PushWarning("TavernOwnerNPC：Dialogic Variables 子系统不可用，购买选项状态无法初始化。");
            return;
        }

        Variant faucet = variables.Call("get_variable", FaucetVariable, default(Variant), true);
        Variant intel = variables.Call("get_variable", IntelVariable, default(Variant), true);
        PurchaseStateReady = faucet.VariantType != Variant.Type.Nil && intel.VariantType != Variant.Type.Nil;
    }
}
