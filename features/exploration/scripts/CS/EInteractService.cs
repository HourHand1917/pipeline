using Godot;
using System.Collections.Generic;

/// <summary>
/// E 键交互服务（Autoload）。探索地图中按 E 代替鼠标左键，
/// 与玩家范围内最近的交互物互动（宝箱、工作台、商店、大门、NPC 等）。
/// 多个交互物同时在范围内时自动选最近的一个。
/// 移动锁定（对话/教程/界面打开）期间不响应，避免误触。
/// </summary>
[GlobalClass]
public partial class EInteractService : Node
{
    public static EInteractService Instance { get; private set; }

    private static readonly List<InteractableBase> _registry = new();

    /// <summary>交互物在 _Ready 注册自己，_ExitTree 注销（InteractableBase 调用）。</summary>
    public static void Register(InteractableBase interactable)
    {
        if (interactable != null && !_registry.Contains(interactable))
            _registry.Add(interactable);
    }

    public static void Unregister(InteractableBase interactable) => _registry.Remove(interactable);

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("EInteractService: 重复实例化"); return; }
        Instance = this;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key || key.Keycode != Key.E)
            return;

        var target = FindNearestInRange();
        if (target == null)
            return;

        target.HandleInteract();
        GetViewport().SetInputAsHandled();
    }

    private static InteractableBase FindNearestInRange()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || !player.MovementEnabled)
            return null;

        InteractableBase best = null;
        float bestDistance = float.MaxValue;
        foreach (InteractableBase interactable in _registry)
        {
            if (interactable == null || !GodotObject.IsInstanceValid(interactable)
                || !interactable.IsPlayerInRange)
                continue;

            float distance = interactable.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = interactable;
            }
        }
        return best;
    }
}
