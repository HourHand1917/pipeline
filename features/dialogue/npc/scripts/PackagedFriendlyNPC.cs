using Godot;

/// <summary>
/// features/dialogue/npc 专用的可拖拽友好 NPC。
/// 先进行内存角色注册，再加载默认 Timeline，避免要求修改 project.godot。
/// </summary>
[GlobalClass]
public partial class PackagedFriendlyNPC : FriendlyNPC
{
	[ExportGroup("封装对话内容")]
	[Export(PropertyHint.File, "*.dtl")]
	public string DefaultTimelinePath { get; set; } = "";

	[Export]
	public StringName DialogicCharacterIdentifier { get; set; } = "";

	[Export]
	public string[] ExtraDialogicCharacterIdentifiers { get; set; } = System.Array.Empty<string>();

	[Export]
	public Godot.Collections.Array<Resource> ExtraDialogicCharacters { get; set; } = new();

	public override void _Ready()
	{
		PackagedDialogueRegistration.RegisterConfiguredCharacters(this);
		PackagedDialogueRegistration.Register(DialogicCharacterIdentifier, DialogueCharacter);
		PackagedDialogueRegistration.RegisterExtra(
			ExtraDialogicCharacterIdentifiers,
			ExtraDialogicCharacters);

		if (DialogueTimeline == null && !string.IsNullOrWhiteSpace(DefaultTimelinePath))
			DialogueTimeline = GD.Load<Resource>(DefaultTimelinePath);

		base._Ready();
	}
}
