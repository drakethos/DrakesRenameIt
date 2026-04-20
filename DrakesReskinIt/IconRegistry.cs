using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace DrakesReskinIt;

/// <summary>
/// Holds all Sprites that can be used as custom icons for item stacks.
/// Other mods (or the ReskinIt mod itself) call <see cref="Register"/> at startup to contribute icons.
/// At runtime, <see cref="DrakesReskinIt.GetDisplayIcon"/> resolves the icon by name from this registry.
/// </summary>
public static class IconRegistry
{
    private static readonly Dictionary<string, Sprite> registry =
        new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    private static ManualLogSource? _log;

    internal static void Init(ManualLogSource log)
    {
        _log = log;
    }

    /// <summary>
    /// Register a sprite under a name that can be stored in <see cref="DrakesReskinIt.DrakeCustomIcon"/>.
    /// Call from your mod's Awake/Start — after this mod initialises but before the player opens inventory.
    /// Overrides an existing entry if the name is already used.
    /// </summary>
    public static void Register(string iconName, Sprite sprite)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            _log?.LogWarning("[IconRegistry] Attempted to register a sprite with an empty name — skipped.");
            return;
        }
        if (sprite == null)
        {
            _log?.LogWarning($"[IconRegistry] Attempted to register null sprite for '{iconName}' — skipped.");
            return;
        }
        registry[iconName] = sprite;
        _log?.LogDebug($"[IconRegistry] Registered icon '{iconName}'.");
    }

    /// <summary>
    /// Register a Texture2D by converting it to a Sprite (full-rect pivot 0.5, 0.5).
    /// Convenience wrapper around <see cref="Register(string, Sprite)"/>.
    /// </summary>
    public static void Register(string iconName, Texture2D texture)
    {
        if (texture == null)
        {
            _log?.LogWarning($"[IconRegistry] Attempted to register null texture for '{iconName}' — skipped.");
            return;
        }
        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));
        Register(iconName, sprite);
    }

    /// <returns>The <see cref="Sprite"/> for <paramref name="iconName"/>, or <c>null</c> if not found.</returns>
    public static Sprite? Get(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;
        return registry.TryGetValue(iconName, out var sprite) ? sprite : null;
    }

    /// <summary>All registered icon names, in no guaranteed order.</summary>
    public static IEnumerable<string> GetAllNames() => registry.Keys;

    /// <summary>All registered (name, sprite) pairs.</summary>
    public static IEnumerable<KeyValuePair<string, Sprite>> GetAll() => registry;

    public static bool Contains(string iconName) =>
        !string.IsNullOrEmpty(iconName) && registry.ContainsKey(iconName);

    public static void Unregister(string iconName)
    {
        if (!string.IsNullOrEmpty(iconName))
            registry.Remove(iconName);
    }
}
