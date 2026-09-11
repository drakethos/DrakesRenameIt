using BepInEx.Logging;

namespace DrakeRenameit;

/// <summary>Retired hammer spike — real flow is <see cref="PaperPlace"/>.</summary>
internal static class PaperNoticePiece
{
    internal static void Register(ManualLogSource log)
    {
        log.LogInfo("[Paper] Hammer PaperNotice spike retired; use hotbar PaperPlace.");
    }
}
