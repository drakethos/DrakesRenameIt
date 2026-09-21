using System;
using System.Reflection;
using DrakeRenameit.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeRenameit;

/// <summary>
/// Spike: on-page description using the donor Sign's own text widget.
/// Building a TMP widget from scratch never rendered (0 verts — Unity defaults to
/// LiberationSans, which Valheim does not ship, and a hand-made world Canvas loses the
/// scale/rect TMP needs). So we keep the authored widget alive and only set its string.
/// The <c>Sign</c> component and raycaster come off, so [E] stays Take.
/// </summary>
internal static class PaperNotePageText
{
    /// <summary>Name must start with <c>drakes_paper</c> so donor stripping keeps it.</summary>
    const string HolderName = "drakes_paper_signtext";
    const float Margin = 0.88f;
    const float FaceClearance = 0.004f;
    static readonly Color Ink = new Color(0.12f, 0.07f, 0.03f, 1f);

    static readonly FieldInfo? SignTextWidgetField = AccessTools.Field(typeof(Sign), "m_textWidget");

    static bool _loggedDonor;
    static int _syncLogsLeft = 6;

    /// <summary>
    /// Run on the sign donor <b>before</b> <see cref="PaperItem.BuildDecorSheetVisual"/>:
    /// moves the Sign's text widget into a holder we own so stripping spares it.
    /// </summary>
    internal static void PreserveSignText(GameObject? root)
    {
        if (root == null)
            return;
        if (root.transform.Find(HolderName) != null)
            return;

        try
        {
            var widget = FindSignWidget(root);
            if (widget == null)
            {
                Debug.LogWarning("[DrakesRenameit] Paper page: donor sign has no TMP text widget.");
                return;
            }

            // Climb to the outermost Canvas inside the donor (sign text may be nested).
            var keep = widget.transform;
            for (var cur = widget.transform.parent; cur != null && cur != root.transform; cur = cur.parent)
            {
                if (cur.GetComponent<Canvas>() != null)
                    keep = cur;
            }

            if (!_loggedDonor)
            {
                _loggedDonor = true;
                var canvas = widget.GetComponentInParent<Canvas>();
                Debug.Log(
                    $"[DrakesRenameit] Paper page donor widget={widget.GetType().Name} " +
                    $"keep='{keep.name}' keepScale={keep.localScale.x:F4} " +
                    $"canvas={(canvas != null ? canvas.renderMode.ToString() : "none")} " +
                    $"font={(widget.font != null ? widget.font.name : "NULL")} fontSize={widget.fontSize:F2} " +
                    $"mat={(widget.fontSharedMaterial != null ? widget.fontSharedMaterial.name : "NULL")}");
            }

            var holder = new GameObject(HolderName);
            holder.transform.SetParent(root.transform, false);

            // Reparent with the authored local scale intact — that scale is what makes
            // the vanilla sign text render at a sane world size.
            var scale = keep.localScale;
            keep.SetParent(holder.transform, false);
            keep.localPosition = Vector3.zero;
            keep.localRotation = Quaternion.identity;
            keep.localScale = scale;

            StripInteract(holder);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DrakesRenameit] Paper page preserve failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Run after decor exists: park the preserved widget on the parchment face.</summary>
    internal static void AttachToPrefab(GameObject? root, bool wall)
    {
        if (root == null)
            return;

        try
        {
            var holder = root.transform.Find(HolderName);
            if (holder == null)
                return;

            var decor = FindDecor(root);
            if (holder.parent != decor)
                holder.SetParent(decor, false);

            OrientOnParchment(holder, decor, wall);

            var widget = holder.GetComponentInChildren<TMP_Text>(true);
            if (widget == null)
                return;

            WakeChain(holder, widget.transform);
            StyleAsPage(widget, holder);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DrakesRenameit] Paper page attach failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// The donor Sign's canvas ships <b>disabled</b>. An inactive ancestor means TMP parses
    /// the string but never builds geometry, so activate holder → widget and turn the
    /// Canvas into a world-space renderer we own.
    /// </summary>
    static void WakeChain(Transform holder, Transform widget)
    {
        holder.gameObject.SetActive(true);
        var chain = widget;
        while (chain != null)
        {
            chain.gameObject.SetActive(true);
            var canvas = chain.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
                canvas.additionalShaderChannels =
                    AdditionalCanvasShaderChannels.TexCoord1 |
                    AdditionalCanvasShaderChannels.Normal |
                    AdditionalCanvasShaderChannels.Tangent;
                if (Camera.main != null)
                    canvas.worldCamera = Camera.main;
            }

            if (chain == holder)
                break;
            chain = chain.parent;
        }
    }

    /// <summary>
    /// Ink on a letter sheet: small type, wrap, shrink, then truncate.
    /// Sign billboard size is useless here — drive from the page rect so a long
    /// description (hundreds of chars) can shrink to fit before truncating.
    /// </summary>
    static void StyleAsPage(TMP_Text widget, Transform? holder)
    {
        var page = new Vector2(PaperItem.PaperSize.x * Margin, PaperItem.PaperSize.y * Margin);

        var canvas = widget.GetComponentInParent<Canvas>();
        var canvasRt = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        var scaleSrc = canvas != null ? canvas.transform : widget.transform;
        var lossy = WorldScale(scaleSrc);
        float sx = Mathf.Max(Mathf.Abs(lossy.x), 1e-4f);
        float sy = Mathf.Max(Mathf.Abs(lossy.y), 1e-4f);
        var units = new Vector2(page.x / sx, page.y / sy);

        if (canvasRt != null)
        {
            canvasRt.sizeDelta = units;
            canvasRt.pivot = new Vector2(0.5f, 0.5f);
            canvasRt.anchoredPosition = Vector2.zero;
        }

        var rt = widget.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        // Portrait page: enough room for a short letter without billboard-size glyphs.
        float max = Mathf.Clamp(units.y * 0.010f, 0.08f, 0.30f);
        float min = Mathf.Max(0.05f, max * 0.35f);
        widget.enableAutoSizing = true;
        widget.fontSize = max;
        widget.fontSizeMax = max;
        widget.fontSizeMin = min;
        widget.richText = true;
        widget.alignment = TextAlignmentOptions.TopLeft;
        widget.color = Ink;
        widget.textWrappingMode = TextWrappingModes.Normal;
        widget.overflowMode = TextOverflowModes.Truncate;
        widget.raycastTarget = false;
        widget.maskable = false;
        widget.enabled = true;
        _ = holder;
    }

    internal static void Sync(GameObject? root, string? description)
    {
        if (root == null)
            return;
        try
        {
            if (PaperWrittenPlace.IsPlacementGhost(root))
                return;

            var widget = FindPageWidget(root);

            if (!RenameitConfig.PaperShowPageText)
            {
                var off = FindHolder(root);
                if (off != null)
                    off.gameObject.SetActive(false);
                PaperItem.SetNotePieceScribbles(root, scribbles: true);
                return;
            }

            PaperItem.SetNotePieceScribbles(root, scribbles: false);
            if (widget == null)
            {
                AttachToPrefab(root, IsWallPiece(root));
                widget = FindPageWidget(root);
            }

            if (widget != null)
            {
                var holder = FindHolder(root);
                if (holder != null)
                {
                    OrientOnParchment(holder, FindDecor(root), IsWallPiece(root));
                    WakeChain(holder, widget.transform);
                    StyleAsPage(widget, holder);
                }
                SetBody(widget, description);
            }

            var bind = root.GetComponent<PaperPageTextBind>();
            if (bind == null)
                bind = root.AddComponent<PaperPageTextBind>();
            bind.Desc = description ?? "";

            LogSync(root, widget, description);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DrakesRenameit] Paper page text failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void SetBody(TMP_Text? body, string? description)
    {
        if (body == null)
            return;
        var desc = TooltipRichText.EnsureRichTextTagsClosedForTooltip(description);
        body.text = desc ?? "";
        body.ForceMeshUpdate(ignoreActiveState: true);
    }

    internal static TMP_Text? FindPageWidget(GameObject root)
    {
        var holder = FindHolder(root);
        return holder != null ? holder.GetComponentInChildren<TMP_Text>(true) : null;
    }

    static Transform? FindHolder(GameObject root)
    {
        var holder = FindDecor(root).Find(HolderName);
        return holder != null ? holder : root.transform.Find(HolderName);
    }

    static TMP_Text? FindSignWidget(GameObject root)
    {
        var sign = root.GetComponentInChildren<Sign>(true);
        if (sign != null && SignTextWidgetField != null &&
            SignTextWidgetField.GetValue(sign) is TMP_Text widget && widget != null)
            return widget;

        return root.GetComponentInChildren<TMP_Text>(true);
    }

    static void StripInteract(GameObject root)
    {
        foreach (var sign in root.GetComponentsInChildren<Sign>(true))
            UnityEngine.Object.DestroyImmediate(sign);
        foreach (var ray in root.GetComponentsInChildren<GraphicRaycaster>(true))
            UnityEngine.Object.DestroyImmediate(ray);
        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            tmp.raycastTarget = false;
    }

    static Transform FindDecor(GameObject root)
    {
        var t = root.transform.Find("drakes_paper_decor");
        return t != null ? t : root.transform;
    }

    /// <summary>Accumulated world scale (prefabs report zero-parent chains as one).</summary>
    static Vector3 WorldScale(Transform t) => t.localToWorldMatrix.lossyScale;

    /// <summary>
    /// Forge parchment faces local +Y. Wall and flat share the same portrait page-up
    /// (LTR along paper width) so a table sheet reads like the wall sheet, just lying
    /// down. Negative X undoes the mirror both showed on the front face.
    /// </summary>
    static void OrientOnParchment(Transform holder, Transform decor, bool wall)
    {
        var forgeFace = decor.Find("paper_front") == null;
        var lift = FaceClearance + MaxExtent(decor, forgeFace ? 1 : 2);
        _ = wall;

        if (forgeFace)
        {
            holder.localPosition = new Vector3(0f, lift, 0f);
            // Same local frame for wall + flat: canvas up = paper height (−Z), LTR = width (+X).
            holder.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
        }
        else
        {
            holder.localPosition = new Vector3(0f, 0f, lift);
            holder.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        holder.localScale = new Vector3(-1f, 1f, 1f);
    }

    static bool IsWallPiece(GameObject root)
    {
        var n = root.name ?? "";
        if (n.IndexOf("Vertical", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (n.IndexOf("Flat", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return IsWallDecor(FindDecor(root));
    }

    /// <summary>Wall notes tip the forge sheet with Euler(90,0,0). Flat keeps identity.</summary>
    static bool IsWallDecor(Transform decor)
    {
        var e = decor.localEulerAngles;
        return Mathf.Abs(Mathf.DeltaAngle(e.x, 90f)) < 5f;
    }

    static float MaxExtent(Transform decor, int axis)
    {
        float max = 0f;
        var filters = decor.GetComponentsInChildren<MeshFilter>(true);
        for (var i = 0; i < filters.Length; i++)
        {
            var filter = filters[i];
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                continue;
            var b = mesh.bounds;
            var corners = new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };
            for (var c = 0; c < corners.Length; c++)
            {
                var local = decor.InverseTransformPoint(filter.transform.TransformPoint(corners[c]));
                var v = axis == 1 ? local.y : local.z;
                if (v > max)
                    max = v;
            }
        }

        return max;
    }

    static void LogSync(GameObject root, TMP_Text? body, string? description)
    {
        if (_syncLogsLeft <= 0)
            return;
        _syncLogsLeft--;

        body?.ForceMeshUpdate(ignoreActiveState: true);
        var verts = 0;
        var chars = 0;
        if (body?.textInfo != null)
        {
            chars = body.textInfo.characterCount;
            if (body.textInfo.meshInfo != null && body.textInfo.meshInfo.Length > 0)
                verts = body.textInfo.meshInfo[0].vertexCount;
        }

        var world = body != null ? WorldScale(body.transform) : Vector3.zero;
        var active = body != null && body.gameObject.activeInHierarchy;
        var renderers = body != null ? body.GetComponentsInChildren<CanvasRenderer>(true).Length : 0;
        var wall = IsWallPiece(root);
        Debug.Log(
            $"[DrakesRenameit] Paper page wall={wall} widget={(body != null ? body.GetType().Name : "NULL")} " +
            $"activeInHierarchy={active} canvasRenderers={renderers} " +
            $"font={(body != null && body.font != null ? body.font.name : "NULL")} " +
            $"fontSize={(body != null ? body.fontSize : 0f):F2} rect={(body != null ? body.rectTransform.sizeDelta : Vector2.zero)} " +
            $"worldScale={world.x:F4} descChars={description?.Length ?? 0} tmpChars={chars} verts={verts}");
    }
}

/// <summary>Repaints for a few frames — first Sync runs before the piece finishes spawning.</summary>
internal sealed class PaperPageTextBind : MonoBehaviour
{
    internal string Desc = "";
    int _frames;

    void LateUpdate()
    {
        _frames++;
        var tmp = PaperNotePageText.FindPageWidget(gameObject);
        PaperNotePageText.SetBody(tmp, Desc);
        var chars = tmp != null && tmp.textInfo != null ? tmp.textInfo.characterCount : 0;
        if (chars > 0 || _frames > 10)
            enabled = false;
    }
}
