using Godot;

[GlobalClass]
public partial class ItemPanel : PanelContainer
{
    [Signal] public delegate void ItemUsedEventHandler(int index);
    [Signal] public delegate void ItemDiscardedEventHandler(int index);

    [Export] private GridContainer itemGrid;
    [Export] private Button useItemButton;
    [Export] private Button discardItemButton;
    [Export] private Label itemDescLabel;
    [Export] private Label bottleCapLabel;
    [Export] private Label faucetLabel;

    private TextureButton[] itemSlots = new TextureButton[4];
    private int selectedIndex = -1;

    public override void _Ready()
    {
        for (int i = 0; i < 4; i++)
        {
            var child = itemGrid.GetChild(i);
            if (child is TextureButton btn)
            {
                int index = i;
                btn.Pressed += () => SelectItem(index);
                itemSlots[i] = btn;
            }
        }

        useItemButton.Pressed += () =>
        {
            if (selectedIndex >= 0)
            {
                EmitSignal(SignalName.ItemUsed, selectedIndex);
                selectedIndex = -1;
                Refresh();
            }
        };

        discardItemButton.Pressed += () =>
        {
            if (selectedIndex >= 0)
            {
                EmitSignal(SignalName.ItemDiscarded, selectedIndex);
                selectedIndex = -1;
                Refresh();
            }
        };
    }

    public void Refresh()
    {
        for (int i = 0; i < 4; i++)
        {
            var item = DataManager.Instance.GetItem(i);
            if (item != null)
            {
                var obj = (GodotObject)item;
                itemSlots[i].TextureNormal = obj.Get("icon").As<Texture2D>();
                itemSlots[i].Disabled = false;
            }
            else
            {
                itemSlots[i].TextureNormal = null;
                itemSlots[i].Disabled = true;
            }
        }

        bool hasSelection = selectedIndex >= 0 && DataManager.Instance.GetItem(selectedIndex) != null;
        useItemButton.Disabled = !hasSelection;
        discardItemButton.Disabled = !hasSelection;

        if (hasSelection)
        {
            var item = DataManager.Instance.GetItem(selectedIndex);
            itemDescLabel.Text = ((GodotObject)item).Get("description").AsString();
        }
        else
        {
            itemDescLabel.Text = "";
        }

        bottleCapLabel.Text = DataManager.Instance.BottleCap.ToString();
        faucetLabel.Text = DataManager.Instance.Faucet.ToString();
    }

    private void SelectItem(int index)
    {
        if (DataManager.Instance.GetItem(index) == null) return;
        selectedIndex = index;
        Refresh();
    }
}