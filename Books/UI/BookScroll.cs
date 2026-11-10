using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Books;

/// <summary>
/// A vertical scroll area with a drawn bar, for the book windows. The suite's panels run with an empty UI Toolkit theme, so a bare
/// ScrollView gets none of Unity's default layout: its content shrank to fit (rows drawn over each other) and its own scroller had no
/// look. Here the content keeps its height, the mouse wheel scrolls it, and a thin track with a draggable thumb shows on the right
/// whenever there is more than fits. (A candidate for DrakeModsLibs as a shared control.)
/// </summary>
internal static class BookScroll
{
    internal const string ThumbName = "drakes_scroll_thumb";

    /// <summary>Adds the scroll area to <paramref name="parent"/> and returns its content element (add rows to that).</summary>
    internal static VisualElement Add(VisualElement parent, float maxHeight, string name)
    {
        var content = Make(maxHeight, name, out var root);
        parent.Add(root);
        return content;
    }

    /// <summary>Builds the area; <paramref name="root"/> goes into the layout, the returned element takes the content.</summary>
    internal static VisualElement Make(float maxHeight, string name, out VisualElement root)
    {
        root = new VisualElement { name = name + "_frame" };
        root.style.position = Position.Relative;
        root.style.flexShrink = 0;
        root.style.maxHeight = maxHeight;

        var scroll = new ScrollView(ScrollViewMode.Vertical)
        {
            name = name,
            verticalScrollerVisibility = ScrollerVisibility.Hidden,
            horizontalScrollerVisibility = ScrollerVisibility.Hidden,
        };
        scroll.style.maxHeight = maxHeight;
        scroll.style.flexShrink = 1;
        scroll.contentViewport.style.overflow = Overflow.Hidden;
        scroll.contentViewport.style.flexGrow = 1;
        scroll.contentViewport.style.flexShrink = 1;
        var content = scroll.contentContainer;
        content.style.flexShrink = 0;
        content.style.flexGrow = 0;
        content.style.paddingRight = 16;
        root.Add(scroll);

        var track = new VisualElement { name = "drakes_scroll_track" }.Fill(UkTheme.Field).Round(4);
        track.style.position = Position.Absolute;
        track.style.right = 0;
        track.style.top = 2;
        track.style.bottom = 2;
        track.style.width = 8;
        var thumb = new VisualElement { name = ThumbName }.Fill(UkTheme.TextMuted).Round(4);
        thumb.style.position = Position.Absolute;
        thumb.style.left = 0;
        thumb.style.right = 0;
        track.Add(thumb);
        root.Add(track);

        void Paint()
        {
            var view = scroll.contentViewport.layout.height;
            var full = content.layout.height;
            var more = full > view + 1f && view > 1f;
            track.style.display = more ? DisplayStyle.Flex : DisplayStyle.None;
            if (!more)
                return;
            var trackHeight = track.layout.height > 1f ? track.layout.height : view;
            var size = Mathf.Max(24f, trackHeight * view / full);
            var range = full - view;
            var at = Mathf.Clamp01(scroll.scrollOffset.y / range);
            thumb.style.height = size;
            thumb.style.top = (trackHeight - size) * at;
        }

        scroll.verticalScroller.valueChanged += _ => Paint();
        content.RegisterCallback<GeometryChangedEvent>(_ => Paint());
        scroll.RegisterCallback<GeometryChangedEvent>(_ => Paint());

        // The wheel: the hidden scroller still moves the view, but only as far as the content's real height allows.
        scroll.RegisterCallback<WheelEvent>(e =>
        {
            var range = content.layout.height - scroll.contentViewport.layout.height;
            if (range <= 0f)
                return;
            scroll.scrollOffset = new Vector2(0f, Mathf.Clamp(scroll.scrollOffset.y + e.delta.y * 40f, 0f, range));
            Paint();
            e.StopPropagation();
        }, TrickleDown.TrickleDown);

        // Drag the thumb, or click the track to jump there.
        var dragging = false;
        var grabOffset = 0f;
        void ScrollTo(float y)
        {
            var range = content.layout.height - scroll.contentViewport.layout.height;
            var size = thumb.layout.height;
            var span = track.layout.height - size;
            if (range <= 0f || span <= 0f)
                return;
            scroll.scrollOffset = new Vector2(0f, Mathf.Clamp01((y - grabOffset) / span) * range);
            Paint();
        }

        track.RegisterCallback<PointerDownEvent>(e =>
        {
            var local = track.WorldToLocal(e.position);
            var thumbTop = thumb.layout.y;
            grabOffset = local.y >= thumbTop && local.y <= thumbTop + thumb.layout.height ? local.y - thumbTop : thumb.layout.height / 2f;
            dragging = true;
            track.CapturePointer(e.pointerId);
            ScrollTo(local.y);
            e.StopPropagation();
        });
        track.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (dragging)
                ScrollTo(track.WorldToLocal(e.position).y);
        });
        track.RegisterCallback<PointerUpEvent>(e =>
        {
            dragging = false;
            track.ReleasePointer(e.pointerId);
        });

        return content;
    }
}
