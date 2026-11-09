using System;
using System.Collections.Generic;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// The shelf's layout, drawn the way the shop's Layout tab is: whether it may hold more than books, then one card per row with
/// Books / Slots / Free and that row's board.
/// <list type="bullet">
/// <item>Books: spines stand side by side like a real shelf. Drag a spine onto another to swap their places.</item>
/// <item>Slots: every slot has its own spot along the row. Drag onto any slot (empty or not) to move or swap.</item>
/// <item>Free: drag along the row, then turn, lift or size the selected one.</item>
/// </list>
/// It shows as a <b>Shelf</b> tab in LockSmith's Shift+E menu (beside Lock and Name, like Shop / Layout on a display case), and as
/// its own window when LockSmith is not installed.
/// </summary>
internal static class ShelfLayout
{
    internal const string TabId = "renameit.shelf";

    internal static void RegisterTab() =>
        PieceMenuTabs.Register(new PieceMenuTab(TabId, "Shelf", c => RenameitConfig.BooksEnabled && c != null && c.GetComponent<BookShelfBehaviour>() != null,
            ctx =>
            {
                var shelf = ctx.Piece != null ? ctx.Piece.GetComponent<BookShelfBehaviour>() : null;
                if (shelf != null)
                    Build(ctx.Body, shelf, ctx.CanManage || IsOwner(shelf), ctx.Rebuild);
            }, group: "renameit.shelf", isRoot: true));

    internal static bool IsOwner(BookShelfBehaviour shelf)
    {
        var piece = shelf.GetComponent<Piece>();
        var player = Player.m_localPlayer;
        return piece != null && (piece.IsCreator() || player != null && API.RenameitPermission.IsAdminOrVIP(player));
    }

    /// <summary>Fill <paramref name="body"/> with the layout. <paramref name="rebuild"/> redraws it after a change has been saved.</summary>
    internal static void Build(VisualElement body, BookShelfBehaviour shelf, bool canManage, Action rebuild)
    {
        // A write lands on the ZDO / inventory right away, but redraw a moment later so what is read back is the saved state.
        void Later() => body.schedule.Execute(rebuild).StartingIn(150);
        var serverAllows = RenameitConfig.ShelfAllowNonBooks;

        var nonBooks = new UkSwitch("Other things on this shelf", "Knick-knacks may stand here too, not only books");
        nonBooks.SetValue(shelf.AllowsNonBooks());
        if (!canManage || !serverAllows)
            nonBooks.SetEnabled(false, !serverAllows ? "This server only allows books on shelves." : "Only the shelf's owner can change this.");
        nonBooks.Changed = on =>
        {
            shelf.SetNonBooks(on);
            Later();
        };
        nonBooks.Root.style.marginBottom = 14;
        body.Add(nonBooks.Root);

        body.Add(UkControls.Text("ROWS", 12, UkTheme.TextMuted, wrap: false));
        if (!canManage)
            body.Add(UkControls.Text("Only the shelf's owner can change the layout.", 14, UkTheme.TextMuted));
        for (var row = 0; row < shelf.ShelfRows; row++)
            body.Add(BuildRow(shelf, row, canManage, Later));
    }

    static VisualElement BuildRow(BookShelfBehaviour shelf, int shelfRow, bool canManage, Action changed)
    {
        var card = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius + 2).Border(UkTheme.LineSoft).Pad(12, 14);
        card.style.marginTop = 8;

        var head = new VisualElement().Row();
        var name = shelf.ShelfRows == 1 ? "Shelf" : shelfRow == 0 ? "Top row" : shelfRow == shelf.ShelfRows - 1 ? "Bottom row" : $"Row {shelfRow + 1}";
        head.Add(UkControls.Text(name, 16, UkTheme.Text, heading: true, wrap: false));
        head.Add(UkControls.Spacer());
        var mode = shelf.GetMode(shelfRow);
        var segment = new VisualElement().Row().Fill(UkTheme.Raised).Round(UkTheme.Radius).Pad(3);
        foreach (var m in new[] { ShelfRowMode.Books, ShelfRowMode.Slots, ShelfRowMode.Free })
        {
            var mm = m;
            var button = UkControls.MakeButton(mm.ToString(), () =>
            {
                shelf.SetMode(shelfRow, mm);
                changed();
            }, mm == mode ? UkButtonKind.Selected : UkButtonKind.Ghost);
            button.style.height = 34;
            button.SetEnabled(canManage);
            segment.Add(button);
        }

        head.Add(segment);
        card.Add(head);

        var hint = mode switch
        {
            ShelfRowMode.Free => "Drag along the shelf. Select one to turn, lift or size it.",
            ShelfRowMode.Slots => "Each slot has its own spot. Drag onto another slot to move or swap.",
            _ => "Books stand side by side in this order. Drag a spine onto another to swap them.",
        };
        var hintLabel = UkControls.Text(hint, 13, UkTheme.TextMuted);
        hintLabel.style.marginTop = 4;
        card.Add(hintLabel);

        var items = shelf.RowItems(shelfRow);
        if (items.Count == 0)
        {
            var empty = UkControls.Text("Nothing on this row yet. Open the shelf (E) and put books in.", 14, UkTheme.TextMuted);
            empty.style.marginTop = 10;
            card.Add(empty);
            return card;
        }

        card.Add(mode switch
        {
            ShelfRowMode.Free => FreeBoard.Build(shelf, shelfRow, canManage),
            ShelfRowMode.Slots => SlotStrip(shelf, shelfRow, items, canManage, changed),
            _ => SpineStrip(shelf, shelfRow, items, canManage, changed),
        });
        return card;
    }

    // ---- Books: the spines as they stand

    static VisualElement SpineStrip(BookShelfBehaviour shelf, int shelfRow, List<ItemDrop.ItemData> items, bool canManage, Action changed)
    {
        var strip = new VisualElement().Row(Align.FlexEnd).Fill(UkTheme.Field).Round(UkTheme.Radius).Pad(10, 12);
        strip.style.marginTop = 10;
        strip.style.minHeight = 104;
        strip.style.borderBottomWidth = 6;
        strip.style.borderBottomColor = new Color(0.42f, 0.30f, 0.18f); // the shelf board
        foreach (var item in items)
        {
            var token = Spine(item);
            token.userData = shelf.SlotIndex(item);
            token.style.marginRight = 2;
            if (canManage)
                Draggable(token, shelf, shelfRow, item, changed);
            strip.Add(token);
        }

        return strip;
    }

    static VisualElement Spine(ItemDrop.ItemData item)
    {
        if (!BookItem.IsBook(item))
        {
            var tile = UkControls.IconTile(item.GetIcon(), 44);
            tile.style.alignSelf = Align.FlexEnd;
            return tile;
        }

        var size = BookData.GetSize(item);
        var height = size switch { BookSize.Small => 62f, BookSize.Large => 84f, _ => 72f };
        var width = size switch { BookSize.Small => 22f, BookSize.Large => 30f, _ => 26f };
        var color = BookData.GetColor(item);
        var spine = new VisualElement().Size(width: width, height: height).Fill(color).Border(Color.Lerp(color, Color.black, 0.55f), 2).Round(3);
        spine.style.alignItems = Align.Center;
        spine.style.justifyContent = Justify.Center;
        spine.style.overflow = Overflow.Hidden;
        spine.tooltip = BookData.DisplayTitle(item);
        var title = UkControls.Text(BookData.DisplayTitle(item), 11, new Color(0.96f, 0.90f, 0.72f), wrap: false);
        title.style.rotate = new Rotate(90f);
        title.style.width = height - 8f;
        title.style.flexShrink = 0;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        title.pickingMode = PickingMode.Ignore;
        spine.Add(title);
        return spine;
    }

    // ---- Slots: one cell per slot along the row

    static VisualElement SlotStrip(BookShelfBehaviour shelf, int shelfRow, List<ItemDrop.ItemData> items, bool canManage, Action changed)
    {
        var bySlot = new Dictionary<int, ItemDrop.ItemData>();
        foreach (var item in items)
            bySlot[shelf.SlotIndex(item)] = item;

        var strip = new VisualElement().Row();
        strip.style.flexWrap = Wrap.Wrap;
        strip.style.marginTop = 10;
        for (var slot = 0; slot < shelf.SlotsPerShelfRow; slot++)
        {
            var cell = UkControls.Card().Column();
            cell.style.width = 52;
            cell.style.height = 96;
            cell.style.marginRight = 4;
            cell.style.marginBottom = 4;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.FlexEnd;
            cell.style.paddingBottom = 6;
            cell.userData = slot;
            if (bySlot.TryGetValue(slot, out var item))
            {
                var token = Spine(item);
                token.pickingMode = PickingMode.Ignore;
                cell.Add(token);
                if (canManage)
                    Draggable(cell, shelf, shelfRow, item, changed);
            }

            strip.Add(cell);
        }

        return strip;
    }

    /// <summary>Drag <paramref name="handle"/> onto any element with a slot number (userData) to move the item there (swapping).</summary>
    static void Draggable(VisualElement handle, BookShelfBehaviour shelf, int shelfRow, ItemDrop.ItemData item, Action changed)
    {
        handle.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
                return;
            handle.CapturePointer(e.pointerId);
            handle.style.opacity = 0.5f;
            e.StopPropagation();
        });
        handle.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!handle.HasPointerCapture(e.pointerId))
                return;
            handle.ReleasePointer(e.pointerId);
            handle.style.opacity = 1f;
            if (SlotUnder(handle.panel, e.position) is not { } slot || slot == shelf.SlotIndex(item))
                return;
            if (shelf.MoveTo(item, shelfRow, slot))
                changed();
        });
    }

    static int? SlotUnder(IPanel? panel, Vector2 position)
    {
        for (var e = panel?.Pick(position); e != null; e = e.parent)
            if (e.userData is int slot)
                return slot;
        return null;
    }

    // ---- Free: drag along the row, turn / lift / size

    static class FreeBoard
    {
        const float BoardHeight = 110f;
        static string? _selected;

        internal static VisualElement Build(BookShelfBehaviour shelf, int shelfRow, bool canManage)
        {
            var wrap = new VisualElement().Column();
            wrap.style.marginTop = 10;
            // Fills the card's width (the LockSmith menu is narrower than our own window); spots are kept as fractions of it.
            var board = new VisualElement().Size(height: BoardHeight).Fill(UkTheme.Field).Round(UkTheme.Radius).Border(UkTheme.LineSoft);
            board.style.alignSelf = Align.Stretch;
            board.style.borderBottomWidth = 6;
            board.style.borderBottomColor = new Color(0.42f, 0.30f, 0.18f);
            board.style.position = Position.Relative;
            wrap.Add(board);

            var tokens = new Dictionary<string, VisualElement>();
            foreach (var item in shelf.RowItems(shelfRow))
            {
                var key = BookShelfBehaviour.PoseKey(item);
                var pose = shelf.GetPose(item);
                var token = Spine(item);
                token.style.position = Position.Absolute;
                token.style.bottom = 4;
                token.style.left = Length.Percent(ToFraction(shelf, pose.X) * 100f);
                token.style.translate = new Translate(Length.Percent(-50f), 0f);
                tokens[key] = token;
                board.Add(token);
                Drag(token, board, shelf, item, canManage, () => Select(tokens, key));
            }

            if (_selected != null && tokens.ContainsKey(_selected))
                Select(tokens, _selected);

            var controls = new VisualElement().Row();
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.marginTop = 8;
            void Add(string text, Func<ShelfPose, ShelfPose> change)
            {
                var button = UkControls.MakeButton(text, () => Adjust(shelf, shelfRow, change));
                button.style.marginRight = 6;
                button.SetEnabled(canManage);
                controls.Add(button);
            }

            Add("Turn left", p => { p.Yaw -= 15f; return p; });
            Add("Turn right", p => { p.Yaw += 15f; return p; });
            Add("Lower", p => { p.Lift -= 0.02f; return p; });
            Add("Raise", p => { p.Lift += 0.02f; return p; });
            Add("Smaller", p => { p.Scale -= 0.1f; return p; });
            Add("Bigger", p => { p.Scale += 0.1f; return p; });
            Add("Reset", p => ShelfPose.At(p.X));
            wrap.Add(controls);
            return wrap;
        }

        static float ToFraction(BookShelfBehaviour shelf, float x) => Mathf.Clamp01(x / shelf.Usable + 0.5f);

        static float FromFraction(BookShelfBehaviour shelf, float f) => (Mathf.Clamp01(f) - 0.5f) * shelf.Usable;

        static float Fraction(VisualElement board, Vector2 worldPosition)
        {
            var width = board.resolvedStyle.width;
            return width > 1f ? Mathf.Clamp01(board.WorldToLocal(worldPosition).x / width) : 0.5f;
        }

        static void Select(Dictionary<string, VisualElement> tokens, string key)
        {
            _selected = key;
            foreach (var kv in tokens)
            {
                var on = kv.Key == key;
                kv.Value.style.borderTopColor = kv.Value.style.borderBottomColor = kv.Value.style.borderLeftColor = kv.Value.style.borderRightColor =
                    on ? UkTheme.Gold : Color.black;
            }
        }

        static void Drag(VisualElement token, VisualElement board, BookShelfBehaviour shelf, ItemDrop.ItemData item, bool canManage, Action select)
        {
            var dragging = false;
            token.RegisterCallback<PointerDownEvent>(e =>
            {
                select();
                if (!canManage)
                    return;
                dragging = true;
                token.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            token.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging)
                    return;
                token.style.left = Length.Percent(Fraction(board, e.position) * 100f);
            });
            token.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging)
                    return;
                dragging = false;
                token.ReleasePointer(e.pointerId);
                var pose = shelf.GetPose(item);
                pose.X = FromFraction(shelf, Fraction(board, e.position));
                shelf.SetPose(item, pose);
            });
        }

        static void Adjust(BookShelfBehaviour shelf, int shelfRow, Func<ShelfPose, ShelfPose> change)
        {
            if (_selected == null)
                return;
            foreach (var item in shelf.RowItems(shelfRow))
            {
                if (BookShelfBehaviour.PoseKey(item) != _selected)
                    continue;
                shelf.SetPose(item, change(shelf.GetPose(item)));
                return;
            }
        }
    }
}

/// <summary>Shift+E on a shelf when LockSmith is not installed (with LockSmith the same layout is its Shelf tab).</summary>
internal sealed class ShelfSettingsPanel
{
    static ShelfSettingsPanel? _instance;
    readonly UkWindow _window;
    BookShelfBehaviour? _shelf;

    public static void Show(BookShelfBehaviour shelf, Player player)
    {
        _instance ??= new ShelfSettingsPanel();
        _instance._shelf = shelf;
        _instance.Rebuild();
        _instance._window.Open();
    }

    internal static void CloseWindow() => _instance?._window.CloseSilent();

    ShelfSettingsPanel()
    {
        _window = new UkWindow("drakes_shelf_settings", 760f);
        _window.ShowFooter(false);
    }

    void Rebuild()
    {
        var shelf = _shelf;
        _window.Body.Clear();
        if (shelf == null)
            return;
        var container = shelf.GetComponent<Container>();
        var title = container != null && Localization.instance != null ? Localization.instance.Localize(container.m_name) : "Shelf";
        var books = 0;
        for (var row = 0; row < shelf.ShelfRows; row++)
            foreach (var item in shelf.RowItems(row))
                if (BookItem.IsBook(item))
                    books++;
        _window.SetHeader(title, books == 1 ? "1 book" : books + " books");
        ShelfLayout.Build(_window.Body, shelf, ShelfLayout.IsOwner(shelf), Rebuild);
    }
}
