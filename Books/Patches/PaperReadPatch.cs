using DrakeRenameit.Paper.Items;
using HarmonyLib;

namespace DrakeRenameit.Books;

/// <summary>Use on a Written or Printed Page opens it to read (client option PaperUseReads). Place in the reader runs the old Use.</summary>
[HarmonyPatch]
internal static class PaperReadPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    static bool UseItem_Prefix(Humanoid __instance, ItemDrop.ItemData item)
    {
        if (!RenameitConfig.PaperUseReads || PaperReaderPanel.PlacingNow || __instance != Player.m_localPlayer || item == null)
            return true;
        if (!PaperItem.IsWrittenLike(item))
            return true;
        PaperReaderPanel.Show(item);
        return false;
    }
}
