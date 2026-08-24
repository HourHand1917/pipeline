using Godot;

/// <summary>
/// 可拖拽的剧情 NPC：玩家进入检测范围后自动开始一次对话。
/// 只触发既有 Dialogic 流程，不触发战斗。
/// </summary>
[GlobalClass]
public partial class ForcedPackagedFriendlyNPC : PackagedFriendlyNPC
{
	private bool _triggeredThisVisit;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (_triggeredThisVisit || !IsPlayerInRange || IsDialogueActive)
			return;

		if (TryStartConfiguredDialogue())
			_triggeredThisVisit = true;
	}
}
