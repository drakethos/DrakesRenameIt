using System;
using System.Collections.Generic;
using DrakeModsLibs.Forge;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// The book window: two pages side by side on the paper texture, &lt; and &gt; to turn, and (for the book's maker) Edit, where you
/// type straight into the pages. Changes are written to the item when you turn a page, leave edit mode, or close.
/// </summary>
internal sealed class BookReaderPanel
{
    static readonly Color PaperTone = new(0.85f, 0.78f, 0.63f);
    static readonly Color Ink = new(0.23f, 0.16f, 0.10f);
    static BookReaderPanel? _instance;

    readonly UkWindow _window;
    readonly VisualElement _spread;
    readonly VisualElement _left;
    readonly VisualElement _right;
    readonly Label _count;
    readonly Button _prev;
    readonly Button _next;
    readonly Button _edit;
    readonly Button _addPage;

    ItemDrop.ItemData? _item;
    List<string> _pages = new();
    int _spreadStart;
    bool _editing;
    bool _canEdit;
    TextField? _leftField;
    TextField? _rightField;

    readonly VisualElement _options;
    readonly UkSwitch _readable;
    readonly UkSwitch _takeable;
    readonly Button _take;
    Action<ItemDrop.ItemData>? _onSave;
    Action? _onTake;
    bool _owner;

    public static bool IsOpen => _instance != null && _instance._window.IsOpen;

    /// <summary>
    /// Open the book. <paramref name="canEdit"/> shows the Edit button (the maker's own book). A placed book passes
    /// <paramref name="onSave"/> (called with the edited copy, to write back to the piece), <paramref name="owner"/> (shows its settings)
    /// and <paramref name="onTake"/> (shows Take when the player may pick it up).
    /// </summary>
    public static void Show(ItemDrop.ItemData item, bool canEdit, Action<ItemDrop.ItemData>? onSave = null, bool owner = false, Action? onTake = null)
    {
        _instance ??= new BookReaderPanel();
        _instance._onSave = onSave;
        _instance._owner = owner;
        _instance._onTake = onTake;
        _instance.Open(item, canEdit);
    }

    BookReaderPanel()
    {
        _window = new UkWindow("drakes_book_reader", 760f);
        _window.Closed = () => Commit();

        _options = new VisualElement().Row();
        _options.style.marginBottom = 10;
        _readable = new UkSwitch("Readers can open", "Others may read it, not only you");
        _readable.Changed = on => SetFlag(BookData.KeyReadable, on);
        _takeable = new UkSwitch("Allow take", "Others may pick it up");
        _takeable.Changed = on => SetFlag(BookData.KeyTake, on);
        _options.Add(_readable.Root);
        _options.Add(_takeable.Root);
        _window.Body.Add(_options);

        _spread = new VisualElement().Row(Align.Stretch);
        _spread.style.minHeight = 380;
        _spread.Round(4).Border(new Color(0.29f, 0.21f, 0.13f), 3);
        var texture = ForgeTextures.Load(typeof(BookReaderPanel).Assembly, "textures/paper_sheet.png");
        _left = Page(texture);
        _right = Page(texture);
        _right.style.borderLeftWidth = 2;
        _right.style.borderLeftColor = new Color(0.66f, 0.57f, 0.42f);
        _spread.Add(_left);
        _spread.Add(_right);
        _window.Body.Add(_spread);

        var foot = _window.Footer;
        _prev = UkControls.MakeButton("<", () => Turn(-2));
        _next = UkControls.MakeButton(">", () => Turn(2));
        _count = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        _edit = UkControls.MakeButton("Edit", ToggleEdit);
        _addPage = UkControls.MakeButton("Add page", AddPage);
        foot.Add(_prev);
        foot.Add(_count);
        foot.Add(_next);
        foot.Add(UkControls.Spacer());
        _take = UkControls.MakeButton("Take", () =>
        {
            Commit();
            var take = _onTake;
            _window.CloseSilent();
            take?.Invoke();
        });
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
            page.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 0.55f);
        }

        page.Pad(18, 20);
        return page;
    }

    void Open(ItemDrop.ItemData item, bool canEdit)
    {
        _item = item;
        _canEdit = canEdit;
        _editing = false;
        _pages = BookData.GetPages(item);
        _spreadStart = 0;
        _window.SetHeader(BookData.DisplayTitle(item), "Book");
        _options.Show(_owner);
        _readable.SetValue(BookData.Readable(item));
        _takeable.SetValue(BookData.GetFlag(item, BookData.KeyTake, false));
        _take.Show(_onTake != null);
        Render();
        _window.Open();
    }

    void Render()
    {
        _left.Clear();
        _right.Clear();
        _leftField = _rightField = null;
        Fill(_left, _spreadStart, ref _leftField);
        Fill(_right, _spreadStart + 1, ref _rightField);

        var end = Math.Min(_spreadStart + 2, _pages.Count);
        _count.text = $"Pages {_spreadStart + 1}-{end} of {_pages.Count}";
        UkControls.SetButtonEnabled(_prev, _spreadStart > 0);
        UkControls.SetButtonEnabled(_next, _spreadStart + 2 < _pages.Count);
        _edit.text = _editing ? "Done" : "Edit";
        _edit.Show(_canEdit);
        _addPage.Show(_canEdit && _editing);
        UkControls.SetButtonEnabled(_addPage, _pages.Count < BookData.MaxPages);
    }

    void Fill(VisualElement page, int index, ref TextField? field)
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
            box.style.backgroundColor = new Color(1f, 1f, 1f, 0.28f);
            box.style.color = Ink;
            box.style.fontSize = 17;
            box.style.minHeight = 330;
            box.style.unityTextAlign = TextAnchor.UpperLeft;
            box.style.whiteSpace = WhiteSpace.Normal;
            box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 8;
            UkFonts.ApplyBody(box);
            tf.RegisterValueChangedCallback(e => _pages[index] = e.newValue ?? "");
            page.Add(tf);
            field = tf;
        }
        else
        {
            var text = UkControls.Text(_pages[index], 17, Ink);
            text.style.whiteSpace = WhiteSpace.Normal;
            UkFonts.ApplyBody(text);
            page.Add(text);
        }

        var number = UkControls.Text((index + 1).ToString(), 12, new Color(0.48f, 0.39f, 0.27f), wrap: false);
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
        // Show the new page on the right when it is odd, else open a fresh spread.
        _spreadStart = (_pages.Count - 1) - ((_pages.Count - 1) % 2);
        Commit();
        Render();
    }

    /// <summary>Write the pages to the item (no-op when nothing was editable).</summary>
    void Commit()
    {
        if (_item == null || !_canEdit && !_owner)
            return;
        if (_canEdit)
            BookData.SetPages(_item, _pages);
        _onSave?.Invoke(_item);
    }

    void SetFlag(string key, bool on)
    {
        if (_item == null || !_owner)
            return;
        BookData.SetFlag(_item, key, on);
        Commit();
    }
}
