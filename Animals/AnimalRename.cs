using System;
using System.Collections;
using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.API;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace DrakeRenameit.Animals;

/// <summary>
/// Names for tamed animals. Vanilla stores the name on the animal (ZDO <c>tamedName</c>), allows 10 characters, shows it in white
/// and lets anyone change it. This keeps vanilla's storage (so nothing is lost if the mod goes away) and adds: the rename
/// window, a bigger limit, colours that survive into the label, who may rename (Anyone / Namer / Admin) and a per-animal lock.
/// The owner of the animal applies the limit and colour policy again, so a modified client cannot get around them.
/// </summary>
internal static class AnimalRename
{
    internal const string ZdoNamerKey = "DrakeAnimal_Namer";
    internal const string ZdoLockedKey = "DrakeAnimal_Locked";
    const string RpcMeta = "DrakeAnimal_RPC_Meta";
    static readonly int NamerHash = ZdoNamerKey.GetStableHashCode();
    static readonly int LockedHash = ZdoLockedKey.GetStableHashCode();
    static readonly int TamedNameHash = "TamedName".GetStableHashCode();
    const int MaxRawLength = 512;

    // ---------------------------------------------------------------- reading

    static ZDO? Zdo(Tameable t) => t.GetComponent<ZNetView>()?.GetZDO();

    /// <summary>The name as stored (rich text tags included); empty when the animal still has its species name.</summary>
    internal static string GetCustomName(Tameable t) => Zdo(t)?.GetString(TamedNameHash, "") ?? "";

    internal static long GetNamer(Tameable t) => Zdo(t)?.GetLong(NamerHash, 0L) ?? 0L;

    internal static bool IsLocked(Tameable t) => Zdo(t)?.GetBool(LockedHash, false) ?? false;

    /// <summary>The species name ("Wolf"), localized.</summary>
    internal static string DefaultName(Tameable t)
    {
        var character = t.GetComponent<Character>();
        var name = character != null ? character.m_name : "";
        return Localization.instance != null ? Localization.instance.Localize(name) : name;
    }

    /// <summary>The tamed animal behind a hovered object, when it can be renamed here.</summary>
    internal static Tameable? NameableAnimal(GameObject? hovered)
    {
        if (hovered == null || !RenameitConfig.AnimalRenameEnabled)
            return null;
        var tameable = hovered.GetComponentInParent<Tameable>();
        return tameable != null && tameable.IsTamed() ? tameable : null;
    }

    // ---------------------------------------------------------------- permission

    internal static bool CanRename(Tameable t, Player? player, bool flash)
    {
        if (player == null || t == null || !RenameitConfig.AnimalRenameEnabled || !t.IsTamed())
            return false;
        if (RenameitPermission.IsElevatedForOverrides(player))
            return true;
        var access = RenameitConfig.AnimalRenameAccess;
        if (string.Equals(access, "Admin", StringComparison.OrdinalIgnoreCase))
            return Deny(player, flash, "Only admins can rename animals here.");
        var namer = GetNamer(t);
        var me = player.GetPlayerID();
        if (namer != 0L && namer != me && (IsLocked(t) || string.Equals(access, "Namer", StringComparison.OrdinalIgnoreCase)))
            return Deny(player, flash, "Only the player who named this animal can change its name.");
        return true;
    }

    /// <summary>The lock switch: the player who named it (or nobody yet), or an admin.</summary>
    internal static bool CanChangeLock(Tameable t, Player? player)
    {
        if (player == null || !RenameitConfig.AnimalAllowLock)
            return false;
        if (RenameitPermission.IsElevatedForOverrides(player))
            return true;
        var namer = GetNamer(t);
        return namer == 0L || namer == player.GetPlayerID();
    }

    static bool Deny(Player player, bool flash, string message)
    {
        if (flash)
            ValheimHudMessage.Show(player, MessageHud.MessageType.Center, message);
        return false;
    }

    // ---------------------------------------------------------------- the name policy

    /// <summary>The limit and the colour switch applied to a name: tags dropped when colours are off, then cut to the visible-character limit.</summary>
    internal static string Sanitize(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0)
            return "";
        if (!RenameitConfig.AnimalAllowColour)
            text = RichModel.FromRaw(text).Plain;
        return RichText.VisibleLength(text) <= RenameitConfig.AnimalNameCharLimit
            ? (text.Length > MaxRawLength ? RichText.Truncate(text, RenameitConfig.AnimalNameCharLimit, MaxRawLength) : text)
            : RichText.Truncate(text, RenameitConfig.AnimalNameCharLimit, MaxRawLength);
    }

    // ---------------------------------------------------------------- writing

    /// <summary>
    /// Save a name (empty = back to the species name) and optionally the lock. The name goes through vanilla's own SetName RPC
    /// (to the owner, never taking ownership). The first player to name an animal is remembered as its namer.
    /// </summary>
    internal static void SetName(Tameable t, string? name, bool? locked = null)
    {
        var nview = t.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        var value = Sanitize(name);
        t.SetText(value);
        var player = Player.m_localPlayer;
        if (player == null)
            return;
        var namer = GetNamer(t);
        var claim = value.Length == 0 ? -1L : namer != 0L ? namer : player.GetPlayerID();
        var lockState = value.Length == 0 ? 0 : locked.HasValue ? (locked.Value ? 1 : 0) : -1;
        SendMeta(nview, claim, lockState);
    }

    internal static void SetLocked(Tameable t, bool on)
    {
        var nview = t.GetComponent<ZNetView>();
        var player = Player.m_localPlayer;
        if (nview == null || !nview.IsValid() || player == null)
            return;
        var namer = GetNamer(t);
        SendMeta(nview, namer != 0L ? namer : player.GetPlayerID(), on ? 1 : 0);
    }

    static void SendMeta(ZNetView nview, long namer, int lockState)
    {
        if (nview.IsOwner())
            ApplyMeta(nview, namer, lockState);
        else
            nview.InvokeRPC(RpcMeta, namer, lockState);
    }

    /// <summary>namer: -1 clears namer and lock, 0 leaves it, else the player id. lockState: -1 unchanged, 0 off, 1 on.</summary>
    static void ApplyMeta(ZNetView nview, long namer, int lockState)
    {
        var zdo = nview.GetZDO();
        if (zdo == null)
            return;
        if (namer < 0L)
        {
            zdo.Set(NamerHash, 0L);
            zdo.Set(LockedHash, false);
            return;
        }

        var current = zdo.GetLong(NamerHash, 0L);
        // Only the namer already on record (or the first one) may change what is stored.
        if (current != 0L && namer != 0L && namer != current)
            return;
        if (namer != 0L)
            zdo.Set(NamerHash, namer);
        if (lockState >= 0 && RenameitConfig.AnimalAllowLock)
            zdo.Set(LockedHash, lockState == 1);
    }

    internal static void RegisterRpcs(Tameable t)
    {
        var nview = t.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid())
            return;
        nview.Unregister(RpcMeta);
        nview.Register<long, int>(RpcMeta, (sender, namer, lockState) =>
        {
            if (nview.IsValid() && nview.IsOwner())
                ApplyMeta(nview, namer, lockState);
        });
    }

    // ---------------------------------------------------------------- window

    internal static void OpenEditor(Tameable t)
    {
        UI.Toolkit.AnimalWorkshopPanel.Show(t);
    }
}

[HarmonyPatch]
internal static class AnimalRenamePatches
{
    [HarmonyPatch(typeof(Tameable), "Awake")]
    [HarmonyPostfix]
    static void TameableAwake(Tameable __instance)
    {
        try
        {
            AnimalRename.RegisterRpcs(__instance);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[AnimalRename] RPC registration failed: {ex.Message}");
        }
    }

    /// <summary>Shift+Use on a tamed animal: our window instead of vanilla's 10-character box.</summary>
    [HarmonyPatch(typeof(Tameable), "SetName")]
    [HarmonyPrefix]
    static bool SetNamePrefix(Tameable __instance)
    {
        if (!RenameitConfig.AnimalRenameEnabled || !__instance.IsTamed() || !RenameitConfig.UseToolkitUi)
            return true;
        if (!AnimalRename.CanRename(__instance, Player.m_localPlayer, flash: true))
            return false;
        try
        {
            AnimalRename.OpenEditor(__instance);
            return false;
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[AnimalRename] Window failed, using vanilla's box: {ex.Message}");
            return true;
        }
    }

    /// <summary>The owner of the animal applies the limit and colour policy, whatever client sent the name.</summary>
    [HarmonyPatch(typeof(Tameable), "RPC_SetName")]
    [HarmonyPrefix]
    static void SetNameRpcPrefix(ref string name)
    {
        if (RenameitConfig.AnimalRenameEnabled)
            name = AnimalRename.Sanitize(name);
    }

    /// <summary>Vanilla's hover starts with the species ("Boar ( Tame, Hungry )"); show the animal's own name there instead, coloured.</summary>
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    [HarmonyPostfix]
    static void HoverTextPostfix(Tameable __instance, ref string __result)
    {
        if (!RenameitConfig.AnimalRenameEnabled || string.IsNullOrEmpty(__result) || !__instance.IsTamed())
            return;
        var raw = AnimalRename.GetCustomName(__instance);
        if (raw.Length == 0)
            return;
        var species = AnimalRename.DefaultName(__instance);
        if (species.Length > 0 && __result.StartsWith(species, StringComparison.Ordinal))
            __result = AnimalRename.Sanitize(raw) + __result.Substring(species.Length);
    }

    /// <summary>
    /// Vanilla strips every tag from the name here, and this is what the name label above the animal and the "follows you"
    /// messages show. Keep the colours (when allowed).
    /// </summary>
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverName))]
    [HarmonyPostfix]
    static void HoverNamePostfix(Tameable __instance, ref string __result)
    {
        if (!RenameitConfig.AnimalRenameEnabled || !__instance.IsTamed())
            return;
        var raw = AnimalRename.GetCustomName(__instance);
        if (raw.Length == 0)
            return;
        __result = AnimalRename.Sanitize(raw);
    }
}

/// <summary>The name label above tamed animals (EnemyHud): size from the player's AnimalLabelStyle / AnimalLabelScale.</summary>
[HarmonyPatch]
internal static class AnimalLabelPatches
{
    static readonly Dictionary<int, float> BaseSizes = new();
    static System.Reflection.FieldInfo? _huds;
    static System.Reflection.FieldInfo? _hudName;

    [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
    [HarmonyPostfix]
    static void UpdateHudsPostfix(EnemyHud __instance)
    {
        if (_failed)
            return;
        try
        {
            _huds ??= AccessTools.Field(typeof(EnemyHud), "m_huds");
            if (_huds?.GetValue(__instance) is not IDictionary huds)
                return;
            var improved = RenameitConfig.AnimalRenameEnabled
                           && string.Equals(RenameitConfig.AnimalLabelStyle, "Improved", StringComparison.OrdinalIgnoreCase);
            var scale = improved ? RenameitConfig.AnimalLabelScale : 1f;
            foreach (DictionaryEntry entry in huds)
            {
                var data = entry.Value;
                if (data == null)
                    continue;
                _hudName ??= AccessTools.Field(data.GetType(), "m_name");
                var label = _hudName?.GetValue(data) as TMP_Text;
                if (label == null)
                    continue;
                var tamed = entry.Key is Character character && character.IsTamed();
                var id = label.GetInstanceID();
                if (!BaseSizes.TryGetValue(id, out var baseSize))
                    BaseSizes[id] = baseSize = label.fontSize;
                var wanted = tamed ? baseSize * scale : baseSize;
                if (!Mathf.Approximately(label.fontSize, wanted))
                    label.fontSize = wanted;
            }
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[AnimalLabel] Label patch disabled after an error: {ex.Message}");
            _failed = true;
        }
    }

    static bool _failed;
}
