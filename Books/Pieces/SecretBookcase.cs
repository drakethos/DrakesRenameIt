using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI.Toolkit;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DrakeRenameit.Books;

/// <summary>
/// The secret bookcase door: a Tall Bookcase (1 x 2 m) full of made-up books that is really a door. Only one fake book, the handle,
/// opens it; everywhere else it passes for a bookcase. It is a real <see cref="Door"/> (wards, LockSmith's lock menu, the shared
/// open / closed state and the door sounds all work as for any door); only the animator is replaced by a hinge swing.
/// </summary>
internal static class SecretBookcase
{
    internal const string PrefabName = "Drakes_SecretBookcase";
    internal const string Token = "$piece_drakes_secret_bookcase";
    internal const string HingeName = "drakes_hinge";
    internal const float Width = 1f, Height = 2f;
    internal const int Rows = 4;

    /// <summary>Which fake book is the handle (row * 16 + position in the row) and what its spine says.</summary>
    internal const string ZHandle = "DrakeSecretHandle";
    internal const string ZLabel = "DrakeSecretLabel";
    /// <summary>Seed for the made-up books, picked once when the door is built, so every client draws the same shelves.</summary>
    internal const string ZSeed = "DrakeSecretSeed";
    internal const int DefaultHandle = 2 * 16 + 3;
    internal static string DefaultLabel => BookNames.SecretHandle;

    internal static void Add(ManualLogSource? log, Material wood)
    {
        try
        {
            var loc = LocalizationManager.Instance.GetLocalization();
            loc.AddTranslation("English", Token.TrimStart('$'), "Secret Bookcase Door");
            loc.AddTranslation("Spanish", Token.TrimStart('$'), "Puerta librería secreta");
            loc.AddTranslation("English", Token.TrimStart('$') + "_desc",
                "A tall bookcase that is really a door. One book on it is the handle: pull it to swing the case open. From behind, or once it stands open, it opens and closes like any door. Shift+E sets which book and its title.");

            var config = new PieceConfig
            {
                Name = Token,
                Description = Token + "_desc",
                Enabled = true,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "",
            };
            foreach (var (item, amount) in Paper.Items.PaperItem.ParseCost("FineWood:16,BronzeNails:6"))
                config.AddRequirement(new RequirementConfig(item, amount, 0, true));
            if (BookAssets.BookcaseIcon is { } icon)
                config.Icon = icon;

            var piece = new CustomPiece(PrefabName, "wood_door", config);
            PieceManager.Instance.AddPiece(piece);
            var go = piece.PiecePrefab;
            if (go == null)
                return;

            // Keep the donor's Piece, ZNetView and Door (its sounds and ward rules); drop its look, animator, colliders and wear.
            for (var i = go.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            foreach (var col in go.GetComponents<Collider>())
                Object.DestroyImmediate(col);
            foreach (var wear in go.GetComponents<WearNTear>())
                Object.DestroyImmediate(wear);
            foreach (var animator in go.GetComponents<Animator>())
                Object.DestroyImmediate(animator);

            var door = go.GetComponent<Door>();
            if (door == null)
            {
                log?.LogWarning("[Books] Secret bookcase skipped: wood_door has no Door.");
                return;
            }

            // The hover passes for a plain bookcase.
            door.m_name = BookShelfPiece.CaseToken;
            door.m_keyItem = null;
            door.m_openEnable = null;
            door.m_canNotBeClosed = false;
            door.m_checkGuardStone = true;

            // Everything that swings hangs from a hinge on the left side (seen from the front), half way through the depth.
            var hinge = new GameObject(HingeName).transform;
            hinge.SetParent(go.transform, false);
            hinge.localPosition = new Vector3(-Width / 2f, 0f, 0f);
            var swing = new GameObject("drakes_swing").transform;
            swing.SetParent(hinge, false);
            swing.localPosition = new Vector3(Width / 2f, 0f, 0f);
            var frame = new GameObject(BookShelfPiece.FrameName).transform;
            frame.SetParent(swing, false);
            frame.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var look = new GameObject("drakes_shelf_mesh");
            look.transform.SetParent(frame, false);
            look.AddComponent<MeshFilter>().sharedMesh = BookMesh.Bookcase(Width, Rows, Height);
            look.AddComponent<MeshRenderer>().sharedMaterial = wood;
            BookShelfPiece.Colliders(frame.gameObject, BookShelfPiece.TallVariant);
            BookShelfPiece.AddSnapPoints(go, BookShelfPiece.TallVariant);

            var p = go.GetComponent<Piece>();
            if (p != null)
            {
                p.m_name = Token;
                p.m_category = Piece.PieceCategory.Furniture;
                p.m_craftingStation = null;
            }

            go.AddComponent<SecretBookcaseBehaviour>();
            log?.LogInfo("[Books] Registered the secret bookcase door.");
        }
        catch (Exception ex)
        {
            log?.LogError($"[Books] Failed to register the secret bookcase door: {ex}");
        }
    }

    /// <summary>Shift+E on the secret door is its own (LockSmith's menu or the Secret door window), never RenameIt's piece rename.</summary>
    internal static void Claim() =>
        DrakeModsLibs.API.PieceInteractionClaims.Register("DrakesRenameIt.secretdoor", piece => piece != null && piece.GetComponent<SecretBookcaseBehaviour>() != null);

    internal static bool CanConfigure(SecretBookcaseBehaviour door)
    {
        var piece = door.GetComponent<Piece>();
        var player = Player.m_localPlayer;
        return piece != null && player != null && (piece.IsCreator() || API.RenameitPermission.IsAdminOrVIP(player));
    }
}

/// <summary>Marks one made-up book on the secret door, so aiming can tell which one (and whether it is the handle).</summary>
internal sealed class SecretBookTag : MonoBehaviour
{
    internal int Slot;
    internal string Title = "";
}

/// <summary>
/// The secret door's look and swing. Books are made up from the door's seed (the same on every client), one of them is the handle
/// with the owner's label on its spine. The swing follows the Door's own shared state (0 closed, 1 / -1 open either way).
/// </summary>
internal sealed class SecretBookcaseBehaviour : MonoBehaviour
{
    const float SwingDegreesPerSecond = 160f;
    const int Columns = 8;

    /// <summary>The door state the sounds last played for (int.MinValue before the first one, so loading plays nothing).</summary>
    internal int LastState = int.MinValue;

    ZNetView? _nview;
    Transform? _hinge;
    Transform? _books;
    float _angle;
    string _shown = "";

    internal bool Swinging => Mathf.Abs(_angle - Target) > 0.5f;

    float Target
    {
        get
        {
            var state = _nview != null && _nview.IsValid() ? _nview.GetZDO().GetInt(ZDOVars.s_state) : 0;
            // State 1 / -1 says which side the opener stood on; the case swings away from them.
            return -state * 90f;
        }
    }

    void Awake()
    {
        _nview = GetComponent<ZNetView>();
        _hinge = transform.Find(SecretBookcase.HingeName);
        if (_nview != null && _nview.IsValid() && _nview.IsOwner() && _nview.GetZDO().GetInt(SecretBookcase.ZSeed) == 0)
            _nview.GetZDO().Set(SecretBookcase.ZSeed, UnityEngine.Random.Range(1, int.MaxValue));
        _angle = Target;
        ApplyAngle();
    }

    void Update()
    {
        if (_hinge == null)
            return;
        var target = Target;
        if (Mathf.Abs(_angle - target) > 0.01f)
        {
            _angle = Mathf.MoveTowards(_angle, target, SwingDegreesPerSecond * Time.deltaTime);
            ApplyAngle();
        }

        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;
        var signature = Seed + "|" + Handle + "|" + Label;
        if (signature != _shown)
        {
            _shown = signature;
            BuildBooks();
        }
    }

    void ApplyAngle()
    {
        if (_hinge != null)
            _hinge.localRotation = Quaternion.Euler(0f, _angle, 0f);
    }

    internal int Seed => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetInt(SecretBookcase.ZSeed, 1) : 1;

    internal int Handle => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetInt(SecretBookcase.ZHandle, SecretBookcase.DefaultHandle) : SecretBookcase.DefaultHandle;

    internal string Label
    {
        get
        {
            var label = _nview != null && _nview.IsValid() ? _nview.GetZDO().GetString(SecretBookcase.ZLabel, "") : "";
            return string.IsNullOrWhiteSpace(label) ? SecretBookcase.DefaultLabel : label;
        }
    }

    internal void Configure(int handle, string label)
    {
        if (_nview == null || !_nview.IsValid())
            return;
        _nview.ClaimOwnership();
        _nview.GetZDO().Set(SecretBookcase.ZHandle, handle);
        _nview.GetZDO().Set(SecretBookcase.ZLabel, (label ?? "").Trim().Length > 40 ? label!.Trim().Substring(0, 40) : (label ?? "").Trim());
    }

    /// <summary>One made-up book: its slot, title and look, the same on every client.</summary>
    internal struct FakeBook
    {
        internal int Slot;
        internal string Title;
        internal Dictionary<string, string> Data;
        internal float Height, Thickness;
    }

    /// <summary>The made-up books row by row (row 0 at the top), packed like a full shelf.</summary>
    internal List<List<FakeBook>> Books()
    {
        var rng = new System.Random(Seed);
        var clear = BookMesh.Pitch(SecretBookcase.Rows, SecretBookcase.Height) - BookMesh.CaseTh;
        var usable = SecretBookcase.Width - 2f * BookMesh.CaseTh - 0.02f;
        var slotWidth = usable / Columns;
        var rows = new List<List<FakeBook>>();
        for (var row = 0; row < SecretBookcase.Rows; row++)
        {
            var books = new List<FakeBook>();
            var count = 7 + rng.Next(2);
            for (var i = 0; i < count; i++)
            {
                var slot = row * 16 + i;
                var handle = slot == Handle;
                var data = new Dictionary<string, string>
                {
                    [BookData.KeyColor] = ColorUtility.ToHtmlStringRGB(BookLook.Covers[rng.Next(BookLook.Covers.Length)]),
                    [BookLook.KeyPattern] = rng.Next((int)BookPattern.Studs + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    [BookLook.KeyAccent] = ColorUtility.ToHtmlStringRGB(BookLook.Accents[rng.Next(BookLook.Accents.Length)]),
                    [BookLook.KeyInk] = ColorUtility.ToHtmlStringRGB(BookLook.Inks[rng.Next(2)]),
                };
                var roll = rng.Next(10);
                var share = roll < 2 ? 0.74f : roll > 7 ? 0.94f : 0.84f;
                var thickness = slotWidth * Mathf.Lerp(0.82f, 1.15f, (float)rng.NextDouble());
                var titles = BookNames.SecretTitles;
                var title = titles[rng.Next(titles.Length)];
                books.Add(new FakeBook
                {
                    Slot = slot, Title = handle ? Label : title, Data = data, Height = (clear - 0.01f) * share, Thickness = thickness,
                });
            }

            rows.Add(books);
        }

        return rows;
    }

    void BuildBooks()
    {
        var frame = transform.Find(SecretBookcase.HingeName + "/drakes_swing/" + BookShelfPiece.FrameName);
        if (frame == null)
            return;
        if (_books != null)
            Destroy(_books.gameObject);
        _books = new GameObject("drakes_secret_books").transform;
        _books.SetParent(frame, false);

        var usable = SecretBookcase.Width - 2f * BookMesh.CaseTh - 0.02f;
        var pitch = BookMesh.Pitch(SecretBookcase.Rows, SecretBookcase.Height);
        var layer = LayerMask.NameToLayer("piece");
        var live = _nview != null && _nview.IsValid();
        var rows = Books();
        for (var row = 0; row < rows.Count; row++)
        {
            var books = rows[row];
            var gap = usable / Columns * 0.05f;
            var total = gap * Mathf.Max(0, books.Count - 1);
            foreach (var b in books)
                total += b.Thickness;
            var squeeze = total > usable ? usable / total : 1f;
            var cursor = -usable / 2f;
            var floor = BookMesh.CasePlinth + pitch * (SecretBookcase.Rows - 1 - row);
            foreach (var b in books)
            {
                var t = b.Thickness * squeeze;
                var d = Mathf.Min(b.Height * 0.74f, BookMesh.CaseD - 0.06f);
                var holder = new GameObject("book").transform;
                holder.SetParent(_books, false);
                holder.localPosition = new Vector3(cursor + t / 2f, floor, -BookMesh.CaseD / 2f + 0.02f + d / 2f);
                BookAssets.Build(holder, d, b.Height, t, b.Data, b.Title);
                // Only a built door gets clickable books: on the build ghost they caught the placement ray and the ghost jumped
                // about every frame.
                if (live)
                {
                    var box = holder.gameObject.AddComponent<BoxCollider>();
                    box.center = new Vector3(0f, b.Height / 2f, 0f);
                    box.size = new Vector3(t, b.Height, d);
                    if (layer >= 0)
                        holder.gameObject.layer = layer;
                }
                else
                {
                    SetLayer(holder, frame.gameObject.layer);
                }
                var tag = holder.gameObject.AddComponent<SecretBookTag>();
                tag.Slot = b.Slot;
                tag.Title = b.Title;
                cursor += t + gap * squeeze;
            }
        }
    }

    /// <summary>Tests: pretend the crosshair is on this slot (-1 = on the case, not a book; null = use the real crosshair).</summary>
    internal static int? TestAim;

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayer(child, layer);
    }

    /// <summary>The made-up book under the crosshair on this door, if any.</summary>
    internal SecretBookTag? Aimed()
    {
        if (TestAim is { } slot)
        {
            if (slot < 0 || _books == null)
                return null;
            foreach (var tag in _books.GetComponentsInChildren<SecretBookTag>())
                if (tag.Slot == slot)
                    return tag;
            return null;
        }

        var cam = Camera.main;
        if (cam == null)
            return null;
        var hits = Physics.RaycastAll(new Ray(cam.transform.position, cam.transform.forward), 8f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<Player>() != null)
                continue;
            if (hit.collider.GetComponentInParent<SecretBookcaseBehaviour>() != this)
                return null;
            return hit.collider.GetComponent<SecretBookTag>();
        }

        return null;
    }

    internal bool AimingAtHandle() => Aimed() is { } tag && tag.Slot == Handle;

    /// <summary>True while it stands open (the shared door state is not 0).</summary>
    internal bool IsOpen => _nview != null && _nview.IsValid() && _nview.GetZDO().GetInt(ZDOVars.s_state) != 0;

    /// <summary>The books face +Z; behind is the plain back, the room side of a hidden door.</summary>
    internal bool IsBehind(Vector3 position) => Vector3.Dot(position - transform.position, transform.forward) < 0f;

    /// <summary>
    /// Use works without the handle from behind (inside the hidden room it is plainly a door) and while it stands open (anyone
    /// can see it is a door then, so it closes from either side). From the front, closed, only the handle opens it.
    /// </summary>
    internal bool OpensWithoutHandle(Humanoid? who) => IsOpen || who != null && IsBehind(who.transform.position);
}

/// <summary>
/// The door without its animator: the hinge swing stands in for it. Hover and E only act through the handle; everything else on the
/// case reads as a bookcase. Shift+E belongs to LockSmith's lock menu when it opens here (with our Secret door tab), else our window.
/// </summary>
[HarmonyPatch]
internal static class SecretBookcasePatches
{
    static SecretBookcaseBehaviour? Secret(Door door) => door != null ? door.GetComponent<SecretBookcaseBehaviour>() : null;

    static readonly MethodInfo? EffectCreate = AccessTools.Method(typeof(EffectList), "Create");

    /// <summary>
    /// EffectList.Create through reflection with the game's own defaults: Valheim 1.0 added a parameter the build stubs don't have,
    /// so a compiled call throws MissingMethodException in game.
    /// </summary>
    static void PlayEffects(EffectList? effects, Transform at)
    {
        if (effects == null || EffectCreate == null)
            return;
        try
        {
            var ps = EffectCreate.GetParameters();
            var args = new object?[ps.Length];
            args[0] = at.position;
            args[1] = at.rotation;
            for (var i = 2; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            EffectCreate.Invoke(effects, args);
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[Books] Secret door sound skipped: {ex.GetBaseException().Message}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Door), "SetState")]
    static bool SetState(Door __instance, int state)
    {
        if (Secret(__instance) is not { } secret)
            return true;
        // Sounds as the vanilla door plays them; the swing itself follows the ZDO state in the behaviour's Update.
        var last = secret.LastState;
        if (last != int.MinValue && last != state)
            PlayEffects(state != 0 ? __instance.m_openEffects : __instance.m_closeEffects, __instance.transform);

        secret.LastState = state;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Door), "CanInteract")]
    static bool CanInteract(Door __instance, ref bool __result)
    {
        if (Secret(__instance) is not { } secret)
            return true;
        __result = !secret.Swinging;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Door), nameof(Door.GetHoverText))]
    static void HoverText(Door __instance, ref string __result)
    {
        if (Secret(__instance) is not { } secret || string.IsNullOrEmpty(__result))
            return;
        var aimed = secret.Aimed();
        var name = Localization.instance != null ? Localization.instance.Localize(__instance.m_name) : __instance.m_name;
        var player = Player.m_localPlayer;
        if (player != null && secret.IsBehind(player.transform.position) && !secret.IsOpen)
        {
            // From the hidden room it is plainly a door: its real name and the door's own line.
            var piece = __instance.GetComponent<Piece>();
            var real = piece != null && Localization.instance != null ? Localization.instance.Localize(piece.m_name) : name;
            var cut = __result.IndexOf('\n');
            __result = real + (cut >= 0 ? __result.Substring(cut) : "");
            return;
        }

        if (secret.IsOpen && (aimed == null || aimed.Slot != secret.Handle))
        {
            // Standing open it is plainly a door from either side: the door's own line (Close).
            var cut = __result.IndexOf('\n');
            __result = name + (cut >= 0 ? __result.Substring(cut) : "");
            return;
        }

        if (aimed != null && aimed.Slot == secret.Handle)
        {
            // The handle: its label in place of the bookcase's name, then the door's own line (open / close / no access).
            var nl = __result.IndexOf('\n');
            __result = secret.Label + "\n<color=#b9ab91>" + name + "</color>" + (nl >= 0 ? __result.Substring(nl) : "");
            return;
        }

        // Anywhere else it is just a bookcase. Whoever may set it up also sees where the settings are.
        __result = aimed != null ? aimed.Title + "\n<color=#b9ab91>" + name + "</color>" : name;
        if (SecretBookcase.CanConfigure(secret) && Localization.instance != null)
            __result += Localization.instance.Localize("\n[<color=yellow><b>Shift</b></color>+<color=yellow><b>$KEY_Use</b></color>] Secret door");
    }

    /// <summary>E only through the handle. Shift+E: LockSmith's menu takes it when it opens here; otherwise our settings window.</summary>
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
    static bool Interact(Door __instance, Humanoid character, bool hold, bool alt, ref bool __result, bool __runOriginal)
    {
        if (Secret(__instance) is not { } secret)
            return true;
        if (!__runOriginal)
            return false;
        if (alt)
        {
            if (character == Player.m_localPlayer && !hold && SecretBookcase.CanConfigure(secret))
                SecretDoorSettings.Show(secret);
            __result = true;
            return false;
        }

        if (secret.AimingAtHandle() || secret.OpensWithoutHandle(character))
            return true;
        __result = false;
        return false;
    }

    /// <summary>A key item used on the case does nothing either (the door has no key).</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Door), nameof(Door.UseItem))]
    static bool UseItem(Door __instance, ref bool __result)
    {
        if (Secret(__instance) == null)
            return true;
        __result = false;
        return false;
    }
}

/// <summary>Which made-up book is the handle and what its spine says: a Secret door tab in LockSmith's menu, or its own window.</summary>
internal static class SecretDoorSettings
{
    internal const string TabId = "renameit.secretdoor";
    static UkWindow? _window;

    internal static void RegisterTab() =>
        PieceMenuTabs.Register(new PieceMenuTab(TabId, "Secret door", _ => false, ctx =>
        {
            var secret = ctx.Host != null ? ctx.Host.GetComponentInParent<SecretBookcaseBehaviour>() : null;
            if (secret != null)
                Build(ctx.Body, secret, ctx.Rebuild);
        }, group: TabId, isRoot: true)
        {
            AppliesToHost = host => host != null && host.GetComponentInParent<SecretBookcaseBehaviour>() is { } s && SecretBookcase.CanConfigure(s),
        });

    internal static void Show(SecretBookcaseBehaviour secret)
    {
        _window ??= new UkWindow("drakes_secret_door", 720f);
        void Rebuild()
        {
            _window.Body.Clear();
            _window.SetHeader("Secret door", "Pick the handle and its title");
            Build(_window.Body, secret, Rebuild);
        }

        Rebuild();
        _window.ShowFooter(false);
        _window.Open();
    }

    internal static void CloseWindow() => _window?.CloseSilent();

    internal static VisualElement? TestCard => _window != null && _window.IsOpen ? _window.Card : null;

    static void Build(VisualElement body, SecretBookcaseBehaviour secret, Action rebuild)
    {
        body = ShelfLayout.Scrolled(body);
        var label = new UkField("Handle title", multiline: false);
        label.SetMax(40);
        label.SetValue(secret.Label);
        label.SubmitPressed = () => secret.Configure(secret.Handle, label.Value);
        label.Root.style.marginBottom = 6;
        body.Add(label.Root);
        var save = UkControls.MakeButton("Set title", () =>
        {
            secret.Configure(secret.Handle, label.Value);
            body.schedule.Execute(rebuild).StartingIn(150);
        }, UkButtonKind.Primary);
        save.style.alignSelf = Align.FlexStart;
        body.Add(save);

        var hint = UkControls.Text("Click the book that opens the door. It looks like any other book on the case; only you know which.", 14, UkTheme.TextMuted);
        hint.style.marginTop = 14;
        hint.style.marginBottom = 6;
        body.Add(hint);

        var rows = secret.Books();
        for (var row = 0; row < rows.Count; row++)
        {
            var strip = new VisualElement().Row(Align.FlexEnd).Fill(UkTheme.Field).Round(UkTheme.Radius).Pad(8, 10);
            strip.style.marginTop = 6;
            strip.style.minHeight = 92;
            strip.style.borderBottomWidth = 6;
            strip.style.borderBottomColor = new Color(0.42f, 0.30f, 0.18f);
            foreach (var book in rows[row])
            {
                var slot = book.Slot;
                var cover = BookLook.ColorOf(book.Data, BookData.KeyColor, BookLook.Covers[0]);
                var isHandle = slot == secret.Handle;
                var spine = new VisualElement().Size(width: 26, height: Mathf.Lerp(56f, 80f, (book.Height - 0.28f) / 0.14f));
                spine.style.backgroundImage = new StyleBackground(BookLook.Spine(cover, BookLook.PatternOf(book.Data),
                    BookLook.ColorOf(book.Data, BookLook.KeyAccent, BookLook.Accents[0])));
                spine.Border(isHandle ? UkTheme.Gold : Color.black, isHandle ? 3 : 1).Round(3);
                spine.style.marginRight = 3;
                spine.tooltip = isHandle ? "The handle: " + book.Title : book.Title;
                spine.RegisterCallback<ClickEvent>(_ =>
                {
                    secret.Configure(slot, label.Value);
                    body.schedule.Execute(rebuild).StartingIn(150);
                });
                strip.Add(spine);
            }

            body.Add(strip);
        }
    }
}
