using System;
using System.Collections.Generic;
using DrakeModsLibs.Forge;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// The book window: two big pages side by side on the paper texture, &lt; and &gt; to turn, and for the book's maker an Edit mode
/// (type straight into the pages) and a Book tab (title, cover colour, size, and for a placed book who may read or take it).
/// Changes are written to the item as they are made.
/// </summary>
internal sealed class BookReaderPanel
{
    static readonly Color PaperTone = new(0.85f, 0.78f, 0.63f);
    static readonly Color Ink = new(0.23f, 0.16f, 0.10f);
    static readonly Color PageNumber = new(0.48f, 0.39f, 0.27f);
    const int BodyFont = 25;
    const float PageHeight = 680f;
    static BookReaderPanel? _instance;

    readonly UkWindow _window;
    readonly VisualElement _tabRow;
    readonly Button _pagesTab;
    readonly Button _bookTab;
    readonly VisualElement _pagesView;
    readonly VisualElement _bookView;
    readonly VisualElement _left;
    readonly VisualElement _right;
    readonly Label _count;
    readonly Button _prev;
    readonly Button _next;
    readonly Button _edit;
    readonly Button _addPage;
    readonly Button _take;
    readonly UkField _title;
    readonly VisualElement _swatches;
    readonly Button[] _sizes = new Button[3];
    readonly UkSwitch _readable;
    readonly UkSwitch _takeable;
    readonly VisualElement _placedOptions;

    ItemDrop.ItemData? _item;
    List<string> _pages = new();
    int _spreadStart;
    bool _editing;
    bool _canEdit;
    bool _owner;
    bool _loading;
    Action<ItemDrop.ItemData>? _onSave;
    Action? _onTake;

    public static bool IsOpen => _instance != null && _instance._window.IsOpen;

    internal static void CloseWindow() => _instance?._window.CloseSilent();

    /// <summary>
    /// Open the book. <paramref name="canEdit"/> is the maker's own book (Edit and the Book tab). A placed book also passes
    /// <paramref name="onSave"/> (called with the edited copy, to write back to the piece), <paramref name="owner"/> (shows who may
    /// read or take it) and <paramref name="onTake"/> (shows Take when the player may pick it up).
    /// </summary>
    public static void Show(ItemDrop.ItemData item, bool canEdit, Action<ItemDrop.ItemData>? onSave = null, bool owner = false, Action? onTake = null)
    {
        _instance ??= new BookReaderPanel();
        _instance._onSave = onSave;
        _instance._owner = owner;
        _instance._onTake = onTake;
        _instance.Open(item, canEdit);
    }

    /// <summary>Test hooks: switch the open window to a tab / into edit mode without clicking.</summary>
    internal static void TestSelect(bool bookTab, bool editing)
    {
        if (_instance == null)
            return;
        _instance.SelectTab(bookTab);
        if (!bookTab && editing && !_instance._editing && _instance._canEdit)
            _instance.ToggleEdit();
    }

    BookReaderPanel()
    {
        _window = new UkWindow("drakes_book_reader", 1780f);
        _window.Closed = Commit;
        var body = _window.Body;

        _tabRow = new VisualElement().Row();
        _tabRow.style.marginBottom = 10;
        _pagesTab = UkControls.MakeButton("Pages", () => SelectTab(book: false));
        _bookTab = UkControls.MakeButton("Book", () => SelectTab(book: true));
        _tabRow.Add(_pagesTab);
        _tabRow.Add(_bookTab);
        body.Add(_tabRow);

        // --- Pages tab
        _pagesView = new VisualElement().Column();
        var spread = new VisualElement().Row(Align.Stretch);
        spread.style.minHeight = PageHeight;
        spread.Round(4).Border(new Color(0.29f, 0.21f, 0.13f), 3);
        var texture = ForgeTextures.Load(typeof(BookReaderPanel).Assembly, "textures/paper_sheet.png");
        _left = Page(texture);
        _right = Page(texture);
        _right.style.borderLeftWidth = 2;
        _right.style.borderLeftColor = new Color(0.66f, 0.57f, 0.42f);
        spread.Add(_left);
        spread.Add(_right);
        _pagesView.Add(spread);
        body.Add(_pagesView);

        // --- Book tab
        _bookView = new VisualElement().Column();
        _title = new UkField("Title", multiline: false);
        _title.Changed = OnTitleChanged;
        _bookView.Add(_title.Root);

        var colorLabel = UkControls.Text("Cover colour", 15, UkTheme.Text, heading: true);
        colorLabel.style.marginTop = 16;
        _bookView.Add(colorLabel);
        _swatches = new VisualElement().Row();
        _swatches.style.marginTop = 6;
        for (var i = 0; i < BookData.CoverColors.Length; i++)
        {
            var color = BookData.CoverColors[i];
            var swatch = new VisualElement().Size(width: 40, height: 40).Fill(color).Border(Color.black, 2).Round(4);
            swatch.style.marginRight = 10;
            swatch.RegisterCallback<ClickEvent>(_ => OnColor(color));
            _swatches.Add(swatch);
        }

        _bookView.Add(_swatches);

        var sizeLabel = UkControls.Text("Size", 15, UkTheme.Text, heading: true);
        sizeLabel.style.marginTop = 16;
        _bookView.Add(sizeLabel);
        var sizeRow = new VisualElement().Row();
        sizeRow.style.marginTop = 6;
        string[] names = { "Small", "Medium", "Large" };
        for (var i = 0; i < 3; i++)
        {
            var size = (BookSize)i;
            _sizes[i] = UkControls.MakeButton(names[i], () => OnSize(size));
            sizeRow.Add(_sizes[i]);
        }

        _bookView.Add(sizeRow);

        _placedOptions = new VisualElement().Column();
        _placedOptions.style.marginTop = 16;
        _readable = new UkSwitch("Readers can open", "Others may read it, not only you");
        _readable.Changed = on => OnFlag(BookData.KeyReadable, on);
        _takeable = new UkSwitch("Allow take", "Others may pick it up");
        _takeable.Changed = on => OnFlag(BookData.KeyTake, on);
        _placedOptions.Add(_readable.Root);
        _placedOptions.Add(_takeable.Root);
        _bookView.Add(_placedOptions);
        body.Add(_bookView);

        // --- footer
        var foot = _window.Footer;
        _prev = UkControls.MakeButton("<", () => Turn(-2));
        _next = UkControls.MakeButton(">", () => Turn(2));
        _count = UkControls.Text("", 15, UkTheme.TextMuted, wrap: false);
        _edit = UkControls.MakeButton("Edit", ToggleEdit);
        _addPage = UkControls.MakeButton("Add page", AddPage);
        _take = UkControls.MakeButton("Take", () =>
        {
            Commit();
            var take = _onTake;
            _window.CloseSilent();
            take?.Invoke();
        });
        foot.Add(_prev);
        foot.Add(_count);
        foot.Add(_next);
        foot.Add(UkControls.Spacer());
        foot.Add(_take);
        foot.Add(_addPage);
        foot.Add(_edit);
        _window.ShowFooter(true);
    }

    static VisualElement Page(Texture2D? texture)
    {
        var page = new VisualElement().Column().Grow();
        page.style.flexBasis = 0;
        page.style.backgroundColor = PaperTone;
        if (texture != null)
        {
            texture.filterMode = FilterMode.Point;
            page.style.backgroundImage = new StyleBackground(texture);
            page.style.unityBackgroundImageTintColor = Color.white;
        }

        page.Pad(40, 52);
        return page;
    }

    void Open(ItemDrop.ItemData item, bool canEdit)
    {
        _item = item;
        _canEdit = canEdit;
        _editing = false;
        _pages = BookData.GetPages(item);
        _spreadStart = 0;

        _loading = true;
        _title.Input.value = CustomTitle(item);
        _readable.SetValue(BookData.Readable(item));
        _takeable.SetValue(BookData.GetFlag(item, BookData.KeyTake, false));
        _loading = false;
        _placedOptions.Show(_owner);
        _tabRow.Show(_canEdit);
        _take.Show(_onTake != null);
        RefreshBookView();
        SelectTab(book: false);
        _window.Open();
    }

    /// <summary>The title as typed (empty when the book has none yet), not the "Untitled book" stand-in.</summary>
    static string CustomTitle(ItemDrop.ItemData item) =>
        item.m_customData != null && item.m_customData.TryGetValue(DrakeModsLibs.Data.DrakeCustomDataKeys.Rename, out var t) ? t : "";

    void SelectTab(bool book)
    {
        Commit();
        _pagesView.Show(!book);
        _bookView.Show(book);
        UkControls.SetKind(_pagesTab, book ? UkButtonKind.Secondary : UkButtonKind.Primary);
        UkControls.SetKind(_bookTab, book ? UkButtonKind.Primary : UkButtonKind.Secondary);
        _window.Footer.Show(true);
        _prev.Show(!book);
        _next.Show(!book);
        _count.Show(!book);
        _edit.Show(!book && _canEdit);
        _addPage.Show(!book && _canEdit && _editing);
        if (!book)
            Render();
        UpdateHeader();
    }

    void UpdateHeader() => _window.SetHeader(_item != null ? BookData.DisplayTitle(_item) : "Book", _pages.Count + (_pages.Count == 1 ? " page" : " pages"));

    void Render()
    {
        _left.Clear();
        _right.Clear();
        Fill(_left, _spreadStart);
        Fill(_right, _spreadStart + 1);

        var end = Math.Min(_spreadStart + 2, _pages.Count);
        _count.text = $"Pages {_spreadStart + 1}-{end} of {_pages.Count}";
        UkControls.SetButtonEnabled(_prev, _spreadStart > 0);
        UkControls.SetButtonEnabled(_next, _spreadStart + 2 < _pages.Count);
        _edit.text = _editing ? "Done" : "Edit";
        _addPage.Show(_canEdit && _editing);
        UkControls.SetButtonEnabled(_addPage, _pages.Count < BookData.MaxPages);
        UpdateHeader();
    }

    void Fill(VisualElement page, int index)
    {
        if (index >= _pages.Count)
            return;

        if (_editing)
        {
            var tf = new TextField { multiline = true, isDelayed = false, maxLength = BookData.MaxPageChars };
            tf.style.flexGrow = 1;
            tf.style.marginLeft = tf.style.marginRight = tf.style.marginTop = tf.style.marginBottom = 0;
            tf.value = _pages[index];
            var box = tf.Q<VisualElement>("unity-text-input") ?? tf;
            // Write straight on the paper: no box, no border, no fill; only the caret and selection show where you are.
            box.style.backgroundColor = Color.clear;
            box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 0;
            box.style.color = Ink;
            box.style.fontSize = BodyFont;
            box.style.minHeight = PageHeight - 130f;
            box.style.unityTextAlign = TextAnchor.UpperLeft;
            box.style.whiteSpace = WhiteSpace.Normal;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 0;
            tf.Q<VisualElement>(className: "unity-base-field__input")?.Fill(Color.clear);
            tf.textSelection.cursorColor = Ink;
            tf.textSelection.selectionColor = new Color(0.55f, 0.40f, 0.18f, 0.35f);
            UkFonts.ApplyBody(box);
            tf.RegisterValueChangedCallback(e => _pages[index] = e.newValue ?? "");
            page.Add(tf);
        }
        else
        {
            var text = UkControls.Text(_pages[index], BodyFont, Ink);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexGrow = 1;
            text.style.unityTextAlign = TextAnchor.UpperLeft;
            UkFonts.ApplyBody(text);
            page.Add(text);
        }

        var number = UkControls.Text((index + 1).ToString(), 13, PageNumber, wrap: false);
        number.style.unityTextAlign = index % 2 == 0 ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        page.Add(number);
    }

    void Turn(int by)
    {
        Commit();
        _spreadStart = Mathf.Clamp(_spreadStart + by, 0, Math.Max(0, _pages.Count - 1));
        _spreadStart -= _spreadStart % 2;
        Render();
    }

    void ToggleEdit()
    {
        if (!_canEdit)
            return;
        _editing = !_editing;
        Commit();
        Render();
    }

    void AddPage()
    {
        if (_pages.Count >= BookData.MaxPages)
            return;
        _pages.Add("");
        _spreadStart = (_pages.Count - 1) - ((_pages.Count - 1) % 2);
        Commit();
        Render();
    }

    // ---- Book tab

    void RefreshBookView()
    {
        if (_item == null)
            return;
        var current = BookData.GetColor(_item);
        var size = BookData.GetSize(_item);
        var i = 0;
        foreach (var child in _swatches.Children())
        {
            var on = ColorUtility.ToHtmlStringRGB(BookData.CoverColors[i]) == ColorUtility.ToHtmlStringRGB(current);
            child.style.borderTopColor = child.style.borderBottomColor = child.style.borderLeftColor = child.style.borderRightColor =
                on ? UkTheme.Gold : Color.black;
            i++;
        }

        for (var s = 0; s < 3; s++)
            UkControls.SetKind(_sizes[s], (int)size == s ? UkButtonKind.Primary : UkButtonKind.Secondary);
    }

    void OnTitleChanged(string value)
    {
        if (_loading || _item == null || !_canEdit)
            return;
        BookData.SetTitle(_item, value);
        Save();
        UpdateHeader();
    }

    void OnColor(Color color)
    {
        if (_item == null || !_canEdit)
            return;
        BookData.SetColor(_item, color);
        Save();
        RefreshBookView();
    }

    void OnSize(BookSize size)
    {
        if (_item == null || !_canEdit)
            return;
        BookData.SetSize(_item, size);
        Save();
        RefreshBookView();
    }

    void OnFlag(string key, bool on)
    {
        if (_loading || _item == null || !_owner)
            return;
        BookData.SetFlag(_item, key, on);
        Save();
    }

    /// <summary>Write the pages to the item (the maker's own book) and tell the placed piece, if there is one.</summary>
    void Commit()
    {
        if (_item == null || !_canEdit && !_owner)
            return;
        if (_canEdit)
            BookData.SetPages(_item, _pages);
        _onSave?.Invoke(_item);
    }

    void Save() => _onSave?.Invoke(_item!);
}
