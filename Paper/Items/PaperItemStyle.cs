using System.Collections.Generic;
using System.Globalization;
using DrakeRenameit.Paper.Functionality;
using UnityEngine;

namespace DrakeRenameit.Paper.Items;

/// <summary>
/// Paper-tab style on Written Page <see cref="ItemDrop.ItemData.m_customData"/>.
/// Keys match piece ZDO names so take/place round-trips without a second vocabulary.
/// </summary>
internal static class PaperItemStyle
{
    /// <summary>Same string as <c>PaperWrittenVessel.ZdoFontSize</c>.</summary>
    public const string FontSizeKey = "DrakePaper_FontSize";

    /// <summary>Same string as <c>PaperWrittenVessel.ZdoLandscape</c>.</summary>
    public const string LandscapeKey = "DrakePaper_Landscape";

    /// <summary>Same string as <c>PaperWrittenVessel.ZdoTakePublic</c>.</summary>
    public const string TakePublicKey = "DrakePaper_TakePublic";

    public static void Read(
        ItemDrop.ItemData? item,
        out float fontSize,
        out bool landscape,
        out bool takePublic)
    {
        fontSize = PaperFontScale.NormalizeStored(RenameitConfig.PaperDefaultFontSize);
        landscape = RenameitConfig.PaperDefaultLandscape;
        takePublic = false;
        if (item?.m_customData == null)
            return;

        if (item.m_customData.TryGetValue(FontSizeKey, out var fs) &&
            float.TryParse(fs, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            fontSize = PaperFontScale.NormalizeStored(parsed);

        if (item.m_customData.TryGetValue(LandscapeKey, out var land))
            landscape = land == "1" || land.Equals("true", System.StringComparison.OrdinalIgnoreCase);

        if (item.m_customData.TryGetValue(TakePublicKey, out var pub))
            takePublic = pub == "1" || pub.Equals("true", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Write font + landscape. Optionally update take-public preference.</summary>
    public static void WriteStyle(ItemDrop.ItemData? item, float fontSize, bool landscape)
    {
        if (item == null)
            return;
        EnsureCustomData(item);
        item.m_customData![FontSizeKey] =
            PaperFontScale.NormalizeStored(fontSize).ToString("R", CultureInfo.InvariantCulture);
        item.m_customData[LandscapeKey] = landscape ? "1" : "0";
    }

    public static void WriteTakePublic(ItemDrop.ItemData? item, bool takePublic)
    {
        if (item == null)
            return;
        EnsureCustomData(item);
        item.m_customData![TakePublicKey] = takePublic ? "1" : "0";
    }

    public static void WriteAll(
        ItemDrop.ItemData? item,
        float fontSize,
        bool landscape,
        bool takePublic)
    {
        WriteStyle(item, fontSize, landscape);
        WriteTakePublic(item, takePublic);
    }

    static void EnsureCustomData(ItemDrop.ItemData item)
    {
        item.m_customData ??= new Dictionary<string, string>();
    }
}
