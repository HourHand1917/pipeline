using Godot;
using Godot.Collections;

/// <summary>
/// 存档管理器（Autoload）。负责收集/分发各系统状态，读写 user://save.json。
/// 自动保存由 ExplorationManager 在玩家回到「家」时触发；主菜单「加载游戏」调用 Load()。
/// </summary>
[GlobalClass]
public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    private const string SavePath = "user://save.json";
    private const string SaveVersion = "1.0.0";

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("SaveManager 重复实例化"); return; }
        Instance = this;
    }

    public bool HasSave() => FileAccess.FileExists(SavePath);

    // ================================================================
    //  存档 / 读档
    // ================================================================

    public void Save()
    {
        var data = new Dictionary
        {
            { "version", SaveVersion },
            { "game_state", SnapshotsToJson(GameState.Instance != null ? GameState.Instance.DumpAll() : new Dictionary<string, Dictionary<string, Dictionary>>()) },
            { "player", DataManager.Instance != null ? DataManager.Instance.SaveState() : new Dictionary() },
            { "growth", GrowthManager.Instance != null ? GrowthManager.Instance.SaveState() : new Array<string>() },
            { "map_id", MapManager.Instance != null ? MapManager.Instance.CurrentMapId.ToString() : "" },
            { "spawn_id", MapManager.Instance != null ? MapManager.Instance.CurrentSpawnId.ToString() : "" },
        };

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"自动保存失败：无法写入 {SavePath}");
            return;
        }
        file.StoreString(Json.Stringify(data));
        file.Close();
        GD.Print("已自动保存。");
    }

    /// <summary>读档并恢复各系统状态，然后切到存档所在地图。返回是否成功。</summary>
    public bool Load()
    {
        if (!HasSave()) return false;

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return false;
        string text = file.GetAsText();
        file.Close();

        var parsed = Json.ParseString(text);
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushError("存档损坏：不是合法的 JSON 对象。");
            return false;
        }
        var data = parsed.AsGodotDictionary();

        if (!data.ContainsKey("version"))
        {
            GD.PushError("存档缺少版本号。");
            return false;
        }

        GameState.Instance?.LoadAll(SnapshotsFromJson(data["game_state"]));
        DataManager.Instance?.LoadState(data["player"].AsGodotDictionary());
        GrowthManager.Instance?.LoadState(data["growth"].AsStringArray());

        string mapId = data.TryGetValue("map_id", out var m) ? m.AsString() : "";
        string spawnId = data.TryGetValue("spawn_id", out var s) ? s.AsString() : "";
        if (!string.IsNullOrEmpty(mapId))
            MapManager.Instance.TravelTo(mapId, spawnId);

        return true;
    }

    /// <summary>删除存档。</summary>
    public void DeleteSave()
    {
        if (HasSave())
            DirAccess.RemoveAbsolute(SavePath);
    }

    /// <summary>新游戏：删档并重置各系统到初始状态。</summary>
    public void NewGame()
    {
        DeleteSave();
        DataManager.Instance?.Reset();
        GameState.Instance?.Reset();
        GrowthManager.Instance?.Reset();
    }

    // ================================================================
    //  类型转换（JSON 不保真泛型 Dictionary，手动互转）
    // ================================================================

    private static Dictionary SnapshotsToJson(Dictionary<string, Dictionary<string, Dictionary>> snapshots)
    {
        var outer = new Dictionary();
        foreach (var (mapId, objects) in snapshots)
        {
            var inner = new Dictionary();
            foreach (var (objId, state) in objects)
                inner[objId] = state;
            outer[mapId] = inner;
        }
        return outer;
    }

    private static Dictionary<string, Dictionary<string, Dictionary>> SnapshotsFromJson(Variant snapshots)
    {
        var result = new Dictionary<string, Dictionary<string, Dictionary>>();
        if (snapshots.VariantType != Variant.Type.Dictionary) return result;

        var outer = snapshots.AsGodotDictionary();
        foreach (var mapKey in outer.Keys)
        {
            string mapId = mapKey.AsString();
            var objects = outer[mapId].AsGodotDictionary();
            var inner = new Dictionary<string, Dictionary>();
            foreach (var objKey in objects.Keys)
            {
                string objId = objKey.AsString();
                inner[objId] = objects[objId].AsGodotDictionary();
            }
            result[mapId] = inner;
        }
        return result;
    }
}
