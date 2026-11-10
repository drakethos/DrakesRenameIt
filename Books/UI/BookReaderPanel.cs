using System;
using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// Books have two ways in.
/// <list type="bullet">
/// <item><b>Read</b> (Use on a book, E on a placed or shelved book): no window. The open book sits over the dimmed game with a
/// slim strip of controls under it. Whoever may edit gets <b>Edit</b> on the strip and writes straight onto the two pages, with
/// the same formatting bar as paper and the Rename tab.</item>
/// <item><b>Full editor</b> (Shift+E on a placed or shelved book, Shift+right-click in the inventory, or the strip's Full editor):
/// the suite's window with a Pages tab (the same book, always editable) and a Book tab (title, cover, size, picture, and for a
/// placed book who may read or take it).</item>
/// </list>
/// Pages are written to the item as you type; the placed piece or shelf gets them through the save callback.
/// </summary>
internal sealed class BookReaderPanel
{
    const float ReadPageW = 540f, ReadPageH = 700f;
    const int ReadFont = 24;
    const float FullPageW = 470f, FullPageH = 600f;
    const int FullFont = 21;
    static BookReaderPanel? _instance;

    /// <summary>The Book tab in the inventory tab host (Shift+right-click), beside Rename.</summary>
    internal const string HostTabId = "renameit.book";

    /// <summary>The full editor was opened as the tab host's Book tab: closing it ends the host session, and its tab row shows.</summary>
    bool _hosted;

    // ---- read view
    readonly UkWindow _read;
    readonly Label _readTitle;
    readonly Label _readByline;
    readonly BookSpread _readSpread;
    readonly PageEditors _readEditors;
    readonly Label _readCount;
    readonly Button _readPrev;
    readonly Button _readNext;
    readonly Button _readEdit;
    readonly Button _readAdd;
    readonly Button _readDelete;
    readonly Button _readTake;
    readonly Button _readFull;

    // ---- full editor
    readonly UkWindow _full;
    readonly Button _pagesTab;
    readonly Button _bookTab;
    readonly VisualElement _pagesView;
    readonly VisualElement _bookView;
    readonly BookSpread _fullSpread;
    readonly PageEditors _fullEditors;
    readonly Label _fullCount;
    readonly Button _fullPrev;
    readonly Button _fullNext;
    readonly Button _fullAdd;
    readonly Button _fullDelete;
    readonly Button _fullTake;
    readonly UkField _title;
    readonly VisualElement _swatches;
    readonly VisualElement _patterns;
    readonly VisualElement _accents;
    readonly VisualElement _inks;
    readonly Button[] _sizes = new Button[3];
    readonly VisualElement _pictures;
    readonly VisualElement _coverPreview;
    readonly Label _coverTitle;
    readonly VisualElement _spinePreview;
    readonly Label _spineTitle;
    readonly VisualElement _placedOptions;
    readonly UkSwitch _readable;
    readonly UkSwitch _takeable;

    ItemDrop.ItemData? _item;
    List<string> _pages = new();
    /// <summary>Index of the left page of the open spread (always even).</summary>
    int _spread;
    bool _editing;
    bool _canEdit;
    bool _owner;
    bool _loading;
    bool _fullMode;
    Action<ItemDrop.ItemData>? _onSave;
    Action? _onTake;

    public static bool IsOpen => _instance != null && (_instance._read.IsOpen || _instance._full.IsOpen);

    /// <summary>True while the player is typing into a page (read view Edit, or the full editor's Pages tab).</summary>
    internal static bool IsEditing => _instance != null && (_instance._read.IsOpen && _instance._editing || _instance._full.IsOpen);

    internal static void CloseWindow()
    {
        if (_instance == null)
            return;
        _instance.Commit();
        _instance._read.CloseSilent();
        _instance._full.CloseSilent();
    }

    /// <summary>
    /// Read the book. <paramref name="canEdit"/> lets the player write in it (Edit on the strip, and the full editor). A placed book
    /// also passes <paramref name="onSave"/> (called with the edited copy, to write it back), <paramref name="owner"/> (may choose who
    /// reads or takes it) and <paramref name="onTake"/> (shows Take).
    /// </summary>
    public static void Show(ItemDrop.ItemData item, bool canEdit, Action<ItemDrop.ItemData>? onSave = null, bool owner = false, Action? onTake = null)
    {
        _instance ??= new BookReaderPanel();
        _instance.Load(item, canEdit, onSave, owner, onTake);
        _instance.OpenRead();
    }

    /// <summary>The full editor (Pages and Book tabs). Falls back to reading when the player may not edit.</summary>
    public static void ShowFull(ItemDrop.ItemData item, bool canEdit, Action<ItemDrop.ItemData>? onSave = null, bool owner = false, Action? onTake = null)
    {
        _instance ??= new BookReaderPanel();
        _instance.Load(item, canEdit, onSave, owner, onTake);
        if (canEdit || owner)
            _instance.OpenFull(bookTab: false);
        else
            _instance.OpenRead();
    }

    /// <summary>The tab host opened the Book tab (Shift+right-click on a book): straight to the Book settings, Pages one click away.</summary>
    internal static void ShowFromHost(ItemDrop.ItemData item)
    {
        _instance ??= new BookReaderPanel();
        _instance.Load(item, BookAccess.CanEditItem(item), null, false, null);
        _instance._hosted = true;
        _instance.OpenFull(bookTab: true);
    }

    /// <summary>The tab host switched to another tab: keep the session, just hide.</summary>
    internal static void HideFromHost()
    {
        if (_instance == null || !_instance._full.IsOpen)
            return;
        _instance.Commit();
        _instance._full.CloseSilent();
    }

    /// <summary>Test hook: in the read view's Edit, puts <paramref name="raw"/> on the left page and selects [anchor, cursor).</summary>
    internal static bool TestCaret(string raw, int cursor, int anchor)
    {
        var panel = _instance;
        if (panel == null || !panel._editing)
            return false;
        panel._readEditors.TestSet(raw, cursor, anchor);
        return true;
    }

    /// <summary>Test hooks: the Book tab (full editor), or Edit in the read view.</summary>
    internal static void TestSelect(bool bookTab, bool editing)
    {
        var panel = _instance;
        if (panel == null)
            return;
        if (bookTab)
        {
            panel._read.CloseSilent();
            panel.OpenFull(bookTab: true);
        }
        else if (editing && !panel._editing && panel._canEdit)
        {
            panel.ToggleEdit();
        }
    }

    BookReaderPanel()
    {
        // ------------------------------------------------------------ read view: just the book, controls below
        _read = new UkWindow("drakes_book_read", 1460f, dim: true, draggable: false);
        _read.Closed = Commit;
        _read.EscapeHandler = () => _readEditors.Toolbar.PickerOpen && Do(_readEditors.Toolbar.ClosePicker);
        var card = _read.Card;
        card.style.backgroundColor = Color.clear;
        card.Border(Color.clear, 0);
        card.style.overflow = Overflow.Visible;
        if (card.childCount > 0)
            card[0].Show(false); // the window header: reading has no box around it
        card.parent.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        var body = _read.Body;
        body.style.paddingTop = body.style.paddingBottom = body.style.paddingLeft = body.style.paddingRight = 0;
        body.style.alignItems = Align.Center;
        body.focusable = true;
        body.RegisterCallback<KeyDownEvent>(OnReadKey);

        _readTitle = UkControls.Text("", 28, UkTheme.Text, heading: true, wrap: false);
        _readTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        _readByline = UkControls.Text("", 15, UkTheme.TextMuted, wrap: false);
        _readByline.style.unityTextAlign = TextAnchor.MiddleCenter;
        _readByline.style.marginBottom = 12;
        body.Add(_readTitle);
        body.Add(_readByline);

        _readSpread = new BookSpread(ReadPageW, ReadPageH);
        body.Add(_readSpread.Root);
        _readEditors = new PageEditors(this, _read.Tips, body, ReadFont, ReadPageH);
        _readEditors.Toolbar.Root.style.marginTop = 12;
        _readEditors.Toolbar.Root.style.width = 820;
        body.Add(_readEditors.Toolbar.Root);

        var strip = new VisualElement().Row().Fill(new Color(UkTheme.Panel.r, UkTheme.Panel.g, UkTheme.Panel.b, 0.94f))
            .Round(UkTheme.RadiusLarge).Border(UkTheme.Line).Pad(8, 10);
        strip.style.marginTop = 12;
        strip.style.alignSelf = Align.Center;
        _readPrev = UkControls.MakeIconButton("‹", () => Turn(-1), "Previous pages (A or Left)");
        _readCount = UkControls.Text("", 15, UkTheme.TextMuted, wrap: false);
        _readCount.style.minWidth = 150;
        _readCount.style.unityTextAlign = TextAnchor.MiddleCenter;
        _readNext = UkControls.MakeIconButton("›", () => Turn(1), "Next pages (D or Right)");
        _readEdit = UkControls.MakeButton("Edit", ToggleEdit);
        _readAdd = UkControls.MakeButton("Add page", AddPage);
        _readDelete = UkControls.MakeButton("Remove page", DeletePage, UkButtonKind.Danger);
        _readTake = UkControls.MakeButton("Take", DoTake);
        _readFull = UkControls.MakeButton("Full editor", () =>
        {
            Commit();
            _read.CloseSilent();
            OpenFull(bookTab: false);
        });
        var close = UkControls.MakeIconButton("✕", () => _read.Close(), "Close (Esc)");
        strip.Add(_readPrev);
        strip.Add(_readCount);
        strip.Add(_readNext);
        var gap = new VisualElement().Size(width: 24);
        strip.Add(gap);
        foreach (var b in new[] { _readEdit, _readAdd, _readDelete, _readTake, _readFull })
        {
            b.style.marginRight = 6;
            strip.Add(b);
        }

        strip.Add(close);
        body.Add(strip);

        // ------------------------------------------------------------ full editor: the suite's window, Pages and Book tabs
        _full = new UkWindow("drakes_book_editor", 1240f);
        _full.Closed = () =>
        {
            Commit();
            if (!_hosted)
                return;
            _hosted = false;
            DrakeModsLibs.UI.DrakeTabHost.Close();
        };
        _full.EscapeHandler = () => _fullEditors.Toolbar.PickerOpen && Do(_fullEditors.Toolbar.ClosePicker);
        var fbody = _full.Body;

        var tabs = new VisualElement().Row();
        tabs.style.marginBottom = 12;
        _pagesTab = UkControls.MakeButton("Pages", () => SelectTab(book: false), UkButtonKind.Ghost);
        _bookTab = UkControls.MakeButton("Cover", () => SelectTab(book: true), UkButtonKind.Ghost);
        _pagesTab.style.height = _bookTab.style.height = 40;
        _pagesTab.style.marginRight = 6;
        tabs.Add(_pagesTab);
        tabs.Add(_bookTab);
        fbody.Add(tabs);

        // Pages: the formatting bar above the book, like the paper editor; paging below.
        _pagesView = new VisualElement().Column();
        _pagesView.style.alignItems = Align.Center;
        _fullSpread = new BookSpread(FullPageW, FullPageH, 40f);
        _fullEditors = new PageEditors(this, _full.Tips, fbody, FullFont, FullPageH);
        _fullEditors.Toolbar.Root.style.alignSelf = Align.Stretch;
        _fullEditors.Toolbar.Root.style.marginBottom = 12;
        _pagesView.Add(_fullEditors.Toolbar.Root);
        _pagesView.Add(_fullSpread.Root);
        var paging = new VisualElement().Row();
        paging.style.marginTop = 12;
        paging.style.alignSelf = Align.Stretch;
        _fullPrev = UkControls.MakeIconButton("‹", () => Turn(-1), "Previous pages");
        _fullCount = UkControls.Text("", 15, UkTheme.TextMuted, wrap: false);
        _fullCount.style.minWidth = 150;
        _fullCount.style.unityTextAlign = TextAnchor.MiddleCenter;
        _fullNext = UkControls.MakeIconButton("›", () => Turn(1), "Next pages");
        _fullAdd = UkControls.MakeButton("Add page", AddPage);
        _fullDelete = UkControls.MakeButton("Remove page", DeletePage, UkButtonKind.Danger);
        _fullDelete.style.marginLeft = 6;
        paging.Add(_fullPrev);
        paging.Add(_fullCount);
        paging.Add(_fullNext);
        paging.Add(UkControls.Spacer());
        var hint = UkControls.Text("Saved as you type  ·  Esc closes", 13, UkTheme.TextMuted, wrap: false);
        hint.style.marginRight = 14;
        paging.Add(hint);
        paging.Add(_fullAdd);
        paging.Add(_fullDelete);
        _pagesView.Add(paging);
        fbody.Add(_pagesView);

        // Book: settings on the left, the book as it will look (cover and spine) on the right.
        _bookView = new VisualElement().Row(Align.FlexStart);
        var settings = new VisualElement().Column().Grow();
        settings.style.flexBasis = 0;
        settings.style.marginRight = 36;
        _title = new UkField("Title", multiline: false);
        _title.SetMax(BookData.MaxTitleChars);
        _title.Changed = OnTitleChanged;
        settings.Add(_title.Root);

        settings.Add(Heading("Cover colour"));
        _swatches = Swatches(BookData.CoverColors, 40f, OnColor);
        settings.Add(_swatches);

        settings.Add(Heading("Pattern"));
        _patterns = new VisualElement().Row();
        _patterns.style.flexWrap = Wrap.Wrap;
        for (var p = 0; p <= (int)BookPattern.Studs; p++)
        {
            var pattern = (BookPattern)p;
            var tile = new VisualElement().Size(width: 46, height: 62).Round(4).Border(Color.black, 2);
            tile.style.marginRight = 8;
            tile.style.marginBottom = 8;
            tile.tooltip = pattern.ToString();
            tile.RegisterCallback<ClickEvent>(_ => OnPattern(pattern));
            _patterns.Add(tile);
        }

        settings.Add(_patterns);
        var accentHead = Heading("Pattern colour");
        settings.Add(accentHead);
        _accents = Swatches(BookLook.Accents, 32f, OnAccent);
        settings.Add(_accents);
        settings.Add(Heading("Spine lettering"));
        _inks = Swatches(BookLook.Inks, 32f, OnInk);
        settings.Add(_inks);

        settings.Add(Heading("Size"));
        var sizeRow = new VisualElement().Row();
        string[] names = { "Small", "Medium", "Large" };
        for (var i = 0; i < 3; i++)
        {
            var size = (BookSize)i;
            _sizes[i] = UkControls.MakeButton(names[i], () => OnSize(size));
            _sizes[i].style.marginRight = 6;
            sizeRow.Add(_sizes[i]);
        }

        settings.Add(sizeRow);
        settings.Add(Heading("Cover picture"));
        _pictures = new VisualElement().Row();
        _pictures.style.flexWrap = Wrap.Wrap;
        settings.Add(_pictures);

        _placedOptions = new VisualElement().Column();
        _placedOptions.style.marginTop = 22;
        _placedOptions.Add(Heading("This placed book"));
        _readable = new UkSwitch("Readers can open", "Others may read it, not only you");
        _readable.Changed = on => OnFlag(BookData.KeyReadable, on);
        _readable.Root.style.marginBottom = 8;
        _takeable = new UkSwitch("Allow take", "Others may pick it up");
        _takeable.Changed = on => OnFlag(BookData.KeyTake, on);
        _placedOptions.Add(_readable.Root);
        _placedOptions.Add(_takeable.Root);
        _fullTake = UkControls.MakeButton("Take the book", DoTake);
        _fullTake.style.marginTop = 10;
        _fullTake.style.alignSelf = Align.FlexStart;
        _placedOptions.Add(_fullTake);
        settings.Add(_placedOptions);
        _bookView.Add(settings);

        var side = new VisualElement().Column();
        side.style.width = 380;
        side.Add(UkControls.Text("Preview", 15, UkTheme.Text, heading: true));
        var previews = new VisualElement().Row(Align.FlexEnd);
        previews.style.marginTop = 8;
        _coverPreview = new VisualElement().Size(width: 250, height: 340).Round(8);
        _coverPreview.style.justifyContent = Justify.Center;
        _coverPreview.style.marginRight = 24;
        _coverTitle = UkControls.Text("", 22, new Color(0.96f, 0.90f, 0.72f));
        _coverTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        _coverTitle.style.marginLeft = _coverTitle.style.marginRight = 18;
        UkFonts.ApplyHeading(_coverTitle);
        _coverPreview.Add(_coverTitle);
        previews.Add(_coverPreview);
        _spinePreview = new VisualElement().Size(width: 46, height: 340).Round(4);
        _spinePreview.style.alignItems = Align.Center;
        _spinePreview.style.justifyContent = Justify.Center;
        _spinePreview.style.overflow = Overflow.Hidden;
        _spineTitle = UkControls.Text("", 17, new Color(0.96f, 0.90f, 0.72f), wrap: false);
        _spineTitle.style.rotate = new Rotate(-90f); // reads bottom to top, like the spines on a shelf
        _spineTitle.style.width = 320;
        _spineTitle.style.flexShrink = 0;
        _spineTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        UkFonts.ApplyHeading(_spineTitle);
        _spinePreview.Add(_spineTitle);
        previews.Add(_spinePreview);
        side.Add(previews);
        var spineNote = UkControls.Text("Cover, and the spine as it stands on a shelf", 13, UkTheme.TextMuted);
        spineNote.style.marginTop = 8;
        side.Add(spineNote);
        _bookView.Add(side);
        fbody.Add(_bookView);
    }

    static VisualElement Swatches(Color[] colors, float size, Action<Color> pick)
    {
        var row = new VisualElement().Row();
        row.style.flexWrap = Wrap.Wrap;
        foreach (var color in colors)
        {
            var c = color;
            var swatch = new VisualElement().Size(width: size, height: size).Fill(c).Border(Color.black, 3).Round(6);
            swatch.style.marginRight = 8;
            swatch.style.marginBottom = 8;
            swatch.RegisterCallback<ClickEvent>(_ => pick(c));
            row.Add(swatch);
        }

        return row;
    }

    static void MarkSwatches(VisualElement row, Color[] colors, Color current)
    {
        var i = 0;
        foreach (var child in row.Children())
        {
            var on = i < colors.Length && ColorUtility.ToHtmlStringRGB(colors[i]) == ColorUtility.ToHtmlStringRGB(current);
            child.Border(on ? UkTheme.Gold : Color.black, 3);
            i++;
        }
    }

    static Label Heading(string text)
    {
        var label = UkControls.Text(text, 15, UkTheme.Text, heading: true);
        label.style.marginTop = 20;
        label.style.marginBottom = 8;
        return label;
    }

    static bool Do(Action action)
    {
        action();
        return true;
    }

    // ================================================================= open

    void Load(ItemDrop.ItemData item, bool canEdit, Action<ItemDrop.ItemData>? onSave, bool owner, Action? onTake)
    {
        if (_read.IsOpen || _full.IsOpen)
            Commit();
        _read.CloseSilent();
        _full.CloseSilent();
        _hosted = false;
        _item = item;
        _canEdit = canEdit;
        _owner = owner;
        _onSave = onSave;
        _onTake = onTake;
        _editing = false;
        _pages = BookData.GetPages(item);
        _spread = 0;
    }

    void OpenRead()
    {
        _fullMode = false;
        _editing = false;
        Render();
        _read.Open();
        _read.Body.schedule.Execute(() => _read.Body.Focus()).StartingIn(30);
    }

    void OpenFull(bool bookTab)
    {
        _fullMode = true;
        _editing = true;
        _loading = true;
        _title.SetValue(CustomTitle(_item!));
        _readable.SetValue(BookData.Readable(_item!));
        _takeable.SetValue(BookData.GetFlag(_item!, BookData.KeyTake, false));
        _loading = false;
        _placedOptions.Show(_owner);
        _fullTake.Show(_onTake != null);
        RefreshBookView();
        SelectTab(bookTab);
        if (_hosted)
            _full.ShowTabs(HostTabId);
        else
            _full.HideTabs();
        _full.Open();
    }

    /// <summary>The title as typed (empty when the book has none yet), not the "Untitled book" stand-in.</summary>
    static string CustomTitle(ItemDrop.ItemData item) =>
        item.m_customData != null && item.m_customData.TryGetValue(DrakeModsLibs.Data.DrakeCustomDataKeys.Rename, out var t) ? t : "";

    void SelectTab(bool book)
    {
        Commit();
        _pagesView.Show(!book);
        _bookView.Show(book);
        UkControls.SetKind(_pagesTab, book ? UkButtonKind.Ghost : UkButtonKind.Selected);
        UkControls.SetKind(_bookTab, book ? UkButtonKind.Selected : UkButtonKind.Ghost);
        _bookTab.Show(_canEdit || _owner);
        if (!book)
            Render();
        UpdateHeader();
    }

    void UpdateHeader()
    {
        if (_item == null)
            return;
        var title = BookData.DisplayTitle(_item);
        var pages = _pages.Count + (_pages.Count == 1 ? " page" : " pages");
        _full.SetHeader(title, pages);
        _readTitle.text = title;
        var author = _item.m_crafterName;
        _readByline.text = string.IsNullOrEmpty(author) ? pages : "by " + author + "  ·  " + pages;
    }

    // ================================================================= pages

    void Render()
    {
        if (_item == null)
            return;
        if (_pages.Count == 0)
            _pages.Add("");
        _spread = Mathf.Clamp(_spread - _spread % 2, 0, Math.Max(0, (_pages.Count - 1) / 2 * 2));

        var spread = _fullMode ? _fullSpread : _readSpread;
        var editors = _fullMode ? _fullEditors : _readEditors;
        var font = _fullMode ? FullFont : ReadFont;
        spread.SetCover(BookData.GetColor(_item));
        spread.SetEdges(_spread, Math.Max(0, _pages.Count - _spread - 2));

        var writing = _fullMode || _editing && _canEdit;
        editors.Toolbar.Root.Show(writing);
        // Writing in the read view: the formatting bar takes the title's room, so the book keeps its place on screen.
        _readTitle.Show(!_editing);
        _readByline.Show(!_editing);
        if (writing)
        {
            editors.Show(spread, _spread, _pages);
        }
        else
        {
            BookSpread.FillRead(spread.Left, _spread < _pages.Count ? _pages[_spread] : "", _spread + 1, font, leftPage: true);
            if (_spread + 1 < _pages.Count)
                BookSpread.FillRead(spread.Right, _pages[_spread + 1], _spread + 2, font, leftPage: false);
            else
                spread.Right.Clear();
        }

        var last = Math.Min(_spread + 2, _pages.Count);
        var count = last > _spread + 1 ? $"Pages {_spread + 1}–{last} of {_pages.Count}" : $"Page {_spread + 1} of {_pages.Count}";
        _readCount.text = _fullCount.text = count;
        foreach (var prev in new[] { _readPrev, _fullPrev })
            UkControls.SetButtonEnabled(prev, _spread > 0);
        foreach (var next in new[] { _readNext, _fullNext })
            UkControls.SetButtonEnabled(next, _spread + 2 < _pages.Count);

        _readEdit.text = _editing ? "Done" : "Edit";
        UkControls.SetKind(_readEdit, _editing ? UkButtonKind.Primary : UkButtonKind.Secondary);
        _readEdit.Show(_canEdit);
        _readAdd.Show(_canEdit && _editing);
        _readDelete.Show(_canEdit && _editing);
        _readFull.Show(_canEdit || _owner);
        _readTake.Show(_onTake != null && !_editing);
        foreach (var add in new[] { _readAdd, _fullAdd })
            UkControls.SetButtonEnabled(add, _pages.Count < BookData.MaxPages);
        foreach (var del in new[] { _readDelete, _fullDelete })
            UkControls.SetButtonEnabled(del, _pages.Count > 1 || _pages[0].Length > 0);
        UpdateHeader();
    }

    /// <summary>A page typed into (index into <see cref="_pages"/>); a page past the end is a new one.</summary>
    internal void OnPageTyped(int index, string raw)
    {
        if (!_canEdit && !_owner || index < 0 || index >= BookData.MaxPages)
            return;
        while (_pages.Count <= index)
            _pages.Add("");
        _pages[index] = raw;
    }

    void OnReadKey(KeyDownEvent e)
    {
        if (_editing)
            return;
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow:
            case KeyCode.A:
                Turn(-1);
                e.StopPropagation();
                break;
            case KeyCode.RightArrow:
            case KeyCode.D:
                Turn(1);
                e.StopPropagation();
                break;
        }
    }

    /// <summary>Turn a spread (two pages) at a time.</summary>
    void Turn(int direction)
    {
        Commit();
        _spread = Mathf.Clamp(_spread + direction * 2, 0, Math.Max(0, (_pages.Count - 1) / 2 * 2));
        Render();
    }

    void ToggleEdit()
    {
        if (!_canEdit)
            return;
        Commit();
        _editing = !_editing;
        Render();
        if (!_editing)
            _read.Body.Focus();
    }

    void AddPage()
    {
        if (_pages.Count >= BookData.MaxPages)
            return;
        Commit();
        _pages.Add("");
        _spread = (_pages.Count - 1) / 2 * 2;
        Commit();
        Render();
        (_fullMode ? _fullEditors : _readEditors).FocusPage(_pages.Count - 1);
    }

    /// <summary>Removes the page last typed in (or the right-hand page of the spread).</summary>
    void DeletePage()
    {
        var editors = _fullMode ? _fullEditors : _readEditors;
        var index = Mathf.Clamp(editors.LastFocused >= 0 ? editors.LastFocused : Math.Min(_spread + 1, _pages.Count - 1), 0, _pages.Count - 1);
        _pages.RemoveAt(index);
        if (_pages.Count == 0)
            _pages.Add("");
        Commit();
        Render();
    }

    void DoTake()
    {
        Commit();
        var take = _onTake;
        _read.CloseSilent();
        _full.CloseSilent();
        take?.Invoke();
    }

    // ================================================================= Book tab

    void RefreshBookView()
    {
        if (_item == null)
            return;
        var current = BookData.GetColor(_item);
        var size = BookData.GetSize(_item);
        var pattern = BookLook.GetPattern(_item);
        var accent = BookLook.GetAccent(_item);
        var ink = BookLook.GetInk(_item);
        MarkSwatches(_swatches, BookData.CoverColors, current);
        MarkSwatches(_accents, BookLook.Accents, accent);
        MarkSwatches(_inks, BookLook.Inks, ink);
        var p = 0;
        foreach (var tile in _patterns.Children())
        {
            tile.style.backgroundImage = new StyleBackground(BookLook.Cover(current, (BookPattern)p, accent));
            tile.Border((BookPattern)p == pattern ? UkTheme.Gold : Color.black, (BookPattern)p == pattern ? 3 : 2);
            p++;
        }

        for (var s = 0; s < 3; s++)
            UkControls.SetKind(_sizes[s], (int)size == s ? UkButtonKind.Selected : UkButtonKind.Secondary);

        var rim = Color.Lerp(current, Color.black, 0.55f);
        _coverPreview.Border(rim, 3);
        _spinePreview.Border(rim, 3);
        _spinePreview.style.backgroundImage = new StyleBackground(BookLook.Spine(current, pattern, accent));
        var shown = BookData.Get(_item, BookData.KeyCover);
        var pic = string.IsNullOrEmpty(shown) ? null : BookCovers.Texture(shown);
        _coverPreview.style.backgroundImage = new StyleBackground(pic != null ? pic : BookLook.Cover(current, pattern, accent));
        _coverTitle.text = pic != null ? "" : BookData.DisplayTitle(_item);
        _coverTitle.style.color = ink;
        _spineTitle.text = BookData.DisplayTitle(_item);
        _spineTitle.style.color = ink;
        // Size shows as the preview's height, the way the three books stand on a shelf.
        var height = size switch { BookSize.Small => 270f, BookSize.Large => 340f, _ => 305f };
        _coverPreview.style.height = _spinePreview.style.height = height;
        _coverPreview.style.width = height * 0.73f;
        _spineTitle.style.width = height - 20f;

        _pictures.Clear();
        var chosen = BookData.Get(_item, BookData.KeyCover);
        void AddPicture(string label, string name)
        {
            var button = UkControls.MakeButton(label, () => OnPicture(name), chosen == name ? UkButtonKind.Selected : UkButtonKind.Secondary);
            button.style.marginRight = 6;
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
        Render();
    }

    void OnPattern(BookPattern pattern)
    {
        if (_item == null || !_canEdit)
            return;
        BookLook.SetPattern(_item, pattern);
        Save();
        RefreshBookView();
    }

    void OnAccent(Color color)
    {
        if (_item == null || !_canEdit)
            return;
        BookLook.SetAccent(_item, color);
        Save();
        RefreshBookView();
    }

    void OnInk(Color color)
    {
        if (_item == null || !_canEdit)
            return;
        BookLook.SetInk(_item, color);
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

    /// <summary>Write the pages to the item and tell the placed piece or shelf, if there is one.</summary>
    void Commit()
    {
        if (_item == null || !_canEdit && !_owner)
            return;
        if (_canEdit)
            BookData.SetPages(_item, _pages);
        _onSave?.Invoke(_item);
    }

    void Save() => _onSave?.Invoke(_item!);

    /// <summary>The page look on its own (the paper reader uses it): the book paper, smooth.</summary>
    internal static VisualElement Page(Texture2D? texture = null)
    {
        var page = new VisualElement().Column().Grow();
        page.style.flexBasis = 0;
        page.style.backgroundColor = new Color(0.82f, 0.74f, 0.57f);
        var paper = texture != null ? texture : BookSpread.PageTexture();
        if (paper != null)
            page.style.backgroundImage = new StyleBackground(paper);
        page.Pad(40, 52);
        return page;
    }

    /// <summary>
    /// Caret, selection and clicks for a page whose shown text has several sizes. The text layout of the shown label (Unity's
    /// TextHandle, reached by reflection: it is internal) gives where each visible character sits; the field's caret index (counted in
    /// visible characters, like the layout) says where to draw. A click is turned into the character under the pointer.
    /// </summary>
    sealed class PageCaret
    {
        static readonly System.Reflection.PropertyInfo? HandleProp =
            typeof(TextElement).GetProperty("uitkTextHandle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        static System.Reflection.MethodInfo? _positionOf;
        static System.Reflection.MethodInfo? _indexAt;

        readonly UkField _field;
        readonly Label _ink;
        readonly int _fontSize;
        readonly VisualElement _caret;
        readonly VisualElement _selection;
        bool _focused;
        int _downIndex = -1;
        float _blinkFrom;
        int _lastCursor = -1;

        internal PageCaret(UkField field, Label ink, int fontSize)
        {
            _field = field;
            _ink = ink;
            _fontSize = fontSize;
            var layer = ink.parent ?? ink;
            _selection = new VisualElement { pickingMode = PickingMode.Ignore };
            _selection.style.position = Position.Absolute;
            _selection.style.left = _selection.style.top = _selection.style.right = _selection.style.bottom = 0;
            layer.Insert(0, _selection);
            _caret = new VisualElement { pickingMode = PickingMode.Ignore }.Size(width: 2).Fill(BookSpread.Ink);
            _caret.style.position = Position.Absolute;
            _caret.Show(false);
            layer.Add(_caret);

            var input = field.Input;
            input.RegisterCallback<FocusInEvent>(_ =>
            {
                _focused = true;
                _blinkFrom = Time.realtimeSinceStartup;
            });
            input.RegisterCallback<FocusOutEvent>(_ =>
            {
                _focused = false;
                _caret.Show(false);
                _selection.Clear();
            });
            input.RegisterCallback<PointerDownEvent>(e => _downIndex = IndexAt(e.position), TrickleDown.TrickleDown);
            input.RegisterCallback<PointerUpEvent>(e =>
            {
                var up = IndexAt(e.position);
                if (up < 0)
                    return;
                var from = _downIndex >= 0 ? _downIndex : up;
                _downIndex = -1;
                // After the box's own handling, which placed the caret by its one-size layout.
                input.schedule.Execute(() => input.SelectRange(from, up)).StartingIn(0);
            });
            input.schedule.Execute(Draw).Every(40);
        }

        object? Handle() => HandleProp?.GetValue(_ink);

        /// <summary>Where visible character <paramref name="index"/> starts, on its line's baseline, in the layer's coordinates.</summary>
        Vector2? PositionOf(int index)
        {
            var handle = Handle();
            if (handle == null)
                return null;
            _positionOf ??= handle.GetType().GetMethod("GetCursorPositionFromStringIndexUsingLineHeight", new[] { typeof(int), typeof(bool), typeof(bool) });
            if (_positionOf == null)
                return null;
            try
            {
                var p = (Vector2)_positionOf.Invoke(handle, new object[] { index, false, true });
                return p + _ink.contentRect.min + new Vector2(_ink.layout.x, _ink.layout.y);
            }
            catch
            {
                return null;
            }
        }

        int IndexAt(Vector2 world)
        {
            var handle = Handle();
            if (handle == null)
                return -1;
            _indexAt ??= handle.GetType().GetMethod("GetCursorIndexFromPosition", new[] { typeof(Vector2), typeof(bool) });
            if (_indexAt == null)
                return -1;
            try
            {
                var local = _ink.WorldToLocal(world) - _ink.contentRect.min;
                var count = (_field.Input.value ?? "").Length;
                return Mathf.Clamp((int)_indexAt.Invoke(handle, new object[] { local, true }), 0, count);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>Line height at the character before <paramref name="index"/>: the page font scaled by its size tag.</summary>
        float LineHeight(int index)
        {
            var model = RichModel.FromRaw(_field.Value);
            var scale = 1f;
            if (model.Length > 0)
            {
                var at = Mathf.Clamp(index - 1, 0, model.Length - 1);
                var size = model.Styles[at].Size;
                if (!string.IsNullOrEmpty(size) && size!.EndsWith("%")
                    && float.TryParse(size.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                    scale = pct / 100f;
            }

            return _fontSize * scale * 1.18f;
        }

        void Draw()
        {
            if (!_focused)
                return;
            var input = _field.Input;
            var cursor = input.cursorIndex;
            var anchor = input.selectIndex;
            _selection.Clear();
            if (anchor != cursor)
            {
                int a = Math.Min(anchor, cursor), b = Math.Max(anchor, cursor);
                // One highlight per line: walk the characters and start a new box whenever the baseline moves.
                float? startX = null;
                var lastX = 0f;
                var lineY = 0f;
                var height = 0f;
                for (var i = a; i <= b; i++)
                {
                    if (PositionOf(i) is not { } p)
                        break;
                    if (startX == null || Mathf.Abs(p.y - lineY) > 2f)
                    {
                        if (startX is { } x0)
                            Box(x0, lineY, lastX, height);
                        startX = p.x;
                        lineY = p.y;
                        height = 0f;
                    }

                    lastX = p.x;
                    height = Mathf.Max(height, LineHeight(i + 1));
                }

                if (startX is { } x1)
                    Box(x1, lineY, lastX, height);
            }

            var plain = input.value ?? "";
            if (cursor > 0 && cursor <= plain.Length && plain[cursor - 1] == '\n' && (cursor == plain.Length || plain[cursor] == '\n')
                && PositionOf(cursor - 1) is { } before)
            {
                // Right after Enter on an empty line the layout has no character there yet: the caret goes to the start of the next
                // line, one line below the line break.
                var lineH = LineHeight(cursor);
                if (cursor != _lastCursor)
                {
                    _lastCursor = cursor;
                    _blinkFrom = Time.realtimeSinceStartup;
                }

                _caret.style.left = _ink.layout.x + _ink.contentRect.xMin;
                _caret.style.top = before.y;
                _caret.style.height = lineH;
                _caret.Show((Time.realtimeSinceStartup - _blinkFrom) % 1.0f < 0.6f);
            }
            else if (PositionOf(cursor) is { } c)
            {
                if (cursor != _lastCursor)
                {
                    _lastCursor = cursor;
                    _blinkFrom = Time.realtimeSinceStartup;
                }

                var h = LineHeight(cursor);
                _caret.style.left = c.x;
                _caret.style.top = c.y - h;
                _caret.style.height = h;
                // Blinks like a text box, steady right after it moves.
                _caret.Show((Time.realtimeSinceStartup - _blinkFrom) % 1.0f < 0.6f);
            }
        }

        void Box(float x0, float baseline, float x1, float height)
        {
            if (x1 <= x0)
                return;
            var box = new VisualElement { pickingMode = PickingMode.Ignore }.Fill(new Color(0.55f, 0.40f, 0.18f, 0.35f));
            box.style.position = Position.Absolute;
            box.style.left = x0;
            box.style.top = baseline - height;
            box.style.width = x1 - x0;
            box.style.height = height;
            _selection.Add(box);
        }
    }

    /// <summary>
    /// The two writable pages of a spread: rich fields drawn straight on the paper (no box), with the suite's formatting bar.
    /// Typing goes to <see cref="OnPageTyped"/>. A right page past the end shows an empty sheet that becomes a new page when typed in.
    /// </summary>
    sealed class PageEditors
    {
        readonly BookReaderPanel _owner;
        readonly UkField _left;
        readonly UkField _right;
        UkField? _active;

        /// <summary>Text sizes A− / A+ step through, one per click (100% is no size tag).</summary>
        static readonly int[] Ladder = { 60, 70, 80, 90, 100, 115, 130, 150, 175, 200 };
        int _leftIndex;
        int _rightIndex;
        bool _filling;

        public UkRichToolbar Toolbar { get; }
        public int LastFocused { get; private set; } = -1;

        public PageEditors(BookReaderPanel owner, UkTooltip tips, VisualElement pickerHost, int fontSize, float pageHeight)
        {
            _owner = owner;
            _left = Make(fontSize, pageHeight, () => _leftIndex);
            _right = Make(fontSize, pageHeight, () => _rightIndex);
            Toolbar = new UkRichToolbar(tips, pickerHost, () => RenameitConfig.RecentColors, v => RenameitConfig.RecentColors = v,
                ("Left page", _left), ("Right page", _right));
            // The toolbar takes over GotFocus; also remember which page was last written in (Remove page acts on it).
            var leftFocus = _left.GotFocus;
            _left.GotFocus = () =>
            {
                leftFocus?.Invoke();
                LastFocused = _leftIndex;
                _active = _left;
            };
            var rightFocus = _right.GotFocus;
            _right.GotFocus = () =>
            {
                rightFocus?.Invoke();
                LastFocused = _rightIndex;
                _active = _right;
            };
            ReplaceSizeButtons();
        }

        /// <summary>
        /// The shared toolbar's two size buttons switch between one small and one large size. On a page you want to click A+ a few times,
        /// so they are swapped for buttons that step through <see cref="Ladder"/>.
        /// </summary>
        void ReplaceSizeButtons()
        {
            if (Toolbar.Root.childCount == 0)
                return;
            var styles = Toolbar.Root[0];
            var buttons = styles.Query<Button>().ToList();
            if (buttons.Count < 4)
                return;
            var smaller = buttons[2];
            var larger = buttons[3];
            var at = styles.IndexOf(smaller);
            smaller.Show(false);
            larger.Show(false);
            var down = UkControls.MakeButton("A−", () => Step(-1), UkButtonKind.Ghost);
            var up = UkControls.MakeButton("A+", () => Step(1), UkButtonKind.Ghost);
            down.tooltip = "Smaller (click again for smaller still)";
            up.tooltip = "Bigger (click again for bigger still)";
            foreach (var b in new[] { down, up })
            {
                b.style.height = 34;
                b.style.paddingLeft = b.style.paddingRight = 8;
            }

            down.style.fontSize = 13;
            up.style.fontSize = 18;
            styles.Insert(at, up);
            styles.Insert(at, down);
        }

        void Step(int direction)
        {
            var field = _active ?? _left;
            var current = 100;
            var size = field.CurrentSelection.Size;
            if (!string.IsNullOrEmpty(size) && size!.EndsWith("%")
                && int.TryParse(size.TrimEnd('%'), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                current = pct;
            var index = 0;
            for (var i = 0; i < Ladder.Length; i++)
                if (Math.Abs(Ladder[i] - current) < Math.Abs(Ladder[index] - current))
                    index = i;
            index = Mathf.Clamp(index + direction, 0, Ladder.Length - 1);
            var next = Ladder[index];
            field.ApplyRich(RichOp.Size, next == 100 ? null : next.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%");
        }

        UkField Make(int fontSize, float pageHeight, Func<int> index)
        {
            var field = new UkField("", multiline: true, rows: 12);
            field.EnableRich();
            field.SetMax(BookData.MaxPageChars);
            if (field.Root.childCount > 0)
                field.Root[0].Show(false); // no title or counter: it is a page, not a form
            field.Root.style.flexGrow = 1;
            field.Line.style.flexGrow = 1;
            var box = field.Input.Q<VisualElement>("unity-text-input") ?? field.Input;
            box.style.backgroundColor = Color.clear;
            box.Border(Color.clear, 0);
            box.style.paddingTop = box.style.paddingBottom = box.style.paddingLeft = box.style.paddingRight = 0;
            box.style.fontSize = fontSize;
            box.style.minHeight = pageHeight - 140f;
            if (field.Line.Q<Label>() is { } inked)
            {
                // The coloured copy that shows (the box only keeps the caret): same size and inset as the box, in ink.
                inked.style.fontSize = fontSize;
                inked.style.color = BookSpread.Ink;
                inked.style.marginLeft = inked.style.marginRight = inked.style.marginTop = 0;
            }

            // The box underneath lays its (invisible) text out at one size; the page shows sizes. So the page draws its own caret and
            // selection from the shown text's layout (PageCaret), and the box's own are hidden.
            field.Input.textSelection.cursorColor = Color.clear;
            field.Input.textSelection.selectionColor = Color.clear;
            var ink = field.Line.Q<Label>();
            if (ink != null)
                new PageCaret(field, ink, fontSize);
            field.Changed = _ =>
            {
                ShowStyles(field, ink);
                if (!_filling)
                    _owner.OnPageTyped(index(), field.Value);
            };
            return field;
        }

        /// <summary>
        /// The shared field draws only colours while you type. A page shows sizes and bold too, so you can see what A− / A+ did; the
        /// caret can sit a little off on a line with resized words.
        /// </summary>
        static void ShowStyles(UkField field, Label? ink)
        {
            if (ink != null)
                ink.text = field.Value;
        }

        /// <summary>Put the fields on <paramref name="spread"/>'s pages for the spread starting at <paramref name="first"/>.</summary>
        public void Show(BookSpread spread, int first, List<string> pages)
        {
            _leftIndex = first;
            _rightIndex = first + 1;
            _filling = true;
            _left.SetValue(first < pages.Count ? pages[first] : "");
            _right.SetValue(first + 1 < pages.Count ? pages[first + 1] : "");
            ShowStyles(_left, _left.Line.Q<Label>());
            ShowStyles(_right, _right.Line.Q<Label>());
            _filling = false;
            Place(spread.Left, _left, first + 1, leftPage: true);
            Place(spread.Right, _right, first + 2, leftPage: false);
            if (first + 1 >= pages.Count)
                _right.Input.tooltip = "Write here to add a page";
        }

        static void Place(VisualElement content, UkField field, int number, bool leftPage)
        {
            content.Clear();
            content.Add(field.Root);
            content.Add(BookSpread.PageNumber(number, leftPage));
        }

        internal void TestSet(string raw, int cursor, int anchor)
        {
            _left.SetValue(raw);
            ShowStyles(_left, _left.Line.Q<Label>());
            _owner.OnPageTyped(_leftIndex, _left.Value);
            var input = _left.Input;
            input.Focus();
            input.schedule.Execute(() => input.SelectRange(anchor, cursor)).StartingIn(60);
        }

        public void FocusPage(int index)
        {
            var field = index == _rightIndex ? _right : _left;
            field.Input.schedule.Execute(() => field.Input.Focus()).StartingIn(30);
        }
    }
}
