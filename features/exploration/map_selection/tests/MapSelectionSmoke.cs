using Godot;
using System;

/// <summary>Resource, validation and public UI API smoke test.</summary>
public partial class MapSelectionSmoke : Node
{
    private int _checks;

    public override void _Ready()
    {
        CallDeferred(MethodName.Run);
    }

    private void Run()
    {
        try
        {
            const string configPath =
                "res://features/exploration/map_selection/resources/default_home_destinations.tres";
            const string uiPath =
                "res://features/exploration/map_selection/scenes/map_select_ui.tscn";

            MapSelectionConfig config = ResourceLoader.Load<MapSelectionConfig>(configPath);
            Check(config != null, "default map selection config loads");
            Check(config.Destinations.Count == 3, "default config contains F2/F3/F4");

            string[] expectedMapIds = { "f2_1", "f3_0", "f4" };
            for (int index = 0; index < config.Destinations.Count; index++)
            {
                MapDestinationData destination = config.Destinations[index];
                Check(destination.MapId.ToString() == expectedMapIds[index],
                    $"destination {index} keeps its expected map id");
                Check(MapSelectionValidator.Validate(
                        destination,
                        config.MapRegistryPath,
                        out string reason),
                    $"{destination.Title} validates its map and spawn: {reason}");
            }

            PackedScene packedUi = ResourceLoader.Load<PackedScene>(uiPath);
            Check(packedUi != null, "standalone map selector scene loads");
            MapSelectUI ui = packedUi.Instantiate<MapSelectUI>();
            AddChild(ui);

            Check(ui.DestinationButtonCount == 3, "UI builds three destination rows");
            Check(ui.MouseFilter == Control.MouseFilterEnum.Stop, "root blocks mouse input behind the modal");

            int closedCount = 0;
            ui.Closed += () => closedCount++;
            ui.Open();
            Check(ui.IsOpen && ui.Visible, "Open public API reveals the modal");
            Check(ui.SelectedIndex == 0, "first available destination is selected once");
            ui.Close();
            Check(!ui.IsOpen && !ui.Visible, "Close public API hides the modal");
            Check(closedCount == 1, "Closed signal emits exactly once");
            ui.Close();
            Check(closedCount == 1, "duplicate close cannot emit twice");

            GD.Print($"MAP_SELECTION_SMOKE_PASS checks={_checks} destinations=3");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MAP_SELECTION_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
