using DrakeModsLibs.UI.Toolkit;
using System;
using System.Linq;
using DrakeModsLibs.UI;
using DrakeRenameit.Paper.Items;
using DrakeRenameit.Permissions;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeRenameit.UI.Toolkit;

/// <summary>
/// The Item Workshop (Rename tab) in UI Toolkit: one panel, edit in place, no Yes/No popups.
/// Name / description / crafted-by are plain fields; Save writes them all at once and offers Undo;
/// Reset all asks inline; a locked item shows why and how to unlock right where the fields are.
/// Logic is not duplicated: edits go through <c>DrakeRenameit.CommitWorkshopEdits</c> and the existing permission checks.
/// </summary>
internal sealed class WorkshopPanel
{
    // Strings are English for now; move to RenameItLocalization (English/Spanish json) when the design settles.
    const string TitleName = "Name";
    const string TitleDesc = "Description";
    const string TitleCrafted = "Crafted-by line";

    static WorkshopPanel? _instance;

    /// <summary>Where the player dragged the panel to (kept for the session).</summary>
    static Vector2 _offset;

    readonly UkScreen _screen;
    readonly VisualElement _scrim;
    readonly VisualElement _stage;
    readonly WorkshopPreview _preview;
    readonly Button _cogButton;
    readonly UkPopover _cogMenu;
    readonly QuickSettingsContent _settings;
    readonly VisualElement _iconSlot;
    readonly Label _title;
    readonly Label _subtitle;
    readonly VisualElement _tabs;
    readonly VisualElement _lockedCard;
    readonly Label _lockedText;
    readonly Label _lockedCost;
    readonly Button _unlock;
    readonly UkField _name;
    readonly UkField _desc;
    readonly UkField _crafted;
    readonly UkRichToolbar _richBar;
    readonly UkSwitch _shared;
    readonly UkConfirm _resetAll;
    readonly Button _revert;
    readonly Button _save;
    readonly UkToast _toast;
    readonly UkTooltip _tips;
    readonly UkDropdown _labelPicker;

    ItemDrop.ItemData? _item;
    bool _open;
    bool _dragging;
    string _savedName = "";
    string _savedDesc = "";
    string _savedCrafted = "";
    int _savedLabelIndex;
    System.Collections.Generic.List<string> _labelOptions = new();
    string? _unlockReason;
    string? _resetAllReason;
    string? _sharedReason;
    DrakeRenameit.WorkshopSnapshot? _undo;

    public static bool IsOpen => _instance != null && _instance._open;

    /// <summary>Opens (or refreshes) the panel for <paramref name="item"/>. Called by the Rename tab.</summary>
    public static void Show(ItemDrop.ItemData item)
    {
        _instance ??= new WorkshopPanel();
        _instance.Open(item);
    }

    /// <summary>The tab host switched away or closed: hide without telling the host again.</summary>
    public static void HideFromHost() => _instance?.Close(notifyHost: false);

    /// <summary>Esc: cancels an inline confirm first, then closes. True when it handled the key.</summary>
    public static bool TryHandleEscape()
    {
        var panel = _instance;
        if (panel == null || !panel._open)
            return false;
        if (panel._richBar.PickerOpen)
        {
            panel._richBar.ClosePicker();
            return true;
        }

        if (panel._cogMenu.IsOpen)
        {
            panel._cogMenu.Close();
            return true;
        }

        if (panel._labelPicker.IsOpen)
        {
            panel._labelPicker.Close();
            return true;
        }

        if (panel._resetAll.IsAsking)
        {
            panel._resetAll.Cancel();
            return true;
        }

        panel.Close(notifyHost: true);
        return true;
    }

    WorkshopPanel()
    {
        _screen = UkScreen.Create("drakes_workshop_ui");

        _scrim = new VisualElement().Fill(UkTheme.Scrim);
        _scrim.style.position = Position.Absolute;
        _scrim.style.left = _scrim.style.right = _scrim.style.top = _scrim.style.bottom = 0;
        _scrim.style.alignItems = Align.Center;
        _scrim.style.justifyContent = Justify.Center;
        _scrim.pickingMode = PickingMode.Position; // swallows clicks so nothing in the inventory behind is hit by accident
        _scrim.Show(false);
        _screen.Root.Add(_scrim);
        // Added after the scrim so hover tooltips draw above the panel.
        _tips = new UkTooltip(_screen.Root);

        var card = new VisualElement().Column().Fill(UkTheme.Panel).Round(UkTheme.RadiusLarge).Border(UkTheme.Line);
        card.style.width = 640;
        card.style.maxHeight = 1020;
        card.style.overflow = Overflow.Hidden;
        // Stage = [preview card][workshop window]; dragging moves both. The preview hides itself when switched off.
        _stage = new VisualElement().Row(Align.FlexStart);
        _scrim.Add(_stage);
        _preview = new WorkshopPreview();
        _preview.Root.style.marginTop = 70;
        _preview.Root.style.marginRight = 24;
        _stage.Add(_preview.Root);
        _stage.Add(card);

        // Header: icon, title, state line, close.
        var header = new VisualElement().Row().Pad(18, 22).BorderBottom(UkTheme.LineSoft);
        _iconSlot = new VisualElement().Fixed();
        header.Add(_iconSlot);
        var titles = new VisualElement().Column().Grow();
        titles.style.marginLeft = 14;
        _title = UkControls.Text("", 24, UkTheme.Text, heading: true, wrap: false);
        _subtitle = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        titles.Add(_title);
        titles.Add(_subtitle);
        header.Add(titles);
        _cogButton = UkControls.MakeIconButton("", () => _cogMenu!.Toggle(_cogButton!), "Quick settings", UkButtonKind.Secondary);
        _cogButton.style.alignItems = Align.Center;
        _cogButton.style.justifyContent = Justify.Center;
        _cogButton.style.marginRight = 8;
        _cogButton.Add(UkIcons.Gear(22f, UkTheme.TextMuted));
        header.Add(_cogButton);
        header.Add(UkControls.MakeIconButton("✕", () => Close(notifyHost: true), "Close (Esc)", UkButtonKind.Secondary));
        card.Add(header);
        MakeDraggable(header);

        // Quick settings: this device only. The same controls as the standalone window (QuickSettingsContent).
        _cogMenu = new UkPopover(card, 430f);
        _settings = new QuickSettingsContent(withHeader: true);
        _settings.PreviewChanged = ApplyPreviewVisibility;
        _cogMenu.Content.Add(_settings.Root);

        // Tabs (only when more than one mod offers something for this item).
        _tabs = new VisualElement().Row().Pad(10, 22).BorderBottom(UkTheme.LineSoft);
        _tabs.Show(false);
        card.Add(_tabs);

        var body = new VisualElement().Column().Pad(20, 22);
        card.Add(body);

        _lockedCard = new VisualElement().Column().Fill(UkTheme.Raised).Round(UkTheme.Radius + 2).Pad(14);
        _lockedText = UkControls.Text("", 15, UkTheme.Text);
        var lockedRow = new VisualElement().Row();
        lockedRow.style.marginTop = 12;
        _lockedCost = UkControls.Text("", 14, UkTheme.TextMuted);
        _lockedCost.style.flexGrow = 1;
        _unlock = UkControls.MakeButton("Unlock", OnUnlock, UkButtonKind.Primary);
        lockedRow.Add(_lockedCost);
        lockedRow.Add(_unlock);
        _tips.Attach(lockedRow, () => _unlockReason);
        _lockedCard.Add(_lockedText);
        _lockedCard.Add(lockedRow);
        _lockedCard.Margin(bottom: 16);
        body.Add(_lockedCard);

        _name = new UkField(TitleName, multiline: false, resetTooltip: "Back to the original name", tips: _tips);
        _name.Changed = _ => OnEdited();
        _name.ResetClicked = () =>
        {
            _name.SetValue(DrakeRenameit.GetVanillaDisplayName(_item));
            OnEdited();
        };
        _name.SubmitPressed = Save;
        _name.Root.style.marginBottom = 16;
        body.Add(_name.Root);

        _desc = new UkField(TitleDesc, multiline: true, rows: 3, resetTooltip: "Back to the original description", tips: _tips);
        _desc.Changed = _ => OnEdited();
        _desc.ResetClicked = () =>
        {
            _desc.SetValue(DrakeRenameit.GetVanillaDisplayDesc(_item));
            OnEdited();
        };
        _desc.Root.style.marginBottom = 16;
        body.Add(_desc.Root);

        // Name and description take rich text: one toolbar writes the tags (closed correctly) and the limit ignores them.
        _name.EnableRich();
        _desc.EnableRich();
        _richBar = new UkRichToolbar(_tips, card, () => RenameitConfig.RecentColors, value => RenameitConfig.RecentColors = value,
            (TitleName, _name), (TitleDesc, _desc));
        _richBar.Root.style.marginBottom = 10;
        body.Insert(body.IndexOf(_name.Root) + 1, _richBar.Root);

        _crafted = new UkField(TitleCrafted, multiline: false, resetTooltip: "Back to the original crafter and label", tips: _tips);
        _crafted.Changed = _ => OnEdited();
        _crafted.ResetClicked = () =>
        {
            _crafted.SetValue(_item?.m_crafterName ?? "");
            _labelPicker.SetIndex(0);
            OnEdited();
        };
        // The word before the name in the tooltip ("Crafted by", "Belongs To"...), picked from the list the server allows.
        _labelPicker = new UkDropdown(card, _tips);
        _labelPicker.Root.style.marginRight = 8;
        _labelPicker.Changed = _ => OnEdited();
        _crafted.Line.Insert(0, _labelPicker.Root);
        // A click anywhere else closes the open list.
        _scrim.RegisterCallback<PointerDownEvent>(e =>
        {
            var target = e.target as VisualElement;
            if (_labelPicker.IsOpen && !_labelPicker.Contains(target))
                _labelPicker.Close();
            if (_cogMenu.IsOpen && !_cogMenu.Contains(target) && !_cogButton.Contains(target))
                _cogMenu.Close();
            _richBar.CloseIfOutside(target);
        }, TrickleDown.TrickleDown);
        _crafted.SubmitPressed = Save;
        _crafted.Root.style.marginBottom = 16;
        body.Add(_crafted.Root);

        _shared = new UkSwitch("Shared", "Anyone can rewrite this item's name and description. Applies immediately.");
        _shared.Changed = OnSharedChanged;
        _tips.Attach(_shared.Root, () => _sharedReason);
        body.Add(_shared.Root);

        // Footer: reset all (inline confirm), hint, revert, save.
        var footer = new VisualElement().Row().Pad(14, 22).BorderTop(UkTheme.LineSoft).Fill(UkTheme.PanelDeep);
        _resetAll = new UkConfirm("Reset all", "Reset name, description and crafter?", "Reset", OnResetAll);
        footer.Add(_resetAll.Root);
        _tips.Attach(_resetAll.Root, () => _resetAllReason);
        footer.Add(UkControls.Spacer());
        var hint = UkControls.Text("Enter save · Esc close", 13, UkTheme.TextMuted, wrap: false);
        hint.style.marginRight = 14;
        footer.Add(hint);
        _revert = UkControls.MakeButton("Revert", RevertFields, UkButtonKind.Secondary);
        _revert.style.marginRight = 8;
        footer.Add(_revert);
        _save = UkControls.MakeButton("Save", Save, UkButtonKind.Primary, width: 110);
        footer.Add(_save);
        card.Add(footer);

        _toast = new UkToast();
        _toast.Root.style.marginLeft = _toast.Root.style.marginRight = 22;
        _toast.Root.style.marginBottom = 14;
        _toast.Root.style.marginTop = 0;
        card.Add(_toast.Root);
    }

    // ---------------------------------------------------------------- open / close

    void Open(ItemDrop.ItemData item)
    {
        _item = item;
        _undo = null;
        DrakeRenameit.CurrentItem = item;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureBlocked();
        HideLibsTabStrip();
        _toast.Hide();
        _resetAll.Cancel();
        _screen.RaiseAboveCanvases();
        _cogMenu.Close();
        _settings.Sync();
        ApplyPreviewVisibility();
        _scrim.Show(true);
        _open = true;
        ApplyOffset();
        Refresh();
        LogDiagnostics();
    }

    void Close(bool notifyHost)
    {
        if (!_open)
            return;
        _open = false;
        _scrim.Show(false);
        _toast.Hide();
        _resetAll.Cancel();
        _cogMenu.Close();
        _item = null;
        _undo = null;
        DrakeRenameit.CurrentItem = null;
        DrakeModsLibs.UI.DrakeGuiInput.EnsureUnblocked();
        if (notifyHost)
            DrakeTabHost.Close();
    }

    /// <summary>
    /// The shared tab host draws its own Jotunn strip when several mods have a tab. This panel has its own tab row, so
    /// hide that strip (stopgap until the host itself is UI Toolkit; it's recreated on the next open).
    /// </summary>
    static void HideLibsTabStrip()
    {
        if (!GUIManager.CustomGUIFront)
            return;
        var strip = GUIManager.CustomGUIFront.transform.Find("drake_tab_strip_root");
        if (strip != null)
            strip.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- state -> view

    void Refresh()
    {
        var item = _item;
        if (item == null)
            return;

        var player = Player.m_localPlayer;
        var locked = DrakeRenameit.ShowUnlockButton(item);
        var blank = PaperItem.IsBlankPaper(item);
        var canName = !locked && DrakeRenameit.CanChangeName(item, false);
        var canDesc = !locked && DrakeRenameit.CanChangeDesc(item, false);
        var canCrafted = !locked && !blank && DrakeRenameit.CanChangeCraftedByLabel(item, false);
        var reason = DrakeRenameit.GetMenuBlockedReason(item);

        // Vanilla/Jotunn items store tokens like $item_x; show what the tooltip shows.
        _savedName = Loc(DrakeRenameit.GetPropperName(item));
        _savedDesc = Loc(DrakeRenameit.getPropperDesc(item));
        _savedCrafted = DrakeRenameit.getCraftedByDisplay(item) ?? "";

        _iconSlot.Clear();
        _iconSlot.Add(UkControls.IconTile(item.GetIcon(), 56));
        _title.text = DrakeRenameit.GetVanillaDisplayName(item);
        _preview.SetItem(item.GetIcon(), DrakeRenameit.GetVanillaDisplayName(item));

        // Locked: say why and offer the way through, right where the fields are.
        _lockedCard.Show(locked);
        if (locked)
        {
            _lockedText.text = "Renaming this item costs a one-time fee. Pay once and it stays unlocked.";
            var cost = RenameUnlockCost.GetCostDisplayShort();
            var afford = player != null && RenameUnlockCost.CanPlayerAfford(player);
            _lockedCost.text = string.IsNullOrEmpty(cost) ? "" : afford ? cost : cost + "  ·  you don't have enough";
            UkControls.SetButtonEnabled(_unlock, afford);
            _unlockReason = afford ? null : "You need " + (string.IsNullOrEmpty(cost) ? "more" : cost) + " in your inventory.";
        }

        var costText = RenameUnlockCost.GetCostDisplayShort();
        var lockedWhy = string.IsNullOrEmpty(costText) ? "Unlock renaming first." : "Unlock renaming first (" + costText + ").";
        var fallback = string.IsNullOrEmpty(reason) ? "You can't change this on this item." : reason;
        _name.SetMax(RenameitConfig.NameCharLimit);
        _desc.SetMax(RenameitConfig.DescCharLimit);
        _crafted.SetMax(RenameitConfig.CraftedByCharLimit);
        _name.SetEditable(canName, locked ? lockedWhy : fallback);
        _desc.SetEditable(canDesc, locked ? lockedWhy : fallback);
        var craftedWhy = locked ? lockedWhy : blank ? "Not available for blank paper." : fallback;
        _crafted.SetEditable(canCrafted, craftedWhy);

        // The "Crafted by" label: pick from the allowed list when the server lets players customize it.
        var labels = RenameitConfig.GetCraftedByAllowedLabelsList();
        var mayLabel = DrakeRenameit.MayEditCraftedByLineLabel();
        var stored = DrakeRenameit.GetStoredCraftedByLineLabel(item);
        _labelOptions = labels;
        _savedLabelIndex = 0;
        if (mayLabel && stored != null)
        {
            for (var i = 1; i < labels.Count; i++)
            {
                if (!string.Equals(labels[i], stored, StringComparison.Ordinal))
                    continue;
                _savedLabelIndex = i;
                break;
            }
        }

        var labelNames = labels.Select((l, i) => i == 0 ? DefaultCraftedByCaption(labels) : l).ToList();
        _labelPicker.SetOptions(labelNames, _savedLabelIndex);
        _labelPicker.SetEnabled(canCrafted && mayLabel, !canCrafted ? craftedWhy : "Custom labels are turned off on this server.");
        LoadFields();

        var showShared = RenameitConfig.PublicRewriteEnabled && !blank;
        _shared.Root.Show(showShared);
        if (showShared)
        {
            var may = player != null && RenamePermissionManager.CanChangePublicFlag(item, player);
            _sharedReason = may ? null : "Only the original creator or an admin can change this.";
            _shared.SetValue(RenamePermissionManager.HasPublicRewriteFlag(item));
            _shared.SetEnabled(may, may ? "Anyone can rewrite this item's name and description. Applies immediately."
                                        : "Only the original creator or an admin can change this.");
        }

        var canReset = !locked && DrakeRenameit.CanResetAnyCustomization(item);
        _resetAll.SetEnabled(canReset);
        _resetAllReason = canReset ? null : locked ? lockedWhy : "Nothing to reset: this item has no custom name, description or crafter.";
        RebuildTabs(item);
        OnEdited();
    }

    /// <summary>Puts the saved values into the fields (also the Revert button).</summary>
    void LoadFields()
    {
        _name.SetValue(_savedName);
        _desc.SetValue(_savedDesc);
        _crafted.SetValue(_savedCrafted);
        _labelPicker.SetIndex(_savedLabelIndex);
    }

    void RevertFields()
    {
        LoadFields();
        OnEdited();
    }

    /// <summary>Called on every keystroke: refresh change marks, reset buttons, Save/Revert availability.</summary>
    void OnEdited()
    {
        var item = _item;
        if (item == null)
            return;

        var nameChanged = _name.Value != _savedName;
        var descChanged = _desc.Value != _savedDesc;
        var craftedChanged = _crafted.Value != _savedCrafted;
        var labelChanged = _labelPicker.SelectedIndex != _savedLabelIndex;
        _labelPicker.SetChanged(labelChanged);
        _name.SetChanged(nameChanged);
        _desc.SetChanged(descChanged);
        _crafted.SetChanged(craftedChanged);

        _name.SetResetVisible(_name.Value != DrakeRenameit.GetVanillaDisplayName(item));
        _desc.SetResetVisible(_desc.Value != DrakeRenameit.GetVanillaDisplayDesc(item));
        _crafted.SetResetVisible(_crafted.Value != (item.m_crafterName ?? "") || _labelPicker.SelectedIndex != 0);

        var dirty = nameChanged || descChanged || craftedChanged || labelChanged;
        UkControls.SetButtonEnabled(_save, dirty);
        UkControls.SetButtonEnabled(_revert, dirty);
        _subtitle.text = dirty ? "Unsaved changes" : SubtitleForSaved(item);

        // Preview: empty fields fall back to the item's originals, like the real tooltip.
        var originalName = DrakeRenameit.GetVanillaDisplayName(item);
        _preview.Update(
            string.IsNullOrEmpty(_name.Value) ? originalName : _name.Value,
            string.IsNullOrEmpty(_desc.Value) ? DrakeRenameit.GetVanillaDisplayDesc(item) : _desc.Value,
            _labelPicker.SelectedText,
            string.IsNullOrEmpty(_crafted.Value) ? (item.m_crafterName ?? "") : _crafted.Value);
    }

    static string SubtitleForSaved(ItemDrop.ItemData item)
    {
        var local = Player.m_localPlayer;
        if (local != null && !string.IsNullOrEmpty(item.m_crafterName) && item.m_crafterName == local.GetPlayerName())
            return "Yours";
        return string.IsNullOrEmpty(item.m_crafterName) ? "No crafter yet" : "Crafted by " + item.m_crafterName;
    }

    void RebuildTabs(ItemDrop.ItemData item)
    {
        var tabs = DrakeTabHost.GetUsableTabs(item)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
        _tabs.Clear();
        _tabs.Show(tabs.Count > 1);
        foreach (var tab in tabs)
        {
            var id = tab.Id;
            var active = id == DrakeTabRegistration.RenameItTabId;
            var button = UkControls.MakeButton(tab.Title, () => SwitchTab(id), UkButtonKind.Ghost);
            button.style.height = 40;
            button.style.marginRight = 6;
            if (active)
                UkControls.SetKind(button, UkButtonKind.Selected);

            _tabs.Add(button);
        }
    }

    void SwitchTab(string id)
    {
        if (id == DrakeTabRegistration.RenameItTabId)
            return;
        var item = _item;
        if (item == null)
            return;
        // Our own tab row replaces the host strip; re-enter the host on the chosen tab. Nobody passes onClosed today.
        Close(notifyHost: false);
        DrakeTabHost.OpenForItem(item, id);
    }

    /// <summary>The default tooltip prefix, localized ("Crafted by"), falling back to the first configured label.</summary>
    static string DefaultCraftedByCaption(System.Collections.Generic.List<string> labels)
    {
        var localized = Loc("$item_crafter");
        return !string.IsNullOrEmpty(localized) ? localized : labels.Count > 0 ? labels[0] : "Crafted by";
    }

    void ApplyPreviewVisibility()
    {
        _preview.Root.Show(RenameitConfig.ShowPreviewCard);
        // The stage re-centres when the card appears or goes; keep the drag offset inside the screen.
        ApplyOffset();
    }

    static string Loc(string? text) =>
        string.IsNullOrEmpty(text) ? "" : Localization.instance != null ? Localization.instance.Localize(text) : text!;

    // ---------------------------------------------------------------- drag

    /// <summary>Drag the panel by its header (not by the buttons in it). Stays inside the screen so it can't get lost.</summary>
    void MakeDraggable(VisualElement handle)
    {
        handle.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || e.target is Button)
                return;
            _dragging = true;
            handle.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        handle.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!_dragging)
                return;
            _offset += new Vector2(e.deltaPosition.x, e.deltaPosition.y);
            ApplyOffset();
        });
        handle.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!_dragging)
                return;
            _dragging = false;
            handle.ReleasePointer(e.pointerId);
        });
        handle.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
    }

    void ApplyOffset()
    {
        var rootW = _scrim.resolvedStyle.width;
        var rootH = _scrim.resolvedStyle.height;
        var cardW = _stage.resolvedStyle.width;
        var cardH = _stage.resolvedStyle.height;
        if (rootW > 0f && cardW > 0f)
        {
            // Keep at least 140 px of the header on screen horizontally and the header row on screen vertically.
            var left = (rootW - cardW) * 0.5f;
            var top = (rootH - cardH) * 0.5f;
            _offset.x = Mathf.Clamp(_offset.x, -(left + cardW - 140f), rootW - 140f - left);
            _offset.y = Mathf.Clamp(_offset.y, -top, rootH - 70f - top);
        }

        _stage.style.translate = new Translate(_offset.x, _offset.y);
    }

    /// <summary>One line per open: what sorts where, so a wrong draw/click order is visible in the log.</summary>
    void LogDiagnostics()
    {
        try
        {
            var canvases = DescribeTopOverlayCanvases();
            RenameitConfig.Log?.LogInfo($"[UITK] Workshop opened: panel sortingOrder={_screen.SortingOrder}, overlay canvases (top 4): {canvases}; EventSystem={(UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentInputModule?.GetType().Name ?? "no module" : "none")}");
        }
        catch (Exception ex)
        {
            RenameitConfig.Log?.LogWarning($"[UITK] Diagnostics failed: {ex.Message}");
        }
    }

    static string DescribeTopOverlayCanvases() =>
        string.Join(", ", UnityEngine.Object.FindObjectsOfType<Canvas>()
            .Where(c => c != null && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
            .OrderByDescending(c => c.sortingOrder)
            .Take(4)
            .Select(c => $"{c.name}:{c.sortingOrder}"));

    // ---------------------------------------------------------------- actions

    void Save()
    {
        var item = _item;
        if (item == null || !_save.enabledSelf)
            return;

        var snapshot = DrakeRenameit.CaptureWorkshopSnapshot(item);
        // A value equal to the original means "clear my customization" (same as the old Reset + OK).
        string? name = _name.Value == _savedName ? null : _name.Value == DrakeRenameit.GetVanillaDisplayName(item) ? "" : _name.Value;
        string? desc = _desc.Value == _savedDesc ? null : _desc.Value == DrakeRenameit.GetVanillaDisplayDesc(item) ? "" : _desc.Value;
        string? crafted = _crafted.Value == _savedCrafted ? null : _crafted.Value == (item.m_crafterName ?? "") ? "" : _crafted.Value;

        var setLabel = _labelPicker.SelectedIndex != _savedLabelIndex;
        var labelToken = _labelPicker.SelectedIndex <= 0 || _labelPicker.SelectedIndex >= _labelOptions.Count
            ? null
            : _labelOptions[_labelPicker.SelectedIndex];
        var result = DrakeRenameit.CommitWorkshopEdits(item, name, desc, crafted, labelToken, setLabel);
        if (result == null)
        {
            Close(notifyHost: true);
            return;
        }

        var peeled = !ReferenceEquals(result, item);
        _item = result;
        _undo = peeled ? null : snapshot;
        Refresh();
        _toast.Show(peeled ? "Page written" : "Saved", peeled ? null : Undo);
    }

    void Undo()
    {
        var item = _item;
        if (item == null || _undo == null)
            return;
        var snapshot = _undo.Value;
        _undo = null;
        DrakeRenameit.RestoreWorkshopSnapshot(item, snapshot);
        Refresh();
        _toast.Show("Reverted", null, 3f);
    }

    void OnResetAll()
    {
        var item = _item;
        if (item == null)
            return;
        var snapshot = DrakeRenameit.CaptureWorkshopSnapshot(item);
        DrakeRenameit.CurrentItem = item;
        DrakeRenameit.ResetAllCustomizations(item);
        _undo = snapshot;
        Refresh();
        _toast.Show("Reset to original", Undo);
    }

    void OnUnlock()
    {
        var item = _item;
        if (item == null)
            return;
        if (DrakeRenameit.TryPayRenameUnlock(item))
        {
            Refresh();
            _toast.Show("Unlocked", null, 3f);
        }
        else
        {
            Refresh();
        }
    }

    void OnSharedChanged(bool on)
    {
        var item = _item;
        var player = Player.m_localPlayer;
        if (item == null || player == null || !RenamePermissionManager.CanChangePublicFlag(item, player))
        {
            if (item != null)
                _shared.SetValue(RenamePermissionManager.HasPublicRewriteFlag(item));
            return;
        }

        DrakeRenameit.SetWorkshopSharedFlag(item, on);
    }
}
