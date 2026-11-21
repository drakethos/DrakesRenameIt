using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace DrakeConflictProbe
{
    /// <summary>
    /// TEST ONLY. A deliberately badly-behaved mod that overlaps DrakesRenameItLegacy's patches, so the ModGuard
    /// compat report and runtime error attribution can be checked against a known culprit:
    ///  1. a bool prefix on ItemStand.UseItem  (reported as "can skip the original"; lets every call through)
    ///  2. a no-op transpiler on ItemDrop.GetHoverName (reported as a rewrite)
    ///  3. a postfix on ItemStand.GetHoverText that throws while a stand holds an item (max 3 times)
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class ConflictProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "com.drakemods.conflictprobe";
        public const string Name = "DrakeConflictProbe";
        public const string Version = "0.0.1";

        internal static ConfigEntry<bool> SkipPrefix = null!;
        internal static ConfigEntry<bool> Transpiler = null!;
        internal static ConfigEntry<bool> ThrowOnHover = null!;
        internal static ConfigEntry<int> MaxThrows = null!;
        internal static int Thrown;

        private void Awake()
        {
            SkipPrefix = Config.Bind("Probe", "SkipStylePrefixOnUseItem", true, "Bool prefix on ItemStand.UseItem (always lets the call through).");
            Transpiler = Config.Bind("Probe", "NoOpTranspilerOnGetHoverName", true, "No-op transpiler on ItemDrop.GetHoverName.");
            ThrowOnHover = Config.Bind("Probe", "ThrowOnStandHover", true, "Postfix on ItemStand.GetHoverText throws while the stand holds an item.");
            MaxThrows = Config.Bind("Probe", "MaxThrows", 3, "Stop throwing after this many times (keeps the log readable).");

            var h = new Harmony(Guid);
            if (SkipPrefix.Value)
                h.Patch(AccessTools.Method(typeof(ItemStand), nameof(ItemStand.UseItem)),
                    prefix: new HarmonyMethod(typeof(ConflictProbePlugin), nameof(UseItemPrefix)));
            if (Transpiler.Value)
                h.Patch(AccessTools.Method(typeof(ItemDrop), nameof(ItemDrop.GetHoverName)),
                    transpiler: new HarmonyMethod(typeof(ConflictProbePlugin), nameof(NoOpTranspiler)));
            if (ThrowOnHover.Value)
                h.Patch(AccessTools.Method(typeof(ItemStand), nameof(ItemStand.GetHoverText)),
                    postfix: new HarmonyMethod(typeof(ConflictProbePlugin), nameof(HoverPostfix)));
            Logger.LogInfo($"[ConflictProbe] TEST MOD active: skipPrefix={SkipPrefix.Value} transpiler={Transpiler.Value} throwOnHover={ThrowOnHover.Value}");
        }

        private static bool UseItemPrefix() => true;

        private static IEnumerable<CodeInstruction> NoOpTranspiler(IEnumerable<CodeInstruction> instructions) => instructions;

        private static void HoverPostfix(ItemStand __instance)
        {
            // Our postfix runs before RenameIt's (it is Priority.Last), so key off "something is mounted", not the name.
            if (Thrown >= MaxThrows.Value || __instance == null || !__instance.HaveAttachment())
                return;
            Thrown++;
            throw new InvalidOperationException($"ConflictProbe: deliberate failure #{Thrown} in ItemStand.GetHoverText postfix");
        }
    }
}
