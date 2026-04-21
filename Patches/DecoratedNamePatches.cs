using HarmonyLib;
using System;
using System.Reflection;

namespace DrakeRenameit.Patches
{
    /// <summary>
    /// UI-only: try to ensure places that use an ItemData "decorated name" helper (notably the upgrade-items list)
    /// show Drake RenameIt custom names when present.
    /// </summary>
    /// <remarks>
    /// Valheim has changed the exact helper method across versions (some builds have <c>GetDecoratedName</c>, others don't).
    /// This patch binds by reflection at runtime so missing methods do not break the build or the whole mod.
    /// </remarks>
    [HarmonyPatch]
    internal static class ItemDataDecoratedNameLikePatch
    {
        private static bool _loggedMissing;

        static MethodBase? TargetMethod()
        {
            var t = typeof(ItemDrop.ItemData);
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Prefer known names when present.
            foreach (var preferred in new[] { "GetDecoratedName", "GetDecoratedNameWithQuality", "GetName" })
            {
                var m = AccessTools.DeclaredMethod(t, preferred)
                        ?? AccessTools.Method(t, preferred);
                if (m != null && m.ReturnType == typeof(string) && !m.IsStatic)
                    return m;
            }

            // Fallback: pick any instance string-returning method that looks like a name builder.
            foreach (var m in t.GetMethods(flags))
            {
                if (m.IsStatic || m.ReturnType != typeof(string))
                    continue;
                var n = m.Name ?? "";
                if (n.IndexOf("Decor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0)
                    return m;
            }

            if (!_loggedMissing)
            {
                _loggedMissing = true;
                // Intentionally no hard error: missing name helper should not disable the whole mod.
                UnityEngine.Debug.LogWarning(
                    "[DrakesRenameIt] Upgrade list rename: no suitable ItemData decorated-name method found; upgrade tab names may remain vanilla.");
            }

            return null;
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDrop.ItemData __instance, ref string __result)
        {
            var item = __instance;
            if (item?.m_shared == null || string.IsNullOrEmpty(__result))
                return;
            if (!DrakeRenameit.hasNewName(item))
                return;

            // Whatever method we patched is already returning a display string; swap only the base-name fragment.
            string originalName = Localization.instance != null
                ? Localization.instance.Localize(item.m_shared.m_name)
                : item.m_shared.m_name;
            if (string.IsNullOrEmpty(originalName))
                return;

            string customName = DrakeRenameit.GetDisplayNameForUi(item, localize: true);
            if (string.IsNullOrEmpty(customName))
                return;

            if (__result.Contains(originalName))
            {
                __result = __result.Replace(originalName, customName);
                return;
            }

            // Fallback: some callers may use the raw token rather than localized.
            if (!string.IsNullOrEmpty(item.m_shared.m_name) && __result.Contains(item.m_shared.m_name))
                __result = __result.Replace(item.m_shared.m_name, customName);
        }
    }
}

