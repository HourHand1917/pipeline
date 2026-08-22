using Godot;
using System;

/// <summary>
/// Read-only validation for destination resources. Invalid map ids or spawn ids are
/// shown as unavailable instead of failing only after the player confirms travel.
/// </summary>
public static class MapSelectionValidator
{
    public static bool Validate(
        MapDestinationData destination,
        string registryPath,
        out string reason)
    {
        if (destination == null)
        {
            reason = "目的地资源为空";
            return false;
        }

        if (destination.MapId.IsEmpty)
        {
            reason = "未配置地图 ID";
            return false;
        }

        if (destination.SpawnId.IsEmpty)
        {
            reason = "未配置出生点 ID";
            return false;
        }

        Resource registry = ResourceLoader.Load<Resource>(registryPath);
        if (registry == null)
        {
            reason = $"地图注册表不存在：{registryPath}";
            return false;
        }

        string scenePath = FindScenePath(registry, destination.MapId);
        if (string.IsNullOrEmpty(scenePath))
        {
            reason = $"地图 ID 未注册：{destination.MapId}";
            return false;
        }

        PackedScene packedScene = ResourceLoader.Load<PackedScene>(scenePath);
        if (packedScene == null)
        {
            reason = $"地图场景无法加载：{scenePath}";
            return false;
        }

        Node sceneRoot = null;
        try
        {
            sceneRoot = packedScene.Instantiate();
            if (!ContainsSpawn(sceneRoot, destination.SpawnId))
            {
                reason = $"地图 {destination.MapId} 中没有出生点 {destination.SpawnId}";
                return false;
            }
        }
        catch (Exception exception)
        {
            reason = $"验证地图失败：{exception.Message}";
            return false;
        }
        finally
        {
            sceneRoot?.Free();
        }

        reason = "";
        return true;
    }

    private static string FindScenePath(Resource registry, StringName mapId)
    {
        var maps = registry.Get("maps").AsGodotArray<GodotObject>();
        foreach (GodotObject map in maps)
        {
            if (map != null && map.Get("id").AsStringName() == mapId)
                return map.Get("scene_path").AsString();
        }

        return "";
    }

    private static bool ContainsSpawn(Node node, StringName spawnId)
    {
        if (node is SpawnPoint spawnPoint && spawnPoint.SpawnId == spawnId)
            return true;

        foreach (Node child in node.GetChildren())
        {
            if (ContainsSpawn(child, spawnId))
                return true;
        }

        return false;
    }
}
