using DrakeModsLibs.Forge;
using DrakeRenameit.Paper.Generated.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Paper.Items;

/// <summary>
/// The paper's look, from the DrakesPaper Asset Forge pack (<c>Forge/Paper</c>): sheet sprites and icons generated
/// into <c>Paper/Generated</c>, images embedded in this DLL. Edit the recipes in Asset Forge and export again
/// (see <c>Forge/Paper/README.md</c>); sizes are US Letter scaled by <see cref="RenameitConfig.PaperScale"/>.
/// </summary>
internal static class PaperAssets
{
    private static float Scale => RenameitConfig.PaperScale;

    internal static Sprite? BlankIcon => PieceOfPaper.Icon;
    internal static Sprite? WrittenIcon => WrittenPaper.Icon;

    /// <summary>An inventory/dropped sheet: flat, face up, under the item's <c>attach</c>.</summary>
    internal static void AttachItemSheet(Transform attach, bool written)
    {
        if (written)
            WrittenPaper.Build(attach, Scale);
        else
            PieceOfPaper.Build(attach, Scale);
    }

    /// <summary>A placed sheet under the piece's decor holder: standing facing +Z on walls, face up when flat.</summary>
    internal static void AttachPieceSheet(Transform decor, bool wall, bool written)
    {
        switch (wall, written)
        {
            case (true, true):
                NoteWall.Build(decor, Scale);
                break;
            case (true, false):
                PaperWall.Build(decor, Scale);
                break;
            case (false, true):
                NoteFlat.Build(decor, Scale);
                break;
            default:
                PaperFlat.Build(decor, Scale);
                break;
        }
    }

    /// <summary>The shared sheet material (blank or written), for swapping a placed note's look. Built once.</summary>
    internal static Material? SheetMaterial(bool written)
    {
        var texture = ForgeTextures.Load(typeof(PaperAssets).Assembly, written ? "textures/paper_sheet_written.png" : "textures/paper_sheet.png");
        return texture != null ? ForgeSprites.Material(texture) : null;
    }

    /// <summary>
    /// Piece prefabs must keep a root ZNetView or world load retries ZDOs forever.
    /// </summary>
    internal static void EnsurePersistentZNetView(GameObject root)
    {
        if (root == null)
            return;

        if (!root.activeSelf)
            root.SetActive(true);

        var nested = root.GetComponentsInChildren<ZNetView>(true);
        ZNetView? rootNv = null;
        for (var i = 0; i < nested.Length; i++)
        {
            var nv = nested[i];
            if (nv == null)
                continue;
            if (nv.gameObject == root)
            {
                rootNv = nv;
                continue;
            }

            Object.DestroyImmediate(nv);
        }

        if (rootNv == null)
            rootNv = root.AddComponent<ZNetView>();

        rootNv.m_persistent = true;
        rootNv.m_distant = false;
        if (!rootNv.enabled)
            rootNv.enabled = true;
    }
}
