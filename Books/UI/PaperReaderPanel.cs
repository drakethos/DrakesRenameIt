using System;
using System.Reflection;
using DrakeModsLibs.API;
using DrakeModsLibs.Forge;
using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// Use on a Written or Printed Page reads it: the same paper look as the book, one page wide. Edit opens the page's usual
/// rename/write menu; Place starts the placing the page always had. Turn it off with the client option PaperUseReads.
/// </summary>
internal sealed class PaperReaderPanel
{
    static PaperReaderPanel? _instance;

    /// <summary>Set while Place runs the game's own Use, so the read patch lets it through.</summary>
    internal static bool PlacingNow;

    readonly UkWindow _window;
    readonly VisualElement _page;
    readonly Label _text;
    readonly Button _edit;
    readonly Button _place;
    ItemDrop.ItemData? _item;

    public static bool IsOpen => _instance != null && _instance._window.IsOpen;

    internal static void CloseWindow() => _instance?._window.CloseSilent();

    public static void Show(ItemDrop.ItemData item)
    {
        _instance ??= new PaperReaderPanel();
        _instance.Open(item);
    }

    PaperReaderPanel()
    {
        _window = new UkWindow("drakes_paper_reader", 860f);
        var texture = ForgeTextures.Load(typeof(PaperReaderPanel).Assembly, "textures/paper_sheet.png");
        _page = BookReaderPanel.Page(texture);
        _page.style.minHeight = 560;
        _page.Round(4).Border(new Color(0.29f, 0.21f, 0.13f), 3);
        _text = UkControls.Text("", 25, new Color(0.23f, 0.16f, 0.10f));
        _text.style.whiteSpace = WhiteSpace.Normal;
        _text.style.unityTextAlign = TextAnchor.UpperLeft;
        UkFonts.ApplyBody(_text);
        _page.Add(_text);
        _window.Body.Add(_page);

        _edit = UkControls.MakeButton("Edit", OnEdit);
        _place = UkControls.MakeButton("Place", OnPlace);
        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(_place);
        _window.Footer.Add(_edit);
        _window.ShowFooter(true);
    }

    void Open(ItemDrop.ItemData item)
    {
        _item = item;
        var title = CustomizeLibsAPI.GetDisplayNameForUi(item, localize: true);
        _window.SetHeader(string.IsNullOrWhiteSpace(title) ? "Page" : title, "Page");
        var body = CustomizeLibsAPI.GetProperDescription(item);
        _text.text = string.IsNullOrWhiteSpace(body) ? "(Nothing is written on this page.)" : body;
        _edit.Show(DrakeRenameit.AnyInventoryActionAvailable(item));
        _window.Open();
    }

    void OnEdit()
    {
        var item = _item;
        _window.CloseSilent();
        if (item != null && DrakeRenameit.IsItemInLocalPlayerInventory(item))
            UIPanels.OpenActionMenu(item);
    }

    static readonly MethodInfo? UseItem = AccessTools.Method(typeof(Humanoid), "UseItem");

    void OnPlace()
    {
        var item = _item;
        var player = Player.m_localPlayer;
        _window.CloseSilent();
        if (item == null || player == null || UseItem == null || !DrakeRenameit.IsItemInLocalPlayerInventory(item))
            return;
        var args = Array.ConvertAll(UseItem.GetParameters(), p =>
            p.ParameterType == typeof(Inventory) ? player.GetInventory()
            : p.ParameterType == typeof(ItemDrop.ItemData) ? item
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null);
        PlacingNow = true;
        try
        {
            UseItem.Invoke(player, args);
        }
        finally
        {
            PlacingNow = false;
        }
    }
}
