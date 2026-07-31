using Godot;
using Godot.Collections;

/// <summary>
/// 宝箱：带内容的可持久化交互物。
/// 展示子类如何 override SaveState/LoadState 扩展状态字段。
/// </summary>
public partial class ChestInteractable : InteractableBase
{
    /// <summary>宝箱里还有多少物品</summary>
    [Export] public int ItemsRemaining { get; set; } = 3;
    /// <summary>是否已经打开过</summary>
    public bool IsOpened { get; private set; }

    protected override void SetupPlaceholder()
    {
        sprite.Modulate = new Color(0.2f, 0.45f, 0.8f);
    }

    public override void HandleInteract()
    {
        if (IsOpened)
        {
            if (ItemsRemaining > 0)
            {
                ItemsRemaining--;
                GD.Print($"从「{DisplayName}」取走 1 件物品，剩余 {ItemsRemaining}。");
                PersistInteraction(_mapId);
            }
            else
            {
                GD.Print($"「{DisplayName}」已空。");
            }
        }
        else
        {
            IsOpened = true;
            GD.Print($"打开「{DisplayName}」！可拿走 {ItemsRemaining} 件物品。");
            PersistInteraction(_mapId);
        }
    }

    public override Dictionary SaveState()
    {
        return new Dictionary
        {
            { "opened", IsOpened },
            { "items_remaining", ItemsRemaining }
        };
    }

    public override void LoadState(Dictionary state)
    {
        if (state.TryGetValue("opened", out var v))
            IsOpened = v.AsBool();
        if (state.TryGetValue("items_remaining", out var r))
            ItemsRemaining = r.AsInt32();

        if (IsOpened && ItemsRemaining <= 0)
        {
            sprite.Modulate = new Color(0.1f, 0.2f, 0.4f); // 空宝箱更暗
        }
    }
}
