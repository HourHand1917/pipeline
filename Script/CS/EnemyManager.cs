using Godot;
using Godot.Collections;

[GlobalClass]
public partial class EnemyManager : Node
{
    [Signal] public delegate void EnemySpawnedEventHandler(EnemyBattle enemy);
    [Signal] public delegate void EnemyDiedEventHandler(EnemyBattle enemy);
    [Signal] public delegate void AllEnemiesDefeatedEventHandler();

    public Array<EnemyBattle> Enemies { get; private set; } = new();
    private Array<GodotObject> _enemyDataConfigs;

    // ================================================================
    //  初始化
    // ================================================================

    public void SetEnemyConfigs(Array<GodotObject> configs)
    {
        _enemyDataConfigs = configs;
    }

    public void SpawnAllFromConfigs(GodotObject rules)
        => SpawnWave(rules);

    /// <summary>
    /// Replaces the current wave. Every EnemyData resource is deep duplicated,
    /// preventing shared AI provider/cooldown/cache state between encounters.
    /// </summary>
    public void SpawnWave(GodotObject rules, int playerCellOverride = -1)
    {
        foreach (Node child in GetChildren())
        {
            if (child is EnemyBattle) child.QueueFree();
        }
        Enemies.Clear();

        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        var enemyDataList = battleMap.Get("enemy_data_list").As<Array>();
        var startCells = battleMap.Get("enemy_start_cells").As<Array<int>>();
        int playerStartCell = playerCellOverride > 0
            ? playerCellOverride
            : battleMap.Get(GDScriptKeys.BattleMap.PlayerStartCell).AsInt32();
        int cellCount = battleMap.Get(GDScriptKeys.BattleMap.CellCount).AsInt32();
        var occupied = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < enemyDataList.Count; i++)
        {
            var sourceData = enemyDataList[i].As<Resource>();
            if (sourceData == null) continue;
            // Resources in .tres files are shared by Godot's resource cache.
            // Deep duplication gives each runtime enemy an independent adapter.
            var runtimeResource = sourceData.Duplicate(true) as Resource;
            var enemyData = runtimeResource as GodotObject;
            if (enemyData == null) continue;

            int preferredCell = i < startCells.Count ? startCells[i] : 6;
            int startCell = FindAvailableStartCell(preferredCell, playerStartCell, cellCount, occupied);
            if (startCell <= 0)
            {
                GD.PushError($"EnemyManager: no free start cell for enemy index {i}.");
                continue;
            }
            occupied.Add(startCell);

            var enemy = new EnemyBasicNode();
            enemy.Name = $"Enemy_{i}";
            enemy.LoadFromData(enemyData);
            enemy.SetMapPosition(startCell);
            AddChild(enemy);

            enemy.Died += () => OnEnemyDied(enemy);

            Enemies.Add(enemy);
            EmitSignal(SignalName.EnemySpawned, enemy);
        }
    }

    public void RemoveEnemy(EnemyBattle enemy)
    {
        if (enemy == null) return;
        Enemies.Remove(enemy);
        enemy.QueueFree();
    }

    // ================================================================
    //  查询
    // ================================================================

    public Array<EnemyBattle> GetAliveEnemies()
    {
        var alive = new Array<EnemyBattle>();
        foreach (var e in Enemies)
            if (e != null && e.IsAlive)
                alive.Add(e);
        return alive;
    }

    public bool HasAliveEnemies()
    {
        foreach (var e in Enemies)
            if (e != null && e.IsAlive)
                return true;
        return false;
    }

    public EnemyBattle GetPrimaryEnemy()
    {
        foreach (var e in Enemies)
            if (e != null && e.IsAlive)
                return e;
        return null;
    }

    public EnemyBattle GetClosestAlive(int playerPosition)
    {
        EnemyBattle closest = null;
        int minDist = int.MaxValue;
        foreach (var e in Enemies)
        {
            if (e == null || !e.IsAlive) continue;
            int dist = Mathf.Abs(e.MapPosition - playerPosition);
            if (dist < minDist) { minDist = dist; closest = e; }
        }
        return closest;
    }

    public EnemyBattle GetTargetEnemy(int targetIndex = 0)
    {
        var alive = GetAliveEnemies();
        if (alive.Count == 0) return null;
        return alive[Mathf.Min(targetIndex, alive.Count - 1)];
    }

    public EnemyBattle GetAliveByRole(StringName role)
    {
        foreach (var enemy in Enemies)
            if (enemy != null && enemy.IsAlive && enemy.Role == role)
                return enemy;
        return null;
    }

    public EnemyBattle GetByRole(StringName role)
    {
        foreach (var enemy in Enemies)
            if (enemy != null && enemy.Role == role)
                return enemy;
        return null;
    }

    public bool IsCellOccupiedByEnemy(int cell, EnemyBattle except = null)
    {
        foreach (var enemy in Enemies)
            if (enemy != null && enemy != except && enemy.IsAlive && enemy.MapPosition == cell)
                return true;
        return false;
    }

    // ================================================================
    //  位置管理
    // ================================================================

    public void SetAllPositions(int position)
    {
        foreach (var e in Enemies)
            if (e != null) e.SetMapPosition(position);
    }

    // ================================================================
    //  内部
    // ================================================================

    private void OnEnemyDied(EnemyBattle enemy)
    {
        EmitSignal(SignalName.EnemyDied, enemy);
        if (!HasAliveEnemies())
            EmitSignal(SignalName.AllEnemiesDefeated);
    }

    private static int FindAvailableStartCell(
        int preferred,
        int playerCell,
        int cellCount,
        System.Collections.Generic.HashSet<int> occupied)
    {
        bool Free(int cell) => cell >= 1 && cell <= cellCount
            && cell != playerCell && !occupied.Contains(cell);

        if (Free(preferred)) return preferred;
        for (int radius = 1; radius <= cellCount; radius++)
        {
            if (Free(preferred - radius)) return preferred - radius;
            if (Free(preferred + radius)) return preferred + radius;
        }
        return -1;
    }
}
