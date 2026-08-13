using Godot;
using System.Collections.Generic;

/// <summary>
/// 组件：挂在父节点下，自动给父节点及所有子孙 CanvasItem 套材质。
/// 监听 child_entered_tree，后续新增的节点也会自动套。
/// </summary>
[GlobalClass]
public partial class MaterialApplier : Node
{
    [Export] private ShaderMaterial _material;

    private readonly HashSet<Node> _connected = new();

    public override void _Ready()
    {
        if (_material == null) return;

        var root = GetParent();
        ApplyRecursive(root);
        ConnectSignal(root);
    }

    private void OnChildAdded(Node child)
    {
        ApplyRecursive(child);
        ConnectSignal(child);
    }

    private void ApplyRecursive(Node node)
    {
        if (node is CanvasItem ci)
            ci.Material = _material;

        foreach (Node c in node.GetChildren())
            ApplyRecursive(c);
    }

    private void ConnectSignal(Node node)
    {
        if (_connected.Add(node))
            node.ChildEnteredTree += OnChildAdded;

        foreach (Node c in node.GetChildren())
            ConnectSignal(c);
    }
}
