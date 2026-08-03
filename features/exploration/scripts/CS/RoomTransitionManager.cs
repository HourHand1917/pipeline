using Godot;
using System.Collections.Generic;

/// <summary>
/// 房间过渡。只管 Tween 淡入淡出，边界由 ExplorationManager 处理。
/// 每个 Room 节点通过 Add Metadata 覆盖默认边界：
///   PlayerLeft, PlayerRight, CamLeft, CamRight（int 类型）
/// </summary>
[GlobalClass]
public partial class RoomTransitionManager : Node2D
{
    [Export] public string StartRoomId { get; set; } = "Room_Front";
    [Export] public float Duration { get; set; } = 1.0f;

    private Node2D _mapLayer;
    private Node2D _currentRoom;

    public override void _Ready()
    {
        _mapLayer = GetParent()?.GetNodeOrNull<Node2D>("MapLayer");
        if (_mapLayer == null) return;

        foreach (Node child in _mapLayer.GetChildren())
        {
            if (child.Name.ToString().StartsWith("Room_") && child is Node2D room)
            {
                SetRoomAlpha(room, 0f);
                SetRoomActive(room, false);
            }
        }

        var start = _mapLayer.GetNodeOrNull<Node2D>(StartRoomId);
        if (start != null)
        {
            SetRoomAlpha(start, 1f);
            SetRoomActive(start, true);
            _currentRoom = start;
            ApplyRoomBounds(start);
        }
    }

    public void TransitionTo(string roomId)
    {
        var target = _mapLayer?.GetNodeOrNull<Node2D>(roomId);
        if (target == null || target == _currentRoom) return;

        // 旧房间立即关闭互动，新房间立即激活
        if (_currentRoom != null) SetRoomActive(_currentRoom, false);
        SetRoomActive(target, true);

        var tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        if (_currentRoom != null) TweenRoom(_currentRoom, 0f, tween);
        TweenRoom(target, 1f, tween);
        _currentRoom = target;
        ApplyRoomBounds(target);
    }

    /// <summary>切换房间内所有 Area2D 的监测状态。关闭后玩家无法互动。</summary>
    private static void SetRoomActive(Node2D room, bool active)
    {
        foreach (var area in room.FindChildren("*", "Area2D", true, false))
            ((Area2D)area).Monitoring = active;
    }

    private void ApplyRoomBounds(Node2D room)
    {
        var mgr = GetParent<ExplorationManager>();
        var bounds = room?.GetNodeOrNull<RoomBounds>("Bounds");
        bounds?.ApplyTo(mgr);
    }

    private void TweenRoom(Node2D room, float a, Tween t)
    {
        foreach (var ci in CanvasItems(room))
        {
            var c = ci.Modulate;
            t.TweenProperty(ci, "modulate", new Color(c.R, c.G, c.B, a), Duration);
        }
    }

    private static void SetRoomAlpha(Node2D room, float a)
    {
        foreach (var ci in CanvasItems(room))
        { var c = ci.Modulate; ci.Modulate = new Color(c.R, c.G, c.B, a); }
    }

    private static List<CanvasItem> CanvasItems(Node n)
    {
        var l = new List<CanvasItem>();
        Collect(n, l);
        return l;
    }

    private static void Collect(Node n, List<CanvasItem> l)
    {
        if (n is CanvasItem ci) l.Add(ci);
        foreach (Node c in n.GetChildren()) Collect(c, l);
    }
}
