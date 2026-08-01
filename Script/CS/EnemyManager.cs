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
        if (_enemyDataConfigs == null || _enemyDataConfigs.Count == 0)
        {
            var enemyData = rules?.Get(GDScriptKeys.GameRules.EnemyData).As<GodotObject>();
            if (enemyData != null)
                SpawnEnemy(enemyData);
            return;
        }

        foreach (var config in _enemyDataConfigs)
            SpawnEnemy(config);
    }

    public EnemyBattle SpawnEnemy(GodotObject enemyData)
    {
        if (enemyData == null) return null;

        var enemy = new EnemyBattle();
        enemy.Name = enemyData.Get(GDScriptKeys.CharacterData.Id).AsString();
        enemy.LoadFromData(enemyData);
        AddChild(enemy);

        enemy.Died += () => OnEnemyDied(enemy);

        Enemies.Add(enemy);
        EmitSignal(SignalName.EnemySpawned, enemy);
        return enemy;
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

    // ================================================================
    //  敌方回合 — 行为选择
    // ================================================================

    public GodotObject GetActionForDistance(int distance)
    {
        var enemy = GetPrimaryEnemy();
        if (enemy == null) return null;

        var enemyData = enemy.GetEnemyData();
        if (enemyData == null) return null;

        return enemyData.Call("get_action_for_distance", distance).As<GodotObject>();
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