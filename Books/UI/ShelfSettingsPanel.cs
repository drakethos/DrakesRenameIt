using System;
using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// Shift+Use on a shelf. Owner only: whether the shelf may hold things other than books, and how each row lays its things out
/// (Books, Slots, Free). A Free row gets a board: drag things along the shelf, and turn, lift or size the selected one.
/// Slots and Free need the server and the owner to allow more than books.
/// </summary>
internal sealed class ShelfSettingsPanel
{
    static ShelfSettingsPanel? _instance;
    readonly UkWindow _window;
    readonly UkSwitch _nonBooks;
    readonly Label _note;
    readonly VisualElement _rows;
    BookShelfBehaviour? _shelf;
    bool _owner;
    bool _loading;
    string? _selectedKey;

    public static void Show(BookShelfBehaviour shelf, Player player)
    {
        _instance ??= new ShelfSettingsPanel();
        _instance.Open(shelf);
    }

    internal static void CloseWindow() => _instance?._window.CloseSilent();

    ShelfSettingsPanel()
    {
        _window = new UkWindow("drakes_shelf_settings", 980f);
        _window.SetHeader("Shelf", "Settings");
        _nonBooks = new UkSwitch("Allow things other than books", "Knick-knacks stand on the shelf too, and rows can be Slots or Free");
        _nonBooks.Changed = on =>
        {
            if (_loading)
                return;
            _shelf?.SetNonBooks(on);
            // The write lands on the next refresh; rebuild the rows once it has.
            _nonBooks.Root.schedule.Execute(BuildRows).StartingIn(250);
        };
        _window.Body.Add(_nonBooks.Root);
        _note = UkControls.Text("", 14, UkTheme.TextMuted);
        _note.style.marginTop = 8;
        _window.Body.Add(_note);
        _rows = new VisualElement().Column();
        _rows.style.marginTop = 14;
        _window.Body.Add(_rows);
    }

    void Open(BookShelfBehaviour shelf)
    {
        _shelf = shelf;
        _selectedKey = null;
        var piece = shelf.GetComponent<Piece>();
        _owner = piece != null && piece.IsCreator();
        var serverAllows = RenameitConfig.ShelfAllowNonBooks;
        _loading = true;
        _nonBooks.SetValue(shelf.AllowsNonBooks());
        _loading = false;
        _nonBooks.Root.SetEnabled(_owner && serverAllows);
        _note.text = !serverAllows
            ? "This server only allows books on shelves."
            : _owner ? "Things already on the shelf stay if you turn this off; no new ones fit." : "Only whoever built this shelf can change this.";
        _window.ShowFooter(false);
        BuildRows();
        _window.Open();
    }

    void BuildRows()
    {
        _rows.Clear();
        if (_shelf == null)
            return;
        var allowed = _shelf.AllowsNonBooks();
        _rows.Add(UkControls.Text("Rows", 16, UkTheme.Text, heading: true));
        if (!allowed)
        {
            _rows.Add(UkControls.Text("Every row stands the books side by side. Allow other things above to choose Slots or Free.", 14, UkTheme.TextMuted));
            return;
        }

        for (var row = 0; row < _shelf.ShelfRows; row++)
            _rows.Add(BuildRow(row));
    }

    VisualElement BuildRow(int shelfRow)
    {
        var shelf = _shelf!;
        var box = new VisualElement().Column();
        box.Pad(10, 12);
        box.Margin(top: 8);
        box.Fill(UkTheme.Raised).Round(UkTheme.Radius).Border(UkTheme.Line, 1);

        var head = new VisualElement().Row();
        var name = shelf.ShelfRows == 1 ? "Shelf" : shelfRow == 0 ? "Top row" : $"Row {shelfRow + 1}";
        head.Add(UkControls.Text(name, 15, UkTheme.Text, heading: true));
        head.Add(UkControls.Spacer());
        var mode = shelf.GetMode(shelfRow);
        foreach (var m in new[] { ShelfRowMode.Books, ShelfRowMode.Slots, ShelfRowMode.Free })
        {
            var mm = m;
            var button = UkControls.MakeButton(mm.ToString(), () => ChooseMode(shelfRow, mm), mm == mode ? UkButtonKind.Primary : UkButtonKind.Secondary);
            button.SetEnabled(_owner);
            head.Add(button);
        }

        box.Add(head);
        var hint = mode switch
        {
            ShelfRowMode.Slots => "Each thing stands in the gap of its slot. Drag things between slots in the shelf window.",
            ShelfRowMode.Free => "Drag things along the shelf, then turn, lift or size the selected one.",
            _ => "Books stand side by side in slot order.",
        };
        var hintLabel = UkControls.Text(hint, 13, UkTheme.TextMuted);
        hintLabel.style.marginTop = 4;
        box.Add(hintLabel);
        if (mode == ShelfRowMode.Free)
            box.Add(BuildBoard(shelfRow));
        return box;
    }

    void ChooseMode(int shelfRow, ShelfRowMode mode)
    {
        if (_shelf == null || !_owner)
            return;
        _shelf.SetMode(shelfRow, mode);
        // Rebuild after the write has been read back.
        _rows.schedule.Execute(BuildRows).StartingIn(200);
    }

    // ---- the Free board

    const float BoardWidth = 880f;
    const float BoardHeight = 110f;
    const float TokenSize = 44f;

    VisualElement BuildBoard(int shelfRow)
    {
        var shelf = _shelf!;
        var wrap = new VisualElement().Column();
        wrap.style.marginTop = 10;

        var board = new VisualElement();
        board.Size(width: BoardWidth, height: BoardHeight).Fill(UkTheme.Field).Round(UkTheme.Radius).Border(UkTheme.LineSoft, 1);
        board.style.position = Position.Relative;
        wrap.Add(board);

        var items = shelf.RowItems(shelfRow);
        var tokens = new Dictionary<string, VisualElement>();
        if (items.Count == 0)
            board.Add(UkControls.Text("Nothing on this row yet. Put things in the shelf first, then arrange them here.", 14, UkTheme.TextMuted));

        foreach (var item in items)
        {
            var key = BookShelfBehaviour.PoseKey(item);
            var pose = shelf.GetPose(item);
            var token = new VisualElement().Size(width: TokenSize, height: TokenSize).Round(6).Border(Color.black, 2);
            token.Fill(BookItem.IsBook(item) ? BookData.GetColor(item) : new Color(0.45f, 0.42f, 0.36f));
            token.style.position = Position.Absolute;
            token.style.bottom = 8;
            token.style.left = XToPixels(shelf, pose.X) - TokenSize / 2f;
            var label = UkControls.Text(Short(item), 12, UkTheme.Text, wrap: false);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.pickingMode = PickingMode.Ignore;
            token.Add(label);
            tokens[key] = token;
            board.Add(token);
            RegisterDrag(token, board, shelf, item, () => Select(tokens, key));
        }

        if (_selectedKey != null && tokens.ContainsKey(_selectedKey))
            Select(tokens, _selectedKey);

        // Turn, lift and size for the selected thing.
        var controls = new VisualElement().Row();
        controls.style.marginTop = 8;
        void Add(string text, Func<ShelfPose, ShelfPose> change)
        {
            var button = UkControls.MakeButton(text, () => Adjust(shelfRow, change));
            button.SetEnabled(_owner);
            controls.Add(button);
        }

        Add("Turn left", p => { p.Yaw -= 15f; return p; });
        Add("Turn right", p => { p.Yaw += 15f; return p; });
        Add("Lift down", p => { p.Lift -= 0.02f; return p; });
        Add("Lift up", p => { p.Lift += 0.02f; return p; });
        Add("Smaller", p => { p.Scale -= 0.1f; return p; });
        Add("Bigger", p => { p.Scale += 0.1f; return p; });
        Add("Reset", p => ShelfPose.At(p.X));
        wrap.Add(controls);
        return wrap;
    }

    static float XToPixels(BookShelfBehaviour shelf, float x) => (x / shelf.Usable + 0.5f) * BoardWidth;

    static float PixelsToX(BookShelfBehaviour shelf, float px) => (Mathf.Clamp01(px / BoardWidth) - 0.5f) * shelf.Usable;

    static string Short(ItemDrop.ItemData item)
    {
        var name = BookItem.IsBook(item) ? BookData.DisplayTitle(item) : Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
        return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, Math.Min(3, name.Length));
    }

    void Select(Dictionary<string, VisualElement> tokens, string key)
    {
        _selectedKey = key;
        foreach (var kv in tokens)
        {
            var on = kv.Key == key;
            kv.Value.style.borderTopColor = kv.Value.style.borderBottomColor = kv.Value.style.borderLeftColor = kv.Value.style.borderRightColor =
                on ? UkTheme.Gold : Color.black;
        }
    }

    void RegisterDrag(VisualElement token, VisualElement board, BookShelfBehaviour shelf, ItemDrop.ItemData item, Action select)
    {
        var dragging = false;
        token.RegisterCallback<PointerDownEvent>(e =>
        {
            select();
            if (!_owner)
                return;
            dragging = true;
            token.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        token.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!dragging)
                return;
            var local = board.WorldToLocal(e.position);
            token.style.left = Mathf.Clamp(local.x, 0f, BoardWidth) - TokenSize / 2f;
        });
        token.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!dragging)
                return;
            dragging = false;
            token.ReleasePointer(e.pointerId);
            var local = board.WorldToLocal(e.position);
            var pose = shelf.GetPose(item);
            pose.X = PixelsToX(shelf, local.x);
            shelf.SetPose(item, pose);
        });
    }

    void Adjust(int shelfRow, Func<ShelfPose, ShelfPose> change)
    {
        if (_shelf == null || !_owner || _selectedKey == null)
            return;
        foreach (var item in _shelf.RowItems(shelfRow))
        {
            if (BookShelfBehaviour.PoseKey(item) != _selectedKey)
                continue;
            _shelf.SetPose(item, change(_shelf.GetPose(item)));
            return;
        }
    }
}
