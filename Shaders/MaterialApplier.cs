using Godot;

/// <summary>
/// 组件：挂在父节点下，自动给父节点及所有子孙 CanvasItem 套材质。
/// 不占用父节点的脚本槽位。监听 child_entered_tree，后续新增的节点也会自动套。
/// </summary>
[GlobalClass]
public partial class MaterialApplier : Node
{
    [Export] private ShaderMaterial _material;

    public override void _Ready()
    {
        if (_material == null) return;

        var root = GetParent();
        ApplyRecursive(root);
        root.ChildEnteredTree += OnChildAdded;
    }

    private void OnChildAdded(Node child)
    {
        ApplyRecursive(child);
        child.ChildEnteredTree += OnChildAdded;
    }

    private void ApplyRecursive(Node node)
    {
        if (node is CanvasItem ci)
            ci.Material = _material;

        foreach (Node c in node.GetChildren())
        {
            ApplyRecursive(c);
            c.ChildEnteredTree += OnChildAdded;
        }
    }
}
