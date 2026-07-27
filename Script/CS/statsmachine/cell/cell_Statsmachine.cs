using Godot;

public partial class StateMachine : Node
{
    // 当前激活的状态
    public State CurrentState { get; private set; }

    // 用字典存储所有状态，方便按名称查找
    private System.Collections.Generic.Dictionary<string, State> _states = new();

    public override void _Ready()
    {
        // 收集所有子节点中的 State 脚本
        foreach (Node child in GetChildren())
        {
            if (child is State state)
            {
                _states[child.Name.ToString().ToLower()] = state;
            }
        }

        // 默认进入第一个状态（通常是 Idle）
        if (_states.Count > 0)
        {
            var firstKey = new System.Collections.Generic.List<string>(_states.Keys)[0];
            TransitionTo(firstKey);
        }
    }

    /// <summary>
    /// 切换到指定状态
    /// </summary>
    public void TransitionTo(string stateName)
    {
        stateName = stateName.ToLower();

        if (!_states.ContainsKey(stateName))
        {
            GD.PrintErr($"State '{stateName}' not found!");
            return;
        }

        State newState = _states[stateName];

        // 先退出当前状态
        CurrentState?.Exit();

        // 再进入新状态
        CurrentState = newState;
        CurrentState.Enter();
    }

    public override void _Process(double delta)
    {
        CurrentState?.Update(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        CurrentState?.PhysicsUpdate(delta);
    }

    public override void _Input(InputEvent @event)
    {
        CurrentState?.HandleInput(@event);
    }
}