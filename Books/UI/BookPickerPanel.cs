using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// "Place a book": opens when the hammer's Book piece is selected. Lists the books in your inventory plus a new blank one; the
/// pick is remembered in <see cref="BookPlaceState"/> and the placement preview follows.
/// </summary>
internal sealed class BookPickerPanel
{
    static BookPickerPanel? _instance;
    readonly UkWindow _window;

    public static bool IsOpen => _instance != null && _instance._window.IsOpen;

    internal static void CloseWindow() => _instance?._window.CloseSilent();

    public static void Show(Player player)
    {
        _instance ??= new BookPickerPanel();
        _instance.Open(player);
    }

    BookPickerPanel()
    {
        _window = new UkWindow("drakes_book_picker", 420f);
        _window.SetHeader("Place a book", "Pick one, then aim and click");
    }

    void Open(Player player)
    {
        var body = _window.Body;
        body.Clear();

        var any = false;
        foreach (var item in player.GetInventory().GetAllItems())
        {
            if (!BookItem.IsBook(item))
                continue;
            any = true;
            var size = BookData.GetSize(item);
            var pages = BookData.GetPages(item).Count;
            body.Add(Row(BookData.DisplayTitle(item), $"{size}, {pages} page{(pages == 1 ? "" : "s")}", BookData.GetColor(item), true,
                () => Pick(() => BookPlaceState.Choose(item))));
        }

        if (!any)
            body.Add(UkControls.Text("No books in your inventory.", 14, UkTheme.TextMuted));

        var canAfford = BookPlaceState.CanAffordBlank(player);
        var cost = new List<string>();
        foreach (var (prefab, _, amount) in BookPlaceState.Cost())
            cost.Add($"{amount} {prefab.Replace("Drakes_PieceOfPaper", "Paper")}");
        body.Add(Row("New blank book", string.Join(", ", cost) + (canAfford ? "" : " (not enough)"), BookData.CoverColors[0], canAfford,
            () => Pick(BookPlaceState.ChooseBlank), dashed: true));

        _window.ShowFooter(false);
        _window.Open();
    }

    void Pick(System.Action choose)
    {
        choose();
        BookPlacementPatches.KeepPick(3f);
        _window.CloseSilent();
    }

    static VisualElement Row(string title, string detail, Color cover, bool enabled, System.Action onClick, bool dashed = false)
    {
        var row = new VisualElement().Row();
        row.Pad(8, 10);
        row.Margin(bottom: 6);
        row.Fill(UkTheme.Raised).Round(UkTheme.Radius).Border(UkTheme.Line, 1);
        var swatch = new VisualElement().Size(width: 16, height: 26).Fill(cover).Border(Color.black, 1);
        swatch.style.marginRight = 12;
        row.Add(swatch);
        var text = new VisualElement().Column().Grow();
        text.Add(UkControls.Text(title, 16, enabled ? UkTheme.Text : UkTheme.TextMuted, wrap: false));
        text.Add(UkControls.Text(detail, 13, UkTheme.TextMuted, wrap: false));
        row.Add(text);
        var pick = UkControls.MakeButton("Choose", onClick);
        UkControls.SetButtonEnabled(pick, enabled);
        row.Add(pick);
        return row;
    }
}
