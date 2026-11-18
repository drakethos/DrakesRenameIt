using System;
using DrakeModsLibs.UI;
using DrakeModsLibs.UI.Toolkit;
using DrakeRenameit.UI.Toolkit;

namespace DrakeRenameit.UI;

/// <summary>
/// The Paper tab the tab host talks to (<see cref="Show"/>, <see cref="Hide"/>, <see cref="TabId"/>). Draws the Paper window in the
/// suite's new look (<see cref="PaperWorkshopPanel"/>) when <c>UseToolkitUi</c> is on, otherwise the classic wood panel
/// (<see cref="ClassicPaperTabPanel"/>). If the new look can't be built the classic one takes over, so Paper never stops working.
/// </summary>
internal static class PaperTabPanel
{
    public const string TabId = "renameit.paper";

    static bool _usingToolkit;

    /// <summary>The tab host opened the Paper tab for <paramref name="ctx"/>'s item.</summary>
    internal static void Show(DrakeTabPageContext ctx)
    {
        if (RenameitConfig.UseToolkitUi && DrakeUiMode.Toolkit)
        {
            try
            {
                ClassicPaperTabPanel.Hide();
                _usingToolkit = true;
                PaperWorkshopPanel.Show(ctx);
                return;
            }
            catch (Exception ex)
            {
                DrakeUiMode.ReportFailure(ex);
            }
        }

        _usingToolkit = false;
        PaperWorkshopPanel.Hide();
        ClassicPaperTabPanel.Show(ctx);
    }

    /// <summary>Tab host switching away (or the wall session ending): hide without ending the host session.</summary>
    internal static void Hide()
    {
        if (_usingToolkit)
            PaperWorkshopPanel.Hide();
        else
            ClassicPaperTabPanel.Hide();
    }

    /// <summary>User dismissed the Paper panel: end the tab-host / wall session.</summary>
    internal static void CloseAndEndHost()
    {
        Hide();
        DrakeTabHost.Close();
    }
}
