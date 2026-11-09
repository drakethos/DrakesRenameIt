using DrakeModsLibs.Forge;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// An open book drawn in UI Toolkit: the cover (in the book's colour, leather grain) around two pages that meet at a shaded spine,
/// with the edges of the pages already read stacked on the left and the ones still to come on the right. The pages are the 1.3 paper
/// look (book_page.png, smooth, not the old torn sprite). <see cref="Left"/> and <see cref="Right"/> are where the page text goes.
/// </summary>
internal sealed class BookSpread
{
    internal static readonly Color Ink = new(0.17f, 0.10f, 0.05f);
    internal static readonly Color PageNumberInk = new(0.45f, 0.36f, 0.24f);
    static readonly Color[] EdgeTones = { new(0.78f, 0.69f, 0.53f), new(0.66f, 0.56f, 0.40f), new(0.86f, 0.78f, 0.62f) };
    const int MaxEdges = 7;

    static Texture2D? _page;
    static Texture2D? _leather;
    static Texture2D? _gutter;

    public VisualElement Root { get; }
    public VisualElement Left { get; }
    public VisualElement Right { get; }
    readonly VisualElement _leftPage;
    readonly VisualElement _rightPage;
    readonly VisualElement _edgesLeft;
    readonly VisualElement _edgesRight;
    readonly float _pageHeight;

    public BookSpread(float pageWidth, float pageHeight, float padding = 46f)
    {
        _pageHeight = pageHeight;
        Root = new VisualElement().Row(Align.Stretch).Round(14);
        Root.style.paddingLeft = Root.style.paddingRight = 16;
        Root.style.paddingTop = 14;
        Root.style.paddingBottom = 20;
        Root.style.flexShrink = 0;
        Root.style.alignSelf = Align.Center;
        if (Leather() is { } leather)
        {
            Root.style.backgroundImage = new StyleBackground(leather);
            Root.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            Root.style.backgroundSize = new BackgroundSize(160, 160);
        }

        _edgesLeft = new VisualElement().Row(Align.Center);
        _edgesLeft.style.flexDirection = FlexDirection.Row;
        _edgesRight = new VisualElement().Row(Align.Center);
        _edgesRight.style.flexDirection = FlexDirection.RowReverse;

        (_leftPage, Left) = MakePage(pageWidth, pageHeight, padding, gutterOnRight: true);
        (_rightPage, Right) = MakePage(pageWidth, pageHeight, padding, gutterOnRight: false);

        // The fold: a thin dark crease where the two pages meet.
        var crease = new VisualElement().Size(width: 2).Fill(new Color(0.30f, 0.22f, 0.13f));
        crease.style.flexShrink = 0;

        Root.Add(_edgesLeft);
        Root.Add(_leftPage);
        Root.Add(crease);
        Root.Add(_rightPage);
        Root.Add(_edgesRight);
        SetCover(BookData.CoverColors[0]);
    }

    /// <summary>Cover colour (the leather grain is tinted with it) and a darker rim.</summary>
    public void SetCover(Color cover)
    {
        Root.style.backgroundColor = cover;
        Root.style.unityBackgroundImageTintColor = Color.Lerp(cover, Color.white, 0.15f);
        var rim = Color.Lerp(cover, Color.black, 0.55f);
        Root.style.borderTopWidth = Root.style.borderBottomWidth = Root.style.borderLeftWidth = Root.style.borderRightWidth = 3;
        Root.style.borderTopColor = Root.style.borderBottomColor = Root.style.borderLeftColor = Root.style.borderRightColor = rim;
    }

    /// <summary>
    /// How thick the stacks of page edges look: <paramref name="before"/> pages lie under the left page, <paramref name="after"/>
    /// under the right one. At least one edge each side, so it always reads as a book.
    /// </summary>
    public void SetEdges(int before, int after)
    {
        Fill(_edgesLeft, Mathf.Clamp(1 + before / 2, 1, MaxEdges));
        Fill(_edgesRight, Mathf.Clamp(1 + after / 2, 1, MaxEdges));
    }

    void Fill(VisualElement stack, int count)
    {
        stack.Clear();
        // Nearest the page first: each edge further out is a touch shorter, like the fanned block of a real book.
        for (var i = 0; i < count; i++)
        {
            var edge = new VisualElement().Size(width: 5, height: _pageHeight - 2f - i * 4f).Fill(EdgeTones[i % EdgeTones.Length]);
            edge.style.borderRightWidth = edge.style.borderLeftWidth = 1;
            edge.style.borderRightColor = edge.style.borderLeftColor = new Color(0.45f, 0.36f, 0.24f, 0.55f);
            edge.style.flexShrink = 0;
            edge.style.alignSelf = Align.Center;
            stack.Add(edge);
        }
    }

    static (VisualElement Page, VisualElement Content) MakePage(float width, float height, float padding, bool gutterOnRight)
    {
        var page = new VisualElement().Size(width: width, height: height);
        page.style.flexShrink = 0;
        page.style.backgroundColor = new Color(0.82f, 0.74f, 0.57f);
        if (PageTexture() is { } texture)
            page.style.backgroundImage = new StyleBackground(texture);

        // Shade toward the spine: the paper curves down into the fold.
        if (Gutter() is { } gutter)
        {
            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            shade.style.position = Position.Absolute;
            shade.style.top = shade.style.bottom = 0;
            shade.style.width = 70;
            if (gutterOnRight)
                shade.style.right = 0;
            else
                shade.style.left = 0;
            shade.style.backgroundImage = new StyleBackground(gutter);
            shade.style.scale = new Scale(new Vector3(gutterOnRight ? 1f : -1f, 1f, 1f));
            page.Add(shade);
        }

        var content = new VisualElement().Column().Grow();
        content.style.paddingTop = padding - 6f;
        content.style.paddingBottom = padding * 0.55f;
        content.style.paddingLeft = gutterOnRight ? padding : padding + 12f;
        content.style.paddingRight = gutterOnRight ? padding + 12f : padding;
        page.Add(content);
        return (page, content);
    }

    /// <summary>The page paper. Smooth filtering: the old reader drew point-filtered and the edges came out jagged.</summary>
    internal static Texture2D? PageTexture()
    {
        if (_page == null)
        {
            _page = ForgeTextures.Load(typeof(BookSpread).Assembly, "textures/book_page.png");
            if (_page != null)
                _page.filterMode = FilterMode.Bilinear;
        }

        return _page;
    }

    static Texture2D? Leather()
    {
        if (_leather == null)
        {
            _leather = ForgeTextures.Load(typeof(BookSpread).Assembly, "textures/book_cover.png");
            if (_leather != null)
            {
                _leather.filterMode = FilterMode.Bilinear;
                _leather.wrapMode = TextureWrapMode.Repeat;
            }
        }

        return _leather;
    }

    /// <summary>Clear on the left, dark on the right (the spine side of a left page).</summary>
    static Texture2D Gutter()
    {
        if (_gutter != null)
            return _gutter;
        const int w = 64;
        _gutter = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (var x = 0; x < w; x++)
        {
            var t = x / (float)(w - 1);
            _gutter.SetPixel(x, 0, new Color(0.18f, 0.11f, 0.04f, Mathf.Pow(t, 2.4f) * 0.55f));
        }

        _gutter.Apply();
        return _gutter;
    }

    /// <summary>A page of text as it reads: rich text (bold, sizes, colours) in ink, with the page number at the foot.</summary>
    internal static void FillRead(VisualElement content, string text, int pageNumber, int fontSize, bool leftPage)
    {
        content.Clear();
        var label = UkControls.Text(text, fontSize, Ink);
        label.enableRichText = true;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexGrow = 1;
        label.style.unityTextAlign = TextAnchor.UpperLeft;
        UkFonts.ApplyBody(label);
        content.Add(label);
        content.Add(PageNumber(pageNumber, leftPage));
    }

    internal static Label PageNumber(int number, bool leftPage)
    {
        var label = UkControls.Text(number > 0 ? number.ToString() : "", 15, PageNumberInk, wrap: false);
        label.style.unityTextAlign = leftPage ? TextAnchor.LowerLeft : TextAnchor.LowerRight;
        label.style.flexShrink = 0;
        return label;
    }
}
