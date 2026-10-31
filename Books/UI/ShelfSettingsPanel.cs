using DrakeModsLibs.UI.Toolkit;
using UnityEngine;

namespace DrakeRenameit.Books;

/// <summary>Shift+Use on a shelf: whether this shelf may hold things other than books. Owner only, and only when the server allows it.</summary>
internal sealed class ShelfSettingsPanel
{
    static ShelfSettingsPanel? _instance;
    readonly UkWindow _window;
    readonly UkSwitch _nonBooks;
    readonly UnityEngine.UIElements.Label _note;
    BookShelfBehaviour? _shelf;

    public static void Show(BookShelfBehaviour shelf, Player player)
    {
        _instance ??= new ShelfSettingsPanel();
        _instance.Open(shelf, player);
    }

    ShelfSettingsPanel()
    {
        _window = new UkWindow("drakes_shelf_settings", 420f);
        _window.SetHeader("Shelf", "Settings");
        _nonBooks = new UkSwitch("Allow things other than books", "Knick-knacks stand on the shelf too");
        _nonBooks.Changed = on => _shelf?.SetNonBooks(on);
        _window.Body.Add(_nonBooks.Root);
        _note = UkControls.Text("", 14, UkTheme.TextMuted);
        _note.style.marginTop = 8;
        _window.Body.Add(_note);
    }

    void Open(BookShelfBehaviour shelf, Player player)
    {
        _shelf = shelf;
        var piece = shelf.GetComponent<Piece>();
        var owner = piece != null && piece.IsCreator();
        var serverAllows = RenameitConfig.ShelfAllowNonBooks;
        _nonBooks.SetValue(shelf.AllowsNonBooks());
        _nonBooks.Root.SetEnabled(owner && serverAllows);
        _note.text = !serverAllows
            ? "This server only allows books on shelves."
            : owner ? "Things already on the shelf stay if you turn this off; no new ones fit." : "Only whoever built this shelf can change this.";
        _window.ShowFooter(false);
        _window.Open();
    }
}
