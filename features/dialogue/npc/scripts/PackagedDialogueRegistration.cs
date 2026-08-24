using Godot;

/// <summary>
/// 只在当前运行实例中把随 NPC 封装的角色交给 Dialogic。
/// 不调用 ProjectSettings.Save()，因此不会改写 project.godot。
/// </summary>
internal static class PackagedDialogueRegistration
{
    private const string CharacterDirectorySetting = "dialogic/directories/dch_directory";

    internal static void Register(StringName identifier, Resource character)
    {
        string key = identifier.ToString();
        if (string.IsNullOrWhiteSpace(key) || character == null ||
            string.IsNullOrWhiteSpace(character.ResourcePath))
            return;

        Variant current = ProjectSettings.GetSetting(CharacterDirectorySetting);
        Godot.Collections.Dictionary directory = current.VariantType == Variant.Type.Dictionary
            ? current.AsGodotDictionary()
            : new Godot.Collections.Dictionary();

        directory[key] = character.ResourcePath;
        ProjectSettings.SetSetting(CharacterDirectorySetting, directory);
    }

    internal static void RegisterExtra(
        string[] identifiers,
        Godot.Collections.Array<Resource> characters)
    {
        int count = Mathf.Min(identifiers.Length, characters.Count);
        for (int i = 0; i < count; i++)
            Register(new StringName(identifiers[i]), characters[i]);
    }

    /// <summary>
    /// Register the characters already dragged into NPCBase by their resource file name.
    /// Dialogic timeline identifiers in this project intentionally match those names
    /// (RUBBER.dch -> RUBBER, reb.dch -> reb, 鼠鼠.dch -> 鼠鼠).
    /// This keeps every packaged NPC self-contained without saving project.godot.
    /// </summary>
    internal static void RegisterConfiguredCharacters(NPCBase npc)
    {
        if (npc == null)
            return;

        RegisterByResourceFileName(npc.DialogueCharacter);
        RegisterByResourceFileName(npc.PlayerDialogueCharacter);
        if (npc.AdditionalPlayerDialogueCharacters == null)
            return;

        foreach (Resource character in npc.AdditionalPlayerDialogueCharacters)
            RegisterByResourceFileName(character);
    }

    internal static void RegisterByResourceFileName(Resource character)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.ResourcePath))
            return;

        string normalizedPath = character.ResourcePath.Replace('\\', '/');
        int slashIndex = normalizedPath.LastIndexOf('/');
        string fileName = slashIndex >= 0
            ? normalizedPath[(slashIndex + 1)..]
            : normalizedPath;
        int extensionIndex = fileName.LastIndexOf('.');
        string identifier = extensionIndex > 0
            ? fileName[..extensionIndex]
            : fileName;
        Register(new StringName(identifier), character);
    }
}
