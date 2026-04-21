using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DrakeRenameit.AgentDebug;

namespace DrakeRenameit.Patches
{
    /// <summary>
    /// UI-only: in the Crafting UI, the Upgrade tab's left-side list uses plain text labels.
    /// Rewrite those labels to use RenameIt's custom display names when present.
    /// </summary>
    [HarmonyPatch]
    internal static class InventoryGuiUpgradeListRenamePatch
    {
        /// <summary>
        /// Last-known tab from vanilla tab clicks (fallback when Toggle/panel heuristics are ambiguous).
        /// </summary>
        internal static bool UpgradeCraftingTabActive;

        private static bool _loggedMissingTargets;
        private static MethodBase? _resolvedTarget;
        private static int _postfixCount;
        private static string? _lastLoggedHow;

        [HarmonyPrepare]
        private static bool Prepare()
        {
            _resolvedTarget = ResolveTargetMethod();
            if (_resolvedTarget != null)
                return true;

            if (!_loggedMissingTargets)
            {
                _loggedMissingTargets = true;
                UnityEngine.Debug.LogWarning(
                    "[DrakesRenameIt] Upgrade list rename: could not find InventoryGui recipe refresh method to patch; upgrade list names may remain vanilla.");
            }

            return false;
        }

        private static MethodBase? ResolveTargetMethod()
        {
            var t = typeof(InventoryGui);
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var name in new[]
                     {
                         "UpdateRecipeList", "UpdateCraftingPanel", "UpdateCrafting", "UpdateRecipe", "DoCrafting",
                         "UpdateRecipes", "RefreshRecipeList", "UpdateRecipePanel",
                     })
            {
                foreach (var m in t.GetMethods(flags))
                {
                    if (m.Name != name || m.IsStatic || m.ReturnType != typeof(void))
                        continue;
                    if (!IsLikelyRecipeUiRefresh(m.Name))
                        continue;

                    var p = m.GetParameters();
                    if (p.Length == 0 || AcceptableRecipeUiHookParameters(p))
                        return m;
                }
            }

            foreach (var m in t.GetMethods(flags))
            {
                if (m.IsStatic || m.ReturnType != typeof(void))
                    continue;
                var n = m.Name ?? "";
                if (n.IndexOf("Update", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (n.IndexOf("Recipe", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!IsLikelyRecipeUiRefresh(n))
                    continue;

                return m;
            }

            return null;
        }

        static MethodBase TargetMethod()
        {
            return _resolvedTarget!;
        }

        private static bool AcceptableRecipeUiHookParameters(ParameterInfo[] parameters)
        {
            foreach (var pi in parameters)
            {
                var pt = pi.ParameterType;
                if (pt == typeof(Player) || pt == typeof(bool) || pt == typeof(int) || pt == typeof(float) ||
                    pt == typeof(string))
                    continue;
                return false;
            }

            return true;
        }

        private static bool IsLikelyRecipeUiRefresh(string? methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return false;
            var n = methodName;
            if (n.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("Joy", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("Controller", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("Input", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(InventoryGui __instance)
        {
            if (__instance == null || Player.m_localPlayer == null)
                return;

            if (!InventoryGuiCraftingTabState.TryGetUpgradeTabActive(__instance, out var how))
            {
                // #region agent log
                _postfixCount++;
                if (_postfixCount % 45 == 1 && how != _lastLoggedHow)
                {
                    _lastLoggedHow = how;
                    AgentDebugLog.Write("run1", "H1", "UpgradeListNamePatches.cs:Postfix", "skip_not_upgrade_tab", how);
                }
                // #endregion
                return;
            }

            var inv = Player.m_localPlayer.GetInventory();
            if (inv == null)
                return;

            var items = inv.GetAllItems();
            if (items == null || items.Count == 0)
                return;

            var map = new Dictionary<string, string>();
            foreach (var item in items)
            {
                if (item?.m_shared == null)
                    continue;
                if (!DrakeRenameit.hasNewName(item))
                    continue;

                string custom = DrakeRenameit.GetDisplayNameForUi(item, localize: true);
                if (string.IsNullOrEmpty(custom))
                    continue;

                string token = item.m_shared.m_name ?? "";
                if (!string.IsNullOrEmpty(token) && !map.ContainsKey(token))
                    map[token] = custom;

                string localized = Localization.instance != null ? Localization.instance.Localize(token) : token;
                if (!string.IsNullOrEmpty(localized) && !map.ContainsKey(localized))
                    map[localized] = custom;
            }

            if (map.Count == 0)
                return;

            var texts = __instance.GetComponentsInChildren<TMP_Text>(true);
            if (texts == null || texts.Length == 0)
                return;

            int replaced = 0;
            foreach (var t in texts)
            {
                if (t == null)
                    continue;

                if (t.GetComponentInParent<Button>(true) == null)
                    continue;

                var current = t.text ?? "";
                if (string.IsNullOrEmpty(current))
                    continue;

                if (map.TryGetValue(current, out var exact))
                {
                    t.text = exact;
                    replaced++;
                    continue;
                }

                foreach (var kv in map)
                {
                    if (string.IsNullOrEmpty(kv.Key))
                        continue;
                    if (current.Contains(kv.Key))
                    {
                        t.text = current.Replace(kv.Key, kv.Value);
                        replaced++;
                        break;
                    }
                }
            }

            // #region agent log
            _postfixCount++;
            if (_postfixCount % 40 == 0 && replaced > 0)
                AgentDebugLog.Write("run1", "H2", "UpgradeListNamePatches.cs:Postfix", "rename_applied",
                    "how=" + how + ";replaced=" + replaced + ";mapCount=" + map.Count);
            // #endregion
        }
    }

    /// <summary>
    /// Derives Craft vs Upgrade from live UI (toggles / panel roots), not only click events — avoids stale
    /// <see cref="InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive"/> when the UI restores tab state or uses non-click navigation.
    /// </summary>
    internal static class InventoryGuiCraftingTabState
    {
        private static readonly BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo? _fiTabCraft;
        private static FieldInfo? _fiTabUpgrade;
        private static FieldInfo[]? _gameObjectFields;
        private static bool _tabFieldsCached;

        private static void EnsureTabButtons()
        {
            if (_tabFieldsCached)
                return;
            _tabFieldsCached = true;
            var t = typeof(InventoryGui);
            _fiTabCraft = t.GetField("m_tabCraft", Flags);
            _fiTabUpgrade = t.GetField("m_tabUpgrade", Flags);
        }

        private static FieldInfo[] GameObjectFields =>
            _gameObjectFields ??= typeof(InventoryGui).GetFields(Flags);

        /// <summary>Returns true if the Upgrade crafting tab is active; <paramref name="how"/> describes the detection path.</summary>
        internal static bool TryGetUpgradeTabActive(InventoryGui gui, out string how)
        {
            how = "unknown";
            EnsureTabButtons();

            var craftBtn = _fiTabCraft?.GetValue(gui) as Button;
            var upgradeBtn = _fiTabUpgrade?.GetValue(gui) as Button;

            if (craftBtn != null && upgradeBtn != null)
            {
                var tc = craftBtn.GetComponent<Toggle>() ?? craftBtn.GetComponentInChildren<Toggle>(true);
                var tu = upgradeBtn.GetComponent<Toggle>() ?? upgradeBtn.GetComponentInChildren<Toggle>(true);
                if (tc != null && tu != null)
                {
                    if (tu.isOn)
                    {
                        how = "toggle_upgrade_on";
                        InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive = true;
                        return true;
                    }

                    if (tc.isOn)
                    {
                        how = "toggle_craft_on";
                        InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive = false;
                        return false;
                    }
                }
            }

            var upgradePanels = CountActiveGameObjectRoots(gui, IsUpgradeRootFieldName);
            var craftPanels = CountActiveGameObjectRoots(gui, IsCraftRootFieldName);
            if (upgradePanels > 0 && craftPanels == 0)
            {
                how = "panel_upgrade_only_active_count=" + upgradePanels;
                InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive = true;
                return true;
            }

            if (craftPanels > 0 && upgradePanels == 0)
            {
                how = "panel_craft_only_active_count=" + craftPanels;
                InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive = false;
                return false;
            }

            how = "listener_" + (InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive ? "upgrade" : "craft");
            return InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive;
        }

        private static bool IsUpgradeRootFieldName(string fieldName)
        {
            var n = fieldName ?? "";
            if (n.IndexOf("upgrade", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (n.IndexOf("tab", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("panel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("root", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("page", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("area", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // exclude item/slot-ish names
            if (n.IndexOf("item", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("slot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return false;
        }

        private static bool IsCraftRootFieldName(string fieldName)
        {
            var n = fieldName ?? "";
            if (n.IndexOf("upgrade", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (n.IndexOf("craft", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (n.IndexOf("tab", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("panel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("root", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("page", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static int CountActiveGameObjectRoots(InventoryGui gui, Func<string, bool> nameMatch)
        {
            var n = 0;
            foreach (var f in GameObjectFields)
            {
                if (f.FieldType != typeof(GameObject))
                    continue;
                if (!nameMatch(f.Name))
                    continue;
                if (f.GetValue(gui) is not GameObject go)
                    continue;
                if (!go.activeInHierarchy)
                    continue;
                n++;
            }

            return n;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    internal static class InventoryGuiCraftingTabTrackerPatch
    {
        private static int _awakeSyncLogBudget = 8;

        [HarmonyPostfix]
        private static void Postfix(InventoryGui __instance)
        {
            if (__instance == null)
                return;

            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var craftField = typeof(InventoryGui).GetField("m_tabCraft", flags);
                var upgradeField = typeof(InventoryGui).GetField("m_tabUpgrade", flags);
                if (craftField?.GetValue(__instance) is not Button craftTab ||
                    upgradeField?.GetValue(__instance) is not Button upgradeTab)
                    return;

                craftTab.onClick.RemoveListener(OnCraftTabClicked);
                upgradeTab.onClick.RemoveListener(OnUpgradeTabClicked);
                craftTab.onClick.AddListener(OnCraftTabClicked);
                upgradeTab.onClick.AddListener(OnUpgradeTabClicked);

                // InventoryGui does not declare OnEnable/OnDisable in a way Harmony can target on the subclass;
                // sync tab state once here from toggles/panels (recipe refresh postfix also re-resolves each tick).
                InventoryGuiCraftingTabState.TryGetUpgradeTabActive(__instance, out var how);
                // #region agent log
                if (_awakeSyncLogBudget-- > 0)
                    AgentDebugLog.Write("run1", "H3", "UpgradeListNamePatches.cs:Awake", "sync_tab_after_clickwire", how);
                // #endregion
            }
            catch
            {
                /* ignore */
            }
        }

        private static void OnCraftTabClicked()
        {
            InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive = false;
        }

        private static void OnUpgradeTabClicked()
        {
            InventoryGuiUpgradeListRenamePatch.UpgradeCraftingTabActive = true;
        }
    }
}
