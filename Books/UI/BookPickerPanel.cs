using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// "Place a book": opens when the hammer's Book piece is selected (or clicked with nothing picked). Two choices: a new blank book
/// (paid from the recipe, and it stays picked while you can afford more), or one of the books you carry, chosen from a drop-down
/// whose list scrolls, so a long inventory never makes a long window. The pick is kept in <see cref="BookPlaceState"/> and the placement preview follows.
/// </summary>
internal sealed class BookPickerPanel
{
    static BookPickerPanel? _instance;
    readonly UkWindow _window;
    readonly List<ItemDrop.ItemData> _books = new();
    int _selected;
    Label? _choiceText;
    VisualElement? _choiceSwatch;
    VisualElement? _list;
    VisualElement? _listContent;

    public static bool IsOpen => _instance != null && _instance._window.IsOpen;

    internal static void CloseWindow() => _instance?._window.CloseSilent();

    public static void Show(Player player)
    {
        _instance ??= new BookPickerPanel();
        _instance.Open(player);
    }

    internal static VisualElement? TestCard => _instance != null && _instance._window.IsOpen ? _instance._window.Card : null;

    /// <summary>Tests: how many books the dropdown offers, and picking entry <paramref name="index"/> as the Place button does.</summary>
    internal static int TestCount => _instance?._books.Count ?? 0;

    internal static bool TestPick(int index)
    {
        if (_instance == null || index < 0 || index >= _instance._books.Count)
            return false;
        _instance.Select(index);
        var item = _instance._books[index];
        _instance.Pick(() => BookPlaceState.Choose(item));
        return true;
    }

    internal static void TestToggleList() => _instance?.ToggleList();

    internal static bool TestPickNew()
    {
        if (_instance == null || Player.m_localPlayer is not { } player || !BookPlaceState.CanAffordBlank(player))
            return false;
        _instance.Pick(BookPlaceState.ChooseBlank);
        return true;
    }

    BookPickerPanel()
    {
        _window = new UkWindow("drakes_book_picker", 520f);
        _window.SetHeader("Place a book", "Pick one, then aim and click");
    }

    void Open(Player player)
    {
        var body = _window.Body;
        body.Clear();
        _books.Clear();
        foreach (var item in player.GetInventory().GetAllItems())
            if (BookItem.IsBook(item))
                _books.Add(item);

        // ---- a new book
        var canAfford = BookPlaceState.CanAffordBlank(player);
        var cost = new List<string>();
        foreach (var (prefab, _, amount) in BookPlaceState.Cost())
            cost.Add($"{amount} {Localize(ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? prefab)}");
        var fresh = Card();
        var freshText = new VisualElement().Column().Grow();
        freshText.Add(UkControls.Text("A new book", 17, UkTheme.Text, heading: true, wrap: false));
        freshText.Add(UkControls.Text(string.Join(", ", cost) + (canAfford ? "" : "  ·  not enough"), 13, canAfford ? UkTheme.TextMuted : UkTheme.Danger, wrap: false));
        fresh.Add(Swatch(BookData.CoverColors[0]));
        fresh.Add(freshText);
        var make = UkControls.MakeButton("New book", () => Pick(BookPlaceState.ChooseBlank), UkButtonKind.Primary, width: 130);
        UkControls.SetButtonEnabled(make, canAfford);
        fresh.Add(make);
        body.Add(fresh);

        // ---- a book you carry
        var carried = Card();
        carried.style.flexDirection = FlexDirection.Column;
        carried.style.alignItems = Align.Stretch;
        carried.Add(UkControls.Text("A book you carry", 17, UkTheme.Text, heading: true, wrap: false));
        if (_books.Count == 0)
        {
            var none = UkControls.Text("No books in your inventory. Craft one at a workbench, or place a new one.", 13, UkTheme.TextMuted);
            none.style.marginTop = 4;
            carried.Add(none);
            body.Add(carried);
            _list = null;
            _window.ShowFooter(false);
            _window.Open();
            return;
        }

        // A drop-down: the chosen book on a button; the list opens under it and scrolls, however many books you carry.
        _selected = 0;
        var row = new VisualElement().Row();
        row.style.marginTop = 8;
        var choice = new Button(ToggleList);
        choice.style.flexGrow = 1;
        choice.style.marginLeft = choice.style.marginTop = choice.style.marginBottom = 0;
        choice.style.marginRight = 10;
        choice.style.height = UkTheme.Target + 2;
        choice.style.flexDirection = FlexDirection.Row;
        choice.style.alignItems = Align.Center;
        choice.style.paddingLeft = 10;
        choice.style.paddingRight = 12;
        choice.Fill(UkTheme.Field).Round(UkTheme.Radius).Border(UkTheme.Line, 1);
        _choiceSwatch = Swatch(BookData.GetColor(_books[0]));
        _choiceSwatch.pickingMode = PickingMode.Ignore;
        choice.Add(_choiceSwatch);
        _choiceText = UkControls.Text(Describe(_books[0]), 15, UkTheme.Text, wrap: false);
        _choiceText.pickingMode = PickingMode.Ignore;
        _choiceText.style.flexGrow = 1;
        _choiceText.style.overflow = Overflow.Hidden;
        choice.Add(_choiceText);
        var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
        chevron.style.width = chevron.style.height = 8;
        chevron.style.borderRightWidth = chevron.style.borderBottomWidth = 2;
        chevron.style.borderRightColor = chevron.style.borderBottomColor = UkTheme.TextMuted;
        chevron.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
        chevron.style.marginBottom = 4;
        choice.Add(chevron);
        row.Add(choice);
        row.Add(UkControls.MakeButton("Place it", () =>
        {
            if (_selected >= 0 && _selected < _books.Count)
            {
                var item = _books[_selected];
                Pick(() => BookPlaceState.Choose(item));
            }
        }, UkButtonKind.Primary, width: 110));
        carried.Add(row);

        _listContent = BookScroll.Make(300f, "drakes_book_picker_list", out var listFrame);
        _list = listFrame;
        _list.style.marginTop = 6;
        _list.Fill(UkTheme.PanelDeep).Round(UkTheme.Radius).Border(UkTheme.Line, 1);
        for (var i = 0; i < _books.Count; i++)
        {
            var index = i;
            var entry = new Button(() =>
            {
                Select(index);
                ToggleList();
            });
            entry.style.flexDirection = FlexDirection.Row;
            entry.style.alignItems = Align.Center;
            entry.style.height = 40;
            entry.style.marginLeft = entry.style.marginRight = entry.style.marginTop = entry.style.marginBottom = 0;
            entry.style.paddingLeft = 10;
            entry.Fill(Color.clear);
            entry.style.borderTopWidth = entry.style.borderBottomWidth = entry.style.borderLeftWidth = entry.style.borderRightWidth = 0;
            var dot = Swatch(BookData.GetColor(_books[i]));
            dot.pickingMode = PickingMode.Ignore;
            entry.Add(dot);
            var text = UkControls.Text(Describe(_books[i]), 15, UkTheme.Text, wrap: false);
            text.pickingMode = PickingMode.Ignore;
            entry.Add(text);
            _listContent.Add(entry);
        }

        _list.Show(false);
        carried.Add(_list);
        var count = UkControls.Text(_books.Count == 1 ? "1 book" : _books.Count + " books", 13, UkTheme.TextMuted, wrap: false);
        count.style.marginTop = 6;
        carried.Add(count);
        body.Add(carried);

        _window.ShowFooter(false);
        _window.Open();
    }

    void ToggleList()
    {
        if (_list != null)
            _list.Show(_list.style.display.value == DisplayStyle.None);
    }

    void Select(int index)
    {
        if (index < 0 || index >= _books.Count)
            return;
        _selected = index;
        if (_choiceText != null)
            _choiceText.text = Describe(_books[index]);
        _choiceSwatch?.Fill(BookData.GetColor(_books[index]));
    }

    static string Describe(ItemDrop.ItemData item)
    {
        var pages = BookData.GetPages(item).Count;
        return $"{BookData.DisplayTitle(item)}  ({BookData.GetSize(item)}, {pages} p.)";
    }

    static string Localize(string text) => Localization.instance != null ? Localization.instance.Localize(text) : text;

    void Pick(System.Action choose)
    {
        choose();
        BookPlacementPatches.KeepPick(3f);
        _window.CloseSilent();
    }

    static VisualElement Card()
    {
        var card = new VisualElement().Row();
        card.Pad(12, 14);
        card.Margin(bottom: 10);
        card.Fill(UkTheme.Raised).Round(UkTheme.Radius).Border(UkTheme.Line, 1);
        return card;
    }

    static VisualElement Swatch(Color cover)
    {
        var swatch = new VisualElement().Size(width: 18, height: 30).Fill(cover).Border(Color.black, 1).Round(2);
        swatch.style.marginRight = 12;
        swatch.style.flexShrink = 0;
        return swatch;
    }
}
