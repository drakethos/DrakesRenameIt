# RenameIt 1.4: two real clients against the server (piece sync)

Hand-over for the agent that runs Player 2. Branch `beta/1.4-piece-sync` (worktree `DrakesWorkshop/DrakesRenameIt-1.4fix`, cut from tag `v1.4.0-beta.8`). Do not use the `DrakesRenameIt` folder: it holds an untested, uncommitted refactor.

## What is reported (2026-11-10, hand test with two players on a server)

1. On workbenches, chests and some other pieces (doors are fine) the second player either cannot open the rename menu with **Shared** on, or can change the name but it is not written / does not reach the other player.
2. The Cart (and probably the Cartography Table) do not sync a rename to other players.
3. Separate, undecided feature: map pin recolours / renames are not shared through the cartography table (pin styles live on the character per world).

## What is already known

- A virtual second player (`peerJoin`, a puppet inside one game) passes **PN-20..22** for all 38 piece kinds below. So the RenameIt logic (owner-side check, sender-side routing, access rules) is right for them when both ends are in one process. The bug is in what the puppet cannot show: a real client, a dedicated server, real ownership hand-over, config sync.
- Suspects, none confirmed: an RPC sent to a ZDO that the server owns (the server has no instance, so it is dropped); ward access (`PieceRequireWardAccess`, default on); config not matching between server and clients (`12 Pieces`); the owner client not having the sender's Player object loaded.
- 1.4fix now logs these (see "Logging"). **The logs are the point of this run.**

Piece kinds in the sweep: piece_chest_wood, piece_chest, piece_chest_private, piece_chest_blackmetal, piece_chest_barrel, Cart, piece_workbench, forge, piece_cauldron, piece_stonecutter, piece_artisanstation, piece_magetable, blastfurnace, smelter, charcoal_kiln, windmill, piece_spinningwheel, eitrrefinery, fermenter, piece_cookingstation, piece_oven, piece_MeadCauldron, piece_cartographytable, piece_bed02, bed, portal_wood, wood_door, wood_gate, piece_chair, piece_bench01, piece_throne01, Raft, Karve, piece_TrainingDummy, piece_barber, piece_beehive, piece_sapcollector, piece_gift1. (List lives in `Tests/gen_renameit_net_pieces.py`.)

## Logging (new, off by default)

`LogSpam = true` in `[11 UI-NotSynced]` of `com.drakemods.renameit.cfg` on **every** client (and the host, if it runs the mod). Lines start with `[PieceRename]`:

| Line | Meaning |
| --- | --- |
| `SetName: sending to the owner of '<piece>' zdo=… owner=…` | this client is not the owner; it sent an RPC to `owner` |
| `SetName: writing directly (claiming) on …` | this client wrote the ZDO itself |
| `SetName from peer N applied on …` | the owner received it and wrote it |
| `reached a client that is not the owner … dropped` | the RPC arrived at a client that is not (or no longer) the owner |
| `Peer N has no player object on this client … refused` | owner could not identify the sender (Player not loaded) |
| `Peer N (name) may not rename '<piece>': <reason>` | owner refused; reason is the same check as the menu |
| `Alt+Use on '<piece>': refused: <reason>` / `not renamable here …` | why the menu did not open for the local player |
| `Rename RPCs NOT registered on '<piece>'` | the piece cannot receive a rename from anyone |

Read it like this: Player 2 renames. Her log should say *sending to the owner … owner=X*. Whoever is X must log *applied* (or *dropped* / *refused* with the reason). No line on X's side at all means the RPC never arrived (owner is the server, or a client without the piece loaded).

## Setup

Same as the existing server suite (`Tests/suites/DrakesRenameIt-Server`, VS-01..05) and `DrakesTestLab/docs/MULTIPLAYER.md`:

- Dedicated server (crossplay, Docker) with `DrakesTestLabServer`, DrakeModsLibs, RenameIt (same build as the clients) and, if used, LockSmith. `[Control] Enabled = true` on the server mod.
- Player 1: profile `drakeTest`, `[Bootstrap] Target = Server`, `[Connect] Address = <server ip:port>`.
- Player 2: second profile, `[Connect] Role = Player2`, same mods and versions, DrakesTestLab from the `feature/server-control` line (worktree `DrakesTestLab-p2`). Start Player 2 **first**.
- Both clients on this RenameIt build: `beta/1.4-piece-sync` (`dotnet msbuild DrakesRenameIt-1.4fix\DrakeRenameit.csproj` deploys to the profile that `environment.props` points at; copy the DLL to the Player 2 profile too, and to the server's mod folder if the server loads RenameIt). Server and clients must match.
- Valheim launches go through the queue (`DrakesWorkshop/scripts/Valheim-Queue.ps1`). Player 2's game being open blocks every queued launch: tell the user before starting it.
- Set `12 Pieces` on the **server** (it is synced): `PieceRenameEnabled=true`, `PieceSharedEnabled=true`, `PieceLockToBuilder=true`, `PieceRequireWardAccess=false` for the first pass. Run again with `PieceRequireWardAccess=true` and a ward that lists Player 2, then one that does not.

## Commands

Copy the suites to Player 1's profile (the 1.4fix copy, not the refactor):

```powershell
$wt = 'C:\Modding\Vahleim\Mod\DrakesWorkshop\DrakesRenameIt-1.4fix'
$dst = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\drakeTest\BepInEx\config\DrakesTestLab\suites\DrakesRenameIt-Server"
Remove-Item $dst -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "$wt\Tests\suites\DrakesRenameIt-Server" $dst -Recurse
Copy-Item "$wt\Tests\suites\DrakesRenameIt\_shared" "$dst\..\DrakesRenameIt\_shared" -Recurse -Force   # the baseline both suites include
```

Order of runs (each is one queued job; Player 2 already connected):

```powershell
# 0. transport sanity: the existing real-client tests (VIP cfg cheating, synced rules, shared chest, ward, last write wins)
& DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites "DrakesRenameIt-Server/VS-0"
# 1. every kind of piece, Player 2 renames a piece I shared
& DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites "DrakesRenameIt-Server/VS-20"
# 2. every kind of piece, my rename reaches Player 2
& DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites "DrakesRenameIt-Server/VS-21"
# 3. Shared on / off gates Player 2 (menu access)
& DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites "DrakesRenameIt-Server/VS-22"
```

Quick single check of one suspect kind while debugging: `-Suites "DrakesRenameIt-Server/VS-20"` runs all 38; to isolate, copy VS-20 and delete all but the Cart / workbench / chest / cartography table blocks (steps are repeated per piece with names `p0..p37`).

Regenerate suites after editing the kinds: `python Tests\gen_renameit_net_pieces.py` (writes PN-20..22 and VS-20..22).

## What to hand back

1. Which kinds fail in which of VS-20 / 21 / 22 (test report: `…\BepInEx\config\DrakesTestLab\results\latest.html` on Player 1).
2. The `[PieceRename]` lines from the logs of Player 1, Player 2 **and the server** for one failing and one passing kind (`LogOutput.log` in each profile; server: its BepInEx log).
3. For a failing kind, the ZDO owner at the time (`server` queries `zdo.owner`, see MULTIPLAYER.md "Server control") and whether the owner was a client or the server.
4. Anything red in any log from our mods (counts as a fail even if the tests pass).

## After the run

If the logs show the RPC dropped because the server owns the ZDO, the fix is on the sender: claim the piece (or ask a nearby client) instead of sending to a non-instantiated owner. If they show *refused* with a ward or builder reason, the rule needs a decision from the user, not a code change. Do not guess; fix what the logs show, add the failing kind to a PN test that reproduces it, then rerun PN-20..22 and VS-20..22.

## Not done

- The server-side value check (`zdo.value` of `DrakePiece_Name` on the server) is not in VS-20..22: confirm the exact `agree`/`server` query syntax for ZDO values on the server mod, then add a three-way read (player1 / server / player2) like the example in MULTIPLAYER.md.
- Map pin styles through the cartography table (item 3 above): needs a design decision first.
