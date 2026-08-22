using Godot;

/// <summary>
/// features/dialogue/npc 专用的可拖拽敌对 NPC。
/// 保留 HostileNPC 的“战前对话结束后进入战斗”完整行为。
/// </summary>
[GlobalClass]
public partial class PackagedHostileNPC : HostileNPC
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
