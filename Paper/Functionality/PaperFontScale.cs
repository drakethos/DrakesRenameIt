using UnityEngine;

namespace DrakeRenameit.Paper.Functionality;

/// <summary>
/// On-page ink size: UI levels 1–7 map to TMP canvas font sizes in the range that
/// actually renders on the parchment (same space as the working phase-1 ink).
/// </summary>
internal static class PaperFontScale
{
    public const int LevelMin = 1;
    /// <summary>Biggest size players want on a note.</summary>
    public const int LevelMax = 7;

    /// <summary>Level 1 — small, room for a long letter.</summary>
    public const float SizeMin = 0.055f;
    /// <summary>Level 7 — large body on a short note (past this barely helps).</summary>
    public const float SizeMax = 0.28f;

    /// <summary>Brief broken "page fraction" era wrote values in this band.</summary>
    const float BrokenFactorMax = 0.12f;

    public static float LevelToSize(int level)
    {
        level = Mathf.Clamp(level, LevelMin, LevelMax);
        var t = (level - LevelMin) / (float)(LevelMax - LevelMin);
        return Mathf.Lerp(SizeMin, SizeMax, t);
    }

    public static int SizeToLevel(float size)
    {
        var t = Mathf.InverseLerp(SizeMin, SizeMax, Mathf.Clamp(NormalizeStored(size), SizeMin, SizeMax));
        return Mathf.Clamp(
            Mathf.RoundToInt(t * (LevelMax - LevelMin)) + LevelMin,
            LevelMin,
            LevelMax);
    }

    /// <summary>
    /// Normalize ZDO/config to a canvas font size.
    /// Migrates legacy 0.08–0.45 absolutes and the short-lived page-fraction values.
    /// </summary>
    public static float NormalizeStored(float stored)
    {
        if (float.IsNaN(stored) || float.IsInfinity(stored) || stored <= 0f)
            return LevelToSize(3);

        // Page-fraction spike (~0.02–0.10): remap through levels so notes recover.
        if (stored < SizeMin && stored <= BrokenFactorMax)
        {
            var t = Mathf.InverseLerp(0.020f, 0.100f, Mathf.Clamp(stored, 0.020f, 0.100f));
            var level = Mathf.Clamp(
                Mathf.RoundToInt(t * (LevelMax - LevelMin)) + LevelMin,
                LevelMin,
                LevelMax);
            return LevelToSize(level);
        }

        return Mathf.Clamp(stored, SizeMin, SizeMax);
    }

    public static int StoredToLevel(float stored) => SizeToLevel(NormalizeStored(stored));

    /// <summary>TMP fontSize / fontSizeMax in canvas units.</summary>
    public static float ResolveCanvasSize(float stored, float pageHeightUnits = 0f)
    {
        _ = pageHeightUnits;
        return NormalizeStored(stored);
    }
}
