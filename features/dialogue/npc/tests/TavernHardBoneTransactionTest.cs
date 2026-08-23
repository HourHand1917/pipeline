using Godot;
using System;
using System.Collections.Generic;

/// <summary>Runtime coverage for the tavern's atomic hard-bone transaction.</summary>
public partial class TavernHardBoneTransactionTest : Node
{
    private int _checks;

    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        DataManager data = DataManager.Instance;
        var originalItems = new List<Resource>();
        int originalCaps = data?.BottleCap ?? 0;

        try
        {
            Check(data != null, "DataManager autoload is available");
            foreach (Resource item in data.ItemBag)
                originalItems.Add(item);
            ClearItems(data);
            SetBottleCaps(data, 0);

            PackedScene scene = ResourceLoader.Load<PackedScene>(
                "res://features/dialogue/npc/scenes/tavern_owner_npc.tscn");
            TavernOwnerNPC npc = scene.Instantiate<TavernOwnerNPC>();
            AddChild(npc);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Check(npc.HardBoneItem != null
                && npc.HardBoneItem.Get("id").AsStringName() == "hard_bone",
                "tavern scene directly carries the hard-bone item resource");
            Check(npc.HardBoneItem.Get("shop_price").AsInt32() == 16,
                "hard-bone price is exactly 16 bottle caps");
            var effects = npc.HardBoneItem.Get("effects").AsGodotArray<GodotObject>();
            Check(effects.Count == 2, "hard-bone carries exactly heal and temporary-strength effects");
            Check(effects[0].Get("type").AsInt32() == 2
                && effects[0].Get("amount").AsInt32() == 4,
                "hard-bone restores exactly four HP");
            Check(effects[1].Get("type").AsInt32() == 6
                && effects[1].Get("buff_stacks").AsInt32() == 4,
                "hard-bone grants exactly four temporary-strength stacks for this turn");

            int result = npc.TryPurchaseHardBone();
            Check(result == (int)TavernOwnerNPC.DrinkPurchaseResult.InsufficientBottleCaps,
                "insufficient caps rejects the purchase");
            Check(data.BottleCap == 0 && data.ItemBag.Count == 0,
                "failed purchase changes neither caps nor bag");

            SetBottleCaps(data, 20);
            for (int index = 0; index < DataManager.MaxItemSlots; index++)
                Check(data.AddItem(npc.HardBoneItem), "test can fill each item slot");
            result = npc.TryPurchaseHardBone();
            Check(result == (int)TavernOwnerNPC.DrinkPurchaseResult.ItemBagFull,
                "full bag rejects the purchase");
            Check(data.BottleCap == 20 && data.ItemBag.Count == DataManager.MaxItemSlots,
                "full-bag rejection does not consume caps or overwrite items");

            ClearItems(data);
            SetBottleCaps(data, 16);
            result = npc.TryPurchaseHardBone();
            Check(result == (int)TavernOwnerNPC.DrinkPurchaseResult.Success,
                "valid purchase succeeds");
            Check(data.BottleCap == 0 && data.ItemBag.Count == 1,
                "successful purchase atomically consumes 16 caps and one bag slot");
            Check(data.ItemBag[0].Get("id").AsStringName() == "hard_bone",
                "the delivered item is hard-bone, not a placeholder");

            npc.QueueFree();
            GD.Print($"TAVERN_HARD_BONE_TRANSACTION_TEST_PASS checks={_checks} price=16 slots=1");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"TAVERN_HARD_BONE_TRANSACTION_TEST_FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            if (data != null)
            {
                ClearItems(data);
                foreach (Resource item in originalItems)
                    data.AddItem(item);
                SetBottleCaps(data, originalCaps);
            }
        }
    }

    private static void ClearItems(DataManager data)
    {
        while (data.ItemBag.Count > 0)
            data.DiscardItem(data.ItemBag.Count - 1);
    }

    private static void SetBottleCaps(DataManager data, int target)
    {
        data.ModifyCurrency(DataManager.CurrencyType.BottleCap, target - data.BottleCap);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
