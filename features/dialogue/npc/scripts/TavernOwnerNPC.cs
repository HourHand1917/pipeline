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
    [Signal]
    public delegate void TavernTransactionResultEventHandler(bool success, string message);

    private const string FaucetVariable = "tavern_faucet_purchased";
    private const string IntelVariable = "tavern_intel_purchased";
    private const string DrinkResultVariable = "tavern_drink_purchase_result";
    public const int HardBonePrice = 16;

    public enum DrinkPurchaseResult
    {
        NotAttempted = 0,
        Success = 1,
        InsufficientBottleCaps = 2,
        ItemBagFull = 3,
        ConfigurationError = 4,
    }

    [ExportGroup("酒吧老板默认内容")]
    [Export(PropertyHint.File, "*.dtl")]
    public string DefaultTimelinePath { get; set; }
        = "res://features/dialogue/npc/timelines/酒吧老板商店对话.dtl";
    [Export] public Resource HardBoneItem { get; set; }

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
                TryPurchaseHardBone();
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

    /// <summary>
    /// Atomic tavern transaction. A failed delivery never consumes bottle caps;
    /// a successful purchase always delivers exactly one consumable item.
    /// </summary>
    public int TryPurchaseHardBone()
    {
        DataManager data = DataManager.Instance;
        DrinkPurchaseResult result;
        string message;

        if (data == null || HardBoneItem == null)
        {
            result = DrinkPurchaseResult.ConfigurationError;
            message = "硬骨头暂时无法出售。";
        }
        else if (data.BottleCap < HardBonePrice)
        {
            result = DrinkPurchaseResult.InsufficientBottleCaps;
            message = $"瓶盖不足：硬骨头需要 {HardBonePrice} 瓶盖。";
        }
        else if (data.ItemBag.Count >= DataManager.MaxItemSlots)
        {
            result = DrinkPurchaseResult.ItemBagFull;
            message = "道具栏已满，先腾出一个格子。";
        }
        else
        {
            data.ModifyCurrency(DataManager.CurrencyType.BottleCap, -HardBonePrice);
            if (!data.AddItem(HardBoneItem))
            {
                data.ModifyCurrency(DataManager.CurrencyType.BottleCap, HardBonePrice);
                result = DrinkPurchaseResult.ItemBagFull;
                message = "道具栏已满，购买没有扣款。";
            }
            else
            {
                result = DrinkPurchaseResult.Success;
                message = $"购买硬骨头成功，花费 {HardBonePrice} 瓶盖。";
            }
        }

        SetDialogicVariable(DrinkResultVariable, (int)result);
        EmitSignal(SignalName.TavernTransactionResult, result == DrinkPurchaseResult.Success, message);
        return (int)result;
    }

    private void SetDialogicVariable(string name, Variant value)
    {
        Node dialogic = GetNodeOrNull<Node>("/root/Dialogic");
        GodotObject variables = dialogic?.Get("VAR").AsGodotObject();
        variables?.Call("set_variable", name, value);
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
        Variant drink = variables.Call("get_variable", DrinkResultVariable, default(Variant), true);
        PurchaseStateReady = faucet.VariantType != Variant.Type.Nil
            && intel.VariantType != Variant.Type.Nil
            && drink.VariantType != Variant.Type.Nil;
    }
}
