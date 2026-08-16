using Godot;

/// <summary>
/// 可直接配置 Dialogic Timeline 的 NPC 交互物。
/// 保留 InteractableBase 的靠近检测和鼠标点击，并补充可配置的互动键。
/// 对话内容、条件、分支和选项全部交由 Dialogic 处理。
/// </summary>
[GlobalClass]
public partial class NPCinteractables : InteractableBase
{
    private bool _startPending;

    [Export(PropertyHint.ResourceType, "DialogicTimeline")]
    public Resource Timeline { get; set; }

    [Export]
    public StringName InteractionAction { get; set; } = "interact";

    [Export]
    public NodePath BubbleAnchorPath { get; set; } = new("BubbleAnchor");

    public override void _Ready()
    {
        base._Ready();
        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsPlayerInRange || Timeline == null || InteractionAction.IsEmpty)
            return;

        if (@event is InputEventKey { Echo: true })
            return;

        if (@event.IsActionPressed(InteractionAction))
        {
            HandleInteract();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void HandleInteract()
    {
        if (!IsPlayerInRange)
            return;

        if (Timeline == null)
        {
            GD.PushWarning($"NPCinteractables '{Name}' 尚未配置 Dialogic Timeline。");
            return;
        }

        var dialogic = GetNodeOrNull<Node>("/root/Dialogic");
        if (dialogic == null)
        {
            GD.PushError("找不到 Dialogic 自动加载。请确认 Dialogic 插件已启用。");
            return;
        }

        Variant currentTimeline = dialogic.Get("current_timeline");
        if (_startPending ||
            (currentTimeline.VariantType != Variant.Type.Nil && currentTimeline.AsGodotObject() != null))
            return;

        var canvas = GetTree().GetFirstNodeInGroup("npc_dialogue_canvas");
        if (canvas == null)
        {
            GD.PushError("场景内缺少 NPCDialogueCanvas；请将气泡 Canvas 场景拖入当前场景。");
            return;
        }

        Node2D anchor = GetNodeOrNull<Node2D>(BubbleAnchorPath) ?? this;
        canvas.Call("set_dialogue_anchor", anchor);

        // 延迟到当前输入事件结束，避免启动对话的鼠标点击同时跳过第一句。
        _startPending = true;
        dialogic.CallDeferred("start_timeline", Timeline);
        GetTree().CreateTimer(0.25).Timeout += () => _startPending = false;
    }
}
