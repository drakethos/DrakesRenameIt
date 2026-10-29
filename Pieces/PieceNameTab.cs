using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.Pieces;

/// <summary>
/// The Name tab inside LockSmith's Lock menu. It is RenameIt's own tab, shown beside Lock rather than as a child of it.
/// Shift+Use on a chest or door that has a Lock menu belongs to LockSmith
/// (see <see cref="DrakeModsLibs.API.PieceInteractionClaims"/>), so the standalone piece workshop does not open there.
/// Pieces with no Lock menu (a workbench, a bed, a portal) keep the workshop.
/// </summary>
internal static class PieceNameTab
{
    internal const string TabId = "renameit.piece";

    internal static void Register() =>
        PieceMenuTabs.Register(new PieceMenuTab(
            TabId,
            "Name",
            c => ShowsFor(c),
            Build,
            group: "renameit",
            isRoot: true)
        {
            // Doors are not containers. Chest matching stays on AppliesTo; this covers the door.
            AppliesToHost = host => ShowsFor(host),
            // Always on the row. Shop still opens on Shop, and a chest still opens on Lock.
            Leads = false,
        });

    /// <summary>Same pieces the workshop would rename, except it does not ask <c>NameablePiece</c> (that is false once LockSmith has claimed the piece).</summary>
    static bool ShowsFor(Component? host)
    {
        if (!host || !RenameitConfig.PieceRenameEnabled)
            return false;
        var piece = host.GetComponentInParent<Piece>() ?? host.GetComponent<Piece>();
        if (!piece || PieceRename.FirstNameableHover(piece) == null)
            return false;
        return PieceRename.CanRename(piece, Player.m_localPlayer, flash: false);
    }

    static void Build(PieceMenuTabContext ctx)
    {
        var host = ctx.Host;
        var piece = host ? host.GetComponentInParent<Piece>() ?? host.GetComponent<Piece>() : null;
        if (!piece)
        {
            ctx.Body.Add(UkControls.Text("This piece cannot be renamed.", 14, UkTheme.TextMuted));
            return;
        }

        var player = Player.m_localPlayer;
        var can = PieceRename.CanRename(piece, player, flash: false);
        var custom = PieceRename.GetCustomName(piece);
        var original = PieceRename.DefaultName(piece);

        var name = new UkField("Name", multiline: false, resetTooltip: "Back to the original name");
        name.EnableRich();
        name.SetMax(System.Math.Max(1, RenameitConfig.PieceNameCharLimit));
        name.SetEditable(can);
        name.SetValue(custom.Length > 0 ? custom : original);
        name.ResetClicked = () =>
        {
            name.SetValue(PieceRename.DefaultName(piece));
        };
        ctx.Body.Add(name.Root);

        var showShared = RenameitConfig.PieceSharedEnabled && RenameitConfig.PieceLockToBuilder;
        if (showShared)
        {
            var shared = new UkSwitch("Shared", "Anyone with access to the ward here can rename this piece.");
            var may = PieceRename.CanChangeShared(piece, player);
            shared.SetValue(PieceRename.IsShared(piece), notify: false);
            shared.SetEnabled(may, may ? "Anyone with access to the ward here can rename this piece." : "Only the builder or an admin can change this.");
            shared.Changed = on =>
            {
                if (!piece || !PieceRename.CanChangeShared(piece, Player.m_localPlayer))
                {
                    shared.SetValue(piece && PieceRename.IsShared(piece), notify: false);
                    return;
                }

                PieceRename.SetShared(piece, on);
            };
            shared.Root.style.marginTop = 12;
            ctx.Body.Add(shared.Root);
        }

        var row = new VisualElement().Row();
        row.style.marginTop = 14;
        row.Add(UkControls.Spacer());
        var save = UkControls.MakeButton("Save", () => Save(piece, name), UkButtonKind.Primary, width: 110);
        UkControls.SetButtonEnabled(save, can);
        name.SubmitPressed = () => Save(piece, name);
        row.Add(save);
        ctx.Body.Add(row);
    }

    static void Save(Piece piece, UkField name)
    {
        if (!piece || !PieceRename.CanRename(piece, Player.m_localPlayer, flash: false))
            return;
        var value = name.Value == PieceRename.DefaultName(piece) ? "" : name.Value;
        PieceRename.SetCustomName(piece, value);
        NameLibrary.Add(NameLibrary.PieceKind(piece), value);
    }
}
