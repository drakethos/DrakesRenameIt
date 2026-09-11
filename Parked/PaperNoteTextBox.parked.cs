// PARKED — not compiled (not in DrakeRenameit.csproj).
// On-page TMP text box for Written notes. Revisit later if we want Sign-like text on the mesh.
// Last working sketch lived in PaperWrittenPlace (CaptureSignFont / AttachNoteTextBox / vessel RefreshTextBox).

#if false
using TMPro;
using UnityEngine;

namespace DrakeRenameit.Parked;

internal static class PaperNoteTextBox
{
    internal static TMP_FontAsset? CaptureSignFont(GameObject go)
    {
        try
        {
            var sign = go.GetComponentInChildren<Sign>(true);
            var widget = sign != null ? sign.m_textWidget : null;
            if (widget != null && widget.font != null)
                return widget.font;
        }
        catch
        {
            /* ignore */
        }

        foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font != null && font.name.IndexOf("Valheim", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return font;
        }

        return null;
    }

    /// <summary>World-space text box on the parchment (name + description, rich-text colors).</summary>
    internal static void AttachNoteTextBox(GameObject root, TMP_FontAsset? font)
    {
        var parent = root.transform.Find("drakes_paper_decor");
        if (parent == null)
            parent = root.transform;

        var box = new GameObject("drakes_paper_textbox");
        var t = box.transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(0f, 0f, 0.0025f);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        var canvas = box.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        var rt = box.GetComponent<RectTransform>();
        float margin = 0.82f;
        rt.sizeDelta = new Vector2(PaperItem.PaperSize.x * margin, PaperItem.PaperSize.y * margin);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var textGo = new GameObject("body");
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.SetParent(t, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.richText = true;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.018f;
        tmp.fontSizeMax = 0.065f;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = new Color(0.14f, 0.09f, 0.04f, 1f);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.raycastTarget = false;
        tmp.text = "";
    }

    internal static void RefreshBody(TextMeshProUGUI? body, string name, string desc)
    {
        if (body == null)
            return;
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(desc))
        {
            body.text = "";
            return;
        }

        if (string.IsNullOrEmpty(desc))
            body.text = name;
        else if (string.IsNullOrEmpty(name))
            body.text = desc;
        else
            body.text = name + "\n" + desc;
    }
}
#endif
