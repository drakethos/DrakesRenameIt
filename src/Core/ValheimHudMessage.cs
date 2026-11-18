using DrakeModsLibs.UI;

namespace DrakeRenameit;

/// <summary>
/// RenameIt's on-screen feedback. Every message now goes to the shared card (<see cref="DrakeMessage"/>) instead of Valheim's
/// yellow centre text. The name is kept so RenameIt's call sites stay as they are; change the look in Libs, not here.
/// </summary>
internal static class ValheimHudMessage
{
    /// <summary>Shows <paramref name="text"/> on the card. <paramref name="type"/> is ignored: the card has one style.</summary>
    internal static void Show(Character? character, MessageHud.MessageType type, string? text)
    {
        if (character == null || string.IsNullOrEmpty(text))
            return;

        DrakeMessage.Show(text!);
    }
}
