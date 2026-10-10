using DrakeModsLibs.UI.Toolkit;
using System;
using UnityEngine;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The shortcut that opens Quick Settings (config <c>QuickSettingsKey</c>, default <c>Shift+S</c>: modifiers held, then the last key
/// pressed). It does nothing while you are looking at an item or a stand, so it never fights an item's own actions, and pressing it
/// again closes the window. When the last key is a movement key (W A S D) it also waits until you are standing still, so backpedalling
/// at a run (Shift+S) doesn't pop the menu open. <see cref="BeginCapture"/> lets the menu rebind it with a keypress.
/// </summary>
internal static class QuickSettingsShortcut
{
    /// <summary>Walking pace is about 2 m/s; slower than this counts as standing still.</summary>
    const float StillSpeed = 1f;

    static Action<string?>? _captureCallback;

    /// <summary>True while the menu is waiting for the player to press the new shortcut.</summary>
    public static bool Capturing { get; private set; }

    /// <summary>Call once per frame.</summary>
    public static void Poll()
    {
        if (Capturing)
        {
            TryCapture();
            return;
        }

        var binding = RenameitConfig.QuickSettingsKey;
        if (string.IsNullOrWhiteSpace(binding) || !Pressed(binding, out var key))
            return;

        if (QuickSettingsPanel.IsOpen)
        {
            QuickSettingsPanel.Close();
            return;
        }

        // The item window has the same settings under its cog; chat and console own the keyboard while they are open.
        if (WorkshopPanel.IsOpen || LookingAtItem() || TypingElsewhere())
            return;
        if (IsMovementKey(key) && MovingNow())
            return;

        QuickSettingsPanel.Open();
    }

    // ---------------------------------------------------------------- rebinding from the menu

    /// <summary>
    /// Waits for the next key press and stores it as the shortcut (with whichever of Shift / Ctrl / Alt is held).
    /// <paramref name="status"/> gets a message to show while waiting or when a press is refused, and null when finished or cancelled.
    /// </summary>
    public static void BeginCapture(Action<string?> status)
    {
        Capturing = true;
        _captureCallback = status;
        status("Press the new shortcut. Esc cancels.");
    }

    public static void CancelCapture() => Finish();

    static void Finish()
    {
        if (!Capturing)
            return;
        Capturing = false;
        var callback = _captureCallback;
        _captureCallback = null;
        callback?.Invoke(null);
    }

    static void TryCapture()
    {
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            // Keyboard only: mouse buttons and joystick keys sit above Mouse0.
            if (key == KeyCode.None || key >= KeyCode.Mouse0 || !UnityEngine.Input.GetKeyDown(key))
                continue;
            if (key == KeyCode.Escape)
            {
                Finish();
                return;
            }

            if (IsModifier(key))
                continue;

            var anyModifier = Held(KeyCode.LeftShift, KeyCode.RightShift) || Held(KeyCode.LeftControl, KeyCode.RightControl) ||
                              Held(KeyCode.LeftAlt, KeyCode.RightAlt);
            var functionKey = key >= KeyCode.F1 && key <= KeyCode.F15;
            if (!anyModifier && !functionKey)
            {
                // A bare letter would fire while you play; insist on a modifier.
                _captureCallback?.Invoke("Hold Shift, Ctrl or Alt too, so it can't trigger while you play.");
                return;
            }

            var chord = (Held(KeyCode.LeftShift, KeyCode.RightShift) ? "Shift+" : "") +
                        (Held(KeyCode.LeftControl, KeyCode.RightControl) ? "Ctrl+" : "") +
                        (Held(KeyCode.LeftAlt, KeyCode.RightAlt) ? "Alt+" : "") + key;
            RenameitConfig.QuickSettingsKey = chord;
            Finish();
            return;
        }
    }

    static bool Held(KeyCode a, KeyCode b) => UnityEngine.Input.GetKey(a) || UnityEngine.Input.GetKey(b);

    static bool IsModifier(KeyCode key) =>
        key == KeyCode.LeftShift || key == KeyCode.RightShift || key == KeyCode.LeftControl || key == KeyCode.RightControl ||
        key == KeyCode.LeftAlt || key == KeyCode.RightAlt || key == KeyCode.LeftCommand || key == KeyCode.RightCommand ||
        key == KeyCode.LeftWindows || key == KeyCode.RightWindows || key == KeyCode.CapsLock;

    // ---------------------------------------------------------------- the check

    /// <summary>"Shift+S": every token before the last must be held, and the last key must go down this frame.</summary>
    static bool Pressed(string binding, out KeyCode key)
    {
        key = KeyCode.None;
        var tokens = binding.Split(new[] { '+', ',', '&', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return false;

        var last = tokens[tokens.Length - 1].Trim();
        if (!Enum.TryParse(last, true, out key) || !UnityEngine.Input.GetKeyDown(key))
            return false;

        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (!DrakeModsLibs.Input.MenuKeyBinding.IsHeld(tokens[i].Trim()))
                return false;
        }

        return true;
    }

    static bool IsMovementKey(KeyCode key) =>
        key == KeyCode.W || key == KeyCode.A || key == KeyCode.S || key == KeyCode.D || key == KeyCode.Space;

    /// <summary>The player is walking or running (sideways speed above walking pace).</summary>
    static bool MovingNow()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return false;
        var velocity = player.GetVelocity();
        return new Vector2(velocity.x, velocity.z).magnitude > StillSpeed;
    }

    /// <summary>An item on the ground or on a stand is under the crosshair.</summary>
    static bool LookingAtItem()
    {
        var player = Player.m_localPlayer;
        if (player == null)
            return false;
        var hovered = player.GetHoverObject();
        return hovered != null && (hovered.GetComponentInParent<ItemDrop>() != null || hovered.GetComponentInParent<ItemStand>() != null);
    }

    static bool TypingElsewhere() =>
        (Chat.instance != null && Chat.instance.HasFocus()) || Console.IsVisible();
}
