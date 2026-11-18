using DrakeModsLibs.UI.Toolkit;
using DrakeModsLibs.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// Quick settings on their own, no item needed: opened by the shortcut (<see cref="QuickSettingsShortcut"/>). Same controls as the
/// Workshop's cog menu (<see cref="QuickSettingsContent"/>). Esc, the close button or the shortcut again closes it.
/// </summary>
internal sealed class QuickSettingsPanel
{
    static QuickSettingsPanel? _instance;

    readonly UkScreen _screen;
    readonly VisualElement _scrim;
    readonly QuickSettingsContent _settings;
    bool _open;

    public static bool IsOpen => _instance != null && _instance._open;

    public static void Open()
    {
        _instance ??= new QuickSettingsPanel();
        _instance.Show();
    }

    public static void Close() => _instance?.Hide();

    /// <summary>Esc closes it. True when it handled the key.</summary>
    public static bool TryHandleEscape()
    {
        if (!IsOpen)
            return false;
        Close();
        return true;
    }

    QuickSettingsPanel()
    {
        _screen = UkScreen.Create("drakes_quick_settings");

        _scrim = new VisualElement().Fill(UkTheme.Scrim);
        _scrim.style.position = Position.Absolute;
        _scrim.style.left = _scrim.style.right = _scrim.style.top = _scrim.style.bottom = 0;
        _scrim.style.alignItems = Align.Center;
        _scrim.style.justifyContent = Justify.Center;
        _scrim.pickingMode = PickingMode.Position; // a menu: clicks stay inside it
        _scrim.Show(false);
        _screen.Root.Add(_scrim);

        var card = new VisualElement().Column().Fill(UkTheme.Panel).Round(UkTheme.RadiusLarge).Border(UkTheme.Line).Pad(18, 22);
        card.style.width = 480;
        _scrim.Add(card);

        var header = new VisualElement().Row();
        header.style.marginBottom = 14;
        header.Add(UkControls.Text("Quick settings", 24, UkTheme.Text, heading: true, wrap: false));
        header.Add(UkControls.Spacer());
        var badge = UkControls.Text("This device only", 12, UkTheme.TextMuted, wrap: false);
        badge.style.marginRight = 12;
        header.Add(badge);
        header.Add(UkControls.MakeIconButton("✕", Hide, "Close (Esc)", UkButtonKind.Secondary));
        card.Add(header);

        _settings = new QuickSettingsContent(withHeader: false);
        card.Add(_settings.Root);
    }

    void Show()
    {
        _screen.RaiseAboveCanvases();
        _settings.Sync();
        _scrim.Show(true);
        _open = true;
        DrakeGuiInput.EnsureBlocked();
    }

    void Hide()
    {
        if (!_open)
            return;
        _open = false;
        _scrim.Show(false);
        DrakeGuiInput.EnsureUnblocked();
    }
}
