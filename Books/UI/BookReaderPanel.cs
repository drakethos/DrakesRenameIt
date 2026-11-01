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
    readonly UkSwitch _readable2;
    readonly UkSwitch _takeable2;
    readonly VisualElement _switchBar;
    readonly VisualElement _toolbar;
    readonly VisualElement _spread;
    readonly VisualElement _coverPreview;
    readonly Label _coverTitle;
    readonly Button _delPage;
    TextField? _tf;
    int _editPage;
    readonly VisualElement _placedOptions;
    readonly VisualElement _pictures;

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
        _window = new UkWindow("drakes_book_reader", 1240f);
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

        // The owner of a placed book sees who may read / take it right above the pages.
        _switchBar = new VisualElement().Row();
        _switchBar.style.marginBottom = 8;
        _readable = new UkSwitch("Readers can open", "Others may read it, not only you");
        _readable.Changed = on => OnFlag(BookData.KeyReadable, on);
        _takeable = new UkSwitch("Allow take", "Others may pick it up");
        _takeable.Changed = on => OnFlag(BookData.KeyTake, on);
        _readable.Root.style.marginRight = 40;
        _switchBar.Add(_readable.Root);
        _switchBar.Add(_takeable.Root);
        _pagesView.Add(_switchBar);

        // Edit mode: a small rich-text toolbar. It wraps the selection in the game's own text tags.
        _toolbar = new VisualElement().Row();
        _toolbar.style.marginBottom = 8;
        _toolbar.style.flexWrap = Wrap.Wrap;
        _toolbar.Add(UkControls.MakeButton("B", () => Tag("<b>", "</b>")));
        _toolbar.Add(UkControls.MakeButton("I", () => Tag("<i>", "</i>")));
        _toolbar.Add(UkControls.MakeButton("Bigger", () => Tag("<size=34>", "</size>")));
        _toolbar.Add(UkControls.MakeButton("Smaller", () => Tag("<size=20>", "</size>")));
        foreach (var hex in new[] { "9b2d20", "24456e", "2f5a2c", "6b2c82", "1a1a1a" })
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var ink);
            var dot = new VisualElement().Size(width: 34, height: 34).Fill(ink).Border(Color.black, 2).Round(4);
            dot.style.marginLeft = 10;
            dot.style.alignSelf = Align.Center;
            var tag = hex;
            dot.RegisterCallback<ClickEvent>(_ => Tag("<color=#" + tag + ">", "</color>"));
            _toolbar.Add(dot);
        }

        var clear = UkControls.MakeButton("Clear formatting", ClearFormatting);
        clear.style.marginLeft = 16;
        _toolbar.Add(clear);
        _pagesView.Add(_toolbar);

        var spread = new VisualElement().Row(Align.Stretch);
        _spread = spread;
        spread.style.justifyContent = Justify.Center;
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

        // --- Book tab: settings on the left, a live cover preview and sharing on the right
        _bookView = new VisualElement().Row(Align.FlexStart);
        var settings = new VisualElement().Column().Grow();
        settings.style.flexBasis = 0;
        settings.style.marginRight = 40;
        _title = new UkField("Title", multiline: false);
        _title.Changed = OnTitleChanged;
        settings.Add(_title.Root);

        var colorLabel = UkControls.Text("Cover colour", 17, UkTheme.Text, heading: true);
        colorLabel.style.marginTop = 24;
        settings.Add(colorLabel);
        _swatches = new VisualElement().Row();
        _swatches.style.marginTop = 8;
        for (var i = 0; i < BookData.CoverColors.Length; i++)
        {
            var color = BookData.CoverColors[i];
            var swatch = new VisualElement().Size(width: 64, height: 64).Fill(color).Border(Color.black, 3).Round(6);
            swatch.style.marginRight = 14;
            swatch.RegisterCallback<ClickEvent>(_ => OnColor(color));
            _swatches.Add(swatch);
        }

        settings.Add(_swatches);

        var sizeLabel = UkControls.Text("Size", 17, UkTheme.Text, heading: true);
        sizeLabel.style.marginTop = 24;
        settings.Add(sizeLabel);
        var sizeRow = new VisualElement().Row();
        sizeRow.style.marginTop = 8;
        string[] names = { "Small", "Medium", "Large" };
        for (var i = 0; i < 3; i++)
        {
            var size = (BookSize)i;
            _sizes[i] = UkControls.MakeButton(names[i], () => OnSize(size));
            sizeRow.Add(_sizes[i]);
        }

        settings.Add(sizeRow);

        var pictureLabel = UkControls.Text("Cover picture", 17, UkTheme.Text, heading: true);
        pictureLabel.style.marginTop = 24;
        settings.Add(pictureLabel);
        _pictures = new VisualElement().Row();
        _pictures.style.marginTop = 8;
        _pictures.style.flexWrap = Wrap.Wrap;
        settings.Add(_pictures);
        _bookView.Add(settings);

        var side = new VisualElement().Column();
        side.style.width = 320;
        side.Add(UkControls.Text("Preview", 17, UkTheme.Text, heading: true));
        _coverPreview = new VisualElement().Size(width: 280, height: 380).Border(Color.black, 3).Round(8);
        _coverPreview.style.marginTop = 8;
        _coverPreview.style.justifyContent = Justify.Center;
        _coverTitle = UkControls.Text("", 24, new Color(0.96f, 0.90f, 0.72f), wrap: true);
        _coverTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        _coverTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        _coverTitle.style.marginLeft = _coverTitle.style.marginRight = 20;
        _coverPreview.Add(_coverTitle);
        side.Add(_coverPreview);

        _placedOptions = new VisualElement().Column();
        _placedOptions.style.marginTop = 24;
        _readable2 = new UkSwitch("Readers can open", "Others may read it, not only you");
        _readable2.Changed = on => OnFlag(BookData.KeyReadable, on);
        _takeable2 = new UkSwitch("Allow take", "Others may pick it up");
        _takeable2.Changed = on => OnFlag(BookData.KeyTake, on);
        _placedOptions.Add(_readable2.Root);
        _placedOptions.Add(_takeable2.Root);
        side.Add(_placedOptions);
        _bookView.Add(side);
        body.Add(_bookView);

        // --- footer
        var foot = _window.Footer;
        _prev = UkControls.MakeButton("<", () => Turn(-1));
        _next = UkControls.MakeButton(">", () => Turn(1));
        _count = UkControls.Text("", 15, UkTheme.TextMuted, wrap: false);
        _edit = UkControls.MakeButton("Edit", ToggleEdit);
        _addPage = UkControls.MakeButton("Add page", AddPage);
        _delPage = UkControls.MakeButton("Delete page", DeletePage);
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
        foot.Add(_delPage);
        foot.Add(_addPage);
        foot.Add(_edit);
        _window.ShowFooter(true);
    }

    internal static VisualElement Page(Texture2D? texture)
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
        _readable2.SetValue(BookData.Readable(item));
        _takeable2.SetValue(BookData.GetFlag(item, BookData.KeyTake, false));
        _loading = false;
        _editPage = 0;
        _placedOptions.Show(_owner);
        _switchBar.Show(_owner);
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
        if (book)
            _editing = false;
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
        _delPage.Show(!book && _canEdit && _editing);
        if (!book)
            Render();
        UpdateHeader();
    }

    void UpdateHeader() => _window.SetHeader(_item != null ? BookData.DisplayTitle(_item) : "Book", _pages.Count + (_pages.Count == 1 ? " page" : " pages"));

    void Render()
    {
        _left.Clear();
        _right.Clear();
        _tf = null;
        _toolbar.Show(_canEdit && _editing);

        if (_editing)
        {
            // One page, wide enough to write on, with the toolbar above.
            _right.Show(false);
            _left.style.flexGrow = 0;
            _left.style.flexBasis = StyleKeyword.Auto;
            _left.style.width = 760;
            if (_pages.Count == 0)
                _pages.Add("");
            _editPage = Mathf.Clamp(_editPage, 0, _pages.Count - 1);
            Fill(_left, _editPage);
            _count.text = $"Page {_editPage + 1} of {_pages.Count}";
            UkControls.SetButtonEnabled(_prev, _editPage > 0);
            UkControls.SetButtonEnabled(_next, _editPage + 1 < _pages.Count);
        }
        else
        {
            _right.Show(true);
            _left.style.flexGrow = 1;
            _left.style.flexBasis = 0;
            _left.style.width = StyleKeyword.Auto;
            Fill(_left, _spreadStart);
            Fill(_right, _spreadStart + 1);
            var end = Math.Min(_spreadStart + 2, _pages.Count);
            _count.text = $"Pages {_spreadStart + 1}-{end} of {_pages.Count}";
            UkControls.SetButtonEnabled(_prev, _spreadStart > 0);
            UkControls.SetButtonEnabled(_next, _spreadStart + 2 < _pages.Count);
        }

        _edit.text = _editing ? "Done" : "Edit";
        _addPage.Show(_canEdit && _editing);
        _delPage.Show(_canEdit && _editing);
        UkControls.SetButtonEnabled(_addPage, _pages.Count < BookData.MaxPages);
        UkControls.SetButtonEnabled(_delPage, _pages.Count > 0);
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
            _tf = tf;
            page.Add(tf);
        }
        else
        {
            var text = UkControls.Text(_pages[index], BodyFont, Ink);
            text.enableRichText = true;
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

    /// <summary>Wrap the selected text (or the caret position) in a tag pair.</summary>
    void Tag(string open, string close)
    {
        var tf = _tf;
        if (tf == null)
            return;
        var v = tf.value ?? "";
        var a = Mathf.Clamp(Math.Min(tf.cursorIndex, tf.selectIndex), 0, v.Length);
        var b = Mathf.Clamp(Math.Max(tf.cursorIndex, tf.selectIndex), a, v.Length);
        tf.value = v.Substring(0, a) + open + v.Substring(a, b - a) + close + v.Substring(b);
        tf.Focus();
        tf.SelectRange(a + open.Length, b + open.Length);
    }

    void ClearFormatting()
    {
        if (_tf == null)
            return;
        _tf.value = System.Text.RegularExpressions.Regex.Replace(_tf.value ?? "", "</?(b|i|size|color)(=[^>]*)?>", "");
    }

    /// <summary>Turn the page: two at a time when reading, one at a time when editing.</summary>
    void Turn(int direction)
    {
        Commit();
        if (_editing)
        {
            _editPage = Mathf.Clamp(_editPage + direction, 0, Math.Max(0, _pages.Count - 1));
        }
        else
        {
            _spreadStart = Mathf.Clamp(_spreadStart + direction * 2, 0, Math.Max(0, _pages.Count - 1));
            _spreadStart -= _spreadStart % 2;
        }

        Render();
    }

    void ToggleEdit()
    {
        if (!_canEdit)
            return;
        Commit();
        _editing = !_editing;
        if (_editing)
            _editPage = Mathf.Clamp(_spreadStart, 0, Math.Max(0, _pages.Count - 1));
        else
            _spreadStart = _editPage - _editPage % 2;
        Render();
    }

    void AddPage()
    {
        if (_pages.Count >= BookData.MaxPages)
            return;
        _pages.Add("");
        _editPage = _pages.Count - 1;
        _spreadStart = _editPage - _editPage % 2;
        Commit();
        Render();
    }

    void DeletePage()
    {
        if (_pages.Count == 0)
            return;
        _pages.RemoveAt(Mathf.Clamp(_editPage, 0, _pages.Count - 1));
        if (_pages.Count == 0)
            _pages.Add("");
        _editPage = Mathf.Clamp(_editPage, 0, _pages.Count - 1);
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

        _coverPreview.style.backgroundColor = current;
        var shown = BookData.Get(_item, BookData.KeyCover);
        var pic = string.IsNullOrEmpty(shown) ? null : BookCovers.Texture(shown);
        _coverPreview.style.backgroundImage = pic != null ? new StyleBackground(pic) : new StyleBackground();
        _coverTitle.text = pic != null ? "" : BookData.DisplayTitle(_item);

        // Cover pictures: None, then every file in the BookCovers folder (a server or modpack adds them).
        _pictures.Clear();
        var chosen = BookData.Get(_item, BookData.KeyCover);
        void AddPicture(string label, string name)
        {
            var button = UkControls.MakeButton(label, () => OnPicture(name), chosen == name ? UkButtonKind.Primary : UkButtonKind.Secondary);
            button.style.marginBottom = 6;
            _pictures.Add(button);
        }

        AddPicture("None", "");
        foreach (var picture in BookCovers.Names())
            AddPicture(picture, picture);
        if (BookCovers.Names().Count == 0)
            _pictures.Add(UkControls.Text("No pictures yet. Put PNG or JPG files in BepInEx/config/DrakesRenameIt/BookCovers.", 13, UkTheme.TextMuted));
    }

    void OnPicture(string name)
    {
        if (_item == null || !_canEdit)
            return;
        BookData.Set(_item, BookData.KeyCover, name);
        Save();
        RefreshBookView();
    }

    void OnTitleChanged(string value)
    {
        if (_loading || _item == null || !_canEdit)
            return;
        BookData.SetTitle(_item, value);
        Save();
        UpdateHeader();
        RefreshBookView();
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
        _loading = true;
        (key == BookData.KeyReadable ? _readable : _takeable).SetValue(on);
        (key == BookData.KeyReadable ? _readable2 : _takeable2).SetValue(on);
        _loading = false;
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
