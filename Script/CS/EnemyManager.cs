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
    {
        foreach (Node child in GetChildren())
        {
            if (child is EnemyBattle) child.QueueFree();
        }
        Enemies.Clear();

        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        var enemyDataList = battleMap.Get("enemy_data_list").As<Array>();
        var startCells = battleMap.Get("enemy_start_cells").As<Array<int>>();

        for (int i = 0; i < enemyDataList.Count; i++)
        {
            var enemyData = enemyDataList[i].As<GodotObject>();
            if (enemyData == null) continue;

            int startCell = i < startCells.Count ? startCells[i] : 6;

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
}