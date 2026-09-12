using UnityEngine;
using MiniGames;

/// <summary>
/// Dedicated Winch cartridge.
/// Presentation and user intent only; WinchModule remains runtime authority.
/// </summary>
public sealed class WinchCartridge :
    IMiniGameCartridge,
    IOverlayRenderable
{
    private readonly Hardpoint _hardpoint;
    private readonly WinchModule _winch;
    private readonly WinchReadoutSource _readout;

    private MiniGameContext _ctx;
    private bool _requestedClose;
    private string _statusNote;

    private PlayerInventory _playerInventory;
    private PlayerEquipment _playerEquipment;

    public WinchCartridge(
        Hardpoint hardpoint,
        WinchModule winch,
        WinchReadoutSource readout)
    {
        _hardpoint = hardpoint;
        _winch = winch;
        _readout = readout;
    }

    public void Begin(
        MiniGameContext context)
    {
        _ctx =
            context ??
            new MiniGameContext();

        _requestedClose =
            false;

        _statusNote =
            null;

        _playerInventory =
            Object.FindFirstObjectByType<PlayerInventory>();

        _playerEquipment =
            _playerInventory != null
                ? _playerInventory.Equipment
                : null;

        if (_playerEquipment == null &&
            _playerInventory != null)
        {
            _playerEquipment =
                _playerInventory.GetComponentInParent<PlayerEquipment>(
                    true);
        }

        if (_playerEquipment == null)
        {
            _playerEquipment =
                Object.FindFirstObjectByType<PlayerEquipment>(
                    FindObjectsInactive.Include);
        }
    }

    public MiniGameResult Tick(
        float dt,
        MiniGameInput input)
    {
        if (_requestedClose)
            return Cancelled("Closed Winch");

        if (_hardpoint == null ||
            !_hardpoint.HasInstalledModule ||
            _hardpoint.InstalledModule == null ||
            _winch == null)
        {
            return Cancelled(
                "Winch module missing");
        }

        return Running();
    }

    public MiniGameResult Cancel()
    {
        return Cancelled(
            "Closed Winch");
    }

    public MiniGameResult Interrupt(
        string reason)
    {
        return Cancelled(
            string.IsNullOrWhiteSpace(
                reason)
                ? "Winch interrupted"
                : $"Winch interrupted: {reason}");
    }

    public void End()
    {
        _ctx =
            null;
    }

    public void DrawOverlayGUI(
        Rect panel)
    {
        const float pad =
            18f;

        const float line =
            23f;

        float x =
            panel.x +
            pad;

        float y =
            panel.y +
            12f;

        float width =
            panel.width -
            pad * 2f;

        GUI.Label(
            new Rect(
                x,
                y,
                width - 44f,
                26f),
            "WINCH CONTROL");

        if (GUI.Button(
                new Rect(
                    panel.xMax - 42f,
                    panel.y + 10f,
                    28f,
                    24f),
                "X"))
        {
            _requestedClose =
                true;

            return;
        }

        y +=
            36f;

        WinchReadoutSnapshot snapshot =
            _readout != null
                ? _readout.Capture()
                : default;

        DrawStatusLine(
            x,
            ref y,
            width,
            line,
            snapshot);

        y +=
            5f;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                126f),
            "RIGGING");

        float sx =
            x + 12f;

        float sy =
            y + 25f;

        float sw =
            width - 24f;

        DrawLineAt(
            sx,
            sy,
            sw,
            line,
            "PAYLOAD:",
            ResolvePayloadLabel(
                snapshot));

        sy +=
            line;

        DrawLineAt(
            sx,
            sy,
            sw,
            line,
            "LINE:",
            ResolveLineLabel(
                snapshot));

        sy +=
            line;

        DrawLineAt(
            sx,
            sy,
            sw,
            line,
            "PAYOUT:",
            $"{snapshot.DeployedLengthMeters:0.00} / {snapshot.AvailableLineMeters:0.00} m");

        sy +=
            line;

        DrawLineAt(
            sx,
            sy,
            sw,
            line,
            "DISTANCE:",
            snapshot.HasDeployedPayload
                ? $"{snapshot.CurrentDistanceMeters:0.00} m | SLACK {snapshot.SlackMeters:0.00} m"
                : "--");

        y +=
            136f;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                78f),
            "LINE RATINGS");

        sx =
            x + 12f;

        sy =
            y + 25f;

        DrawLineAt(
            sx,
            sy,
            sw,
            line,
            "WORKING LOAD:",
            snapshot.WorkingLoadNewtons > 0f
                ? $"{snapshot.WorkingLoadNewtons:0} N"
                : "--");

        sy +=
            line;

        DrawLineAt(
            sx,
            sy,
            sw,
            line,
            "BREAKING LOAD:",
            snapshot.BreakingLoadNewtons > 0f
                ? $"{snapshot.BreakingLoadNewtons:0} N"
                : "--");

        y +=
            88f;

        int slotCount =
            Mathf.Max(
                1,
                snapshot.LineSlotCount);

        float lineSlotContentWidth =
            width -
            24f;

        int lineSlotColumns =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    lineSlotContentWidth /
                    170f),
                1,
                Mathf.Min(
                    6,
                    slotCount));

        int slotRows =
            Mathf.CeilToInt(
                slotCount /
                (float)lineSlotColumns);

        const float lineSlotCardHeight =
            80f;

        const float lineSlotRowGap =
            8f;

        float lineSlotSectionHeight =
            56f +
            slotRows *
            lineSlotCardHeight +
            Mathf.Max(
                0,
                slotRows -
                1) *
            lineSlotRowGap;

        DrawSectionBox(
            new Rect(
                x,
                y,
                width,
                lineSlotSectionHeight),
            "LINE SLOTS");

        float lineSlotY =
            y +
            25f;

        GUI.Label(
            new Rect(
                x +
                12f,
                lineSlotY,
                width -
                24f,
                22f),
            ResolveAvailableLineSummary());

        lineSlotY +=
            25f;

        DrawLineSlots(
            x +
            12f,
            lineSlotY,
            lineSlotContentWidth,
            slotCount,
            lineSlotColumns,
            snapshot.HasDeployedPayload);

        y +=
            lineSlotSectionHeight +
            10f;

        GUI.Label(
            new Rect(
                x,
                y,
                width,
                22f),
            "CONTROL");

        y +=
            25f;

        float gap =
            8f;

        float buttonWidth =
            (width - gap * 3f) /
            4f;

        DrawCommandButton(
            new Rect(
                x,
                y,
                buttonWidth,
                34f),
            "LOWER",
            snapshot.HasDeployment &&
            snapshot.AvailableLineMeters > 0f,
            TryLower);

        DrawCommandButton(
            new Rect(
                x + (buttonWidth + gap),
                y,
                buttonWidth,
                34f),
            "STOP",
            snapshot.HasWinch,
            Stop);

        DrawCommandButton(
            new Rect(
                x + (buttonWidth + gap) * 2f,
                y,
                buttonWidth,
                34f),
            "RAISE",
            snapshot.HasDeployment &&
            snapshot.HasDeployedPayload,
            TryRaise);

        DrawCommandButton(
            new Rect(
                x + (buttonWidth + gap) * 3f,
                y,
                buttonWidth,
                34f),
            "QUICK RELEASE",
            snapshot.HasDeployment &&
            snapshot.AvailableLineMeters > 0f,
            QuickRelease);

        y +=
            43f;

        DrawCommandButton(
            new Rect(
                x,
                y,
                width,
                32f),
            "CUT LINE",
            snapshot.HasDeployment &&
            snapshot.HasDeployedPayload,
            CutLine);

        y +=
            41f;

        if (!string.IsNullOrWhiteSpace(
                _statusNote))
        {
            GUI.Label(
                new Rect(
                    x,
                    y,
                    width,
                    22f),
                _statusNote);

            y +=
                24f;
        }

        GUI.Label(
            new Rect(
                x,
                y,
                width,
                40f),
            "Live tether tension is intentionally not shown yet; Unity's current joint readback is known to report zero in this setup.");
    }

    private void TryLower()
    {
        if (_winch == null)
            return;

        bool ok =
            _winch.TryLower();

        _statusNote =
            ok
                ? "LOWER COMMAND ACCEPTED"
                : "LOWER COMMAND REJECTED";
    }

    private void TryRaise()
    {
        if (_winch == null)
            return;

        bool ok =
            _winch.TryRaise();

        _statusNote =
            ok
                ? "RAISE COMMAND ACCEPTED"
                : "RAISE COMMAND REJECTED";
    }

    private void Stop()
    {
        if (_winch == null)
            return;

        _winch.Stop();

        _statusNote =
            "WINCH STOPPED";
    }

    private void QuickRelease()
    {
        if (_winch == null)
            return;

        bool ok =
            _winch.QuickRelease();

        _statusNote =
            ok
                ? "BRAKE RELEASED"
                : "QUICK RELEASE REJECTED";
    }

    private void CutLine()
    {
        if (_winch == null)
            return;

        bool ok =
            _winch.CutLine();

        _statusNote =
            ok
                ? "LINE CUT | PAYLOAD RELEASED"
                : "CUT LINE REJECTED";
    }

    private enum PlayerLineSourceKind
    {
        None = 0,
        Hotbar = 1,
        Hands = 2
    }

    private struct PlayerLineSource
    {
        public PlayerLineSourceKind Kind;
        public int HotbarIndex;
        public ItemInstance Item;
    }

    private void DrawLineSlots(
        float x,
        float y,
        float width,
        int slotCount,
        int columns,
        bool payloadDeployed)
    {
        const float columnGap =
            8f;

        const float rowGap =
            8f;

        const float cardHeight =
            80f;

        float cardWidth =
            (width -
             columnGap *
             Mathf.Max(
                 0,
                 columns -
                 1)) /
            columns;

        for (int i = 0;
             i < slotCount;
             i++)
        {
            int column =
                i %
                columns;

            int row =
                i /
                columns;

            Rect card =
                new Rect(
                    x +
                    column *
                    (cardWidth +
                     columnGap),
                    y +
                    row *
                    (cardHeight +
                     rowGap),
                    cardWidth,
                    cardHeight);

            DrawLineSlotCard(
                card,
                i,
                payloadDeployed);
        }
    }

    private void DrawLineSlotCard(
        Rect card,
        int slotIndex,
        bool payloadDeployed)
    {
        if (_winch == null)
            return;

        WinchLineSlotBinding binding =
            new WinchLineSlotBinding(
                _winch,
                slotIndex);

        ItemInstance loaded =
            binding.GetItem();

        GUI.Box(
            card,
            GUIContent.none);

        const float pad =
            7f;

        const float iconSize =
            54f;

        Rect iconRect =
            new Rect(
                card.x +
                pad,
                card.y +
                13f,
                iconSize,
                iconSize);

        DrawLineSlotIcon(
            iconRect,
            loaded);

        float infoX =
            iconRect.xMax +
            8f;

        float infoWidth =
            Mathf.Max(
                0f,
                card.xMax -
                pad -
                infoX);

        GUI.Label(
            new Rect(
                infoX,
                card.y +
                6f,
                infoWidth,
                18f),
            $"SLOT {slotIndex + 1}");

        string itemText =
            loaded != null &&
            loaded.Definition != null
                ? loaded.Quantity == 1
                    ? loaded.Definition.DisplayName
                    : $"{loaded.Definition.DisplayName} x{loaded.Quantity}"
                : "EMPTY";

        GUI.Label(
            new Rect(
                infoX,
                card.y +
                24f,
                infoWidth,
                20f),
            itemText);

        Rect buttonRect =
            new Rect(
                infoX,
                card.yMax -
                32f,
                infoWidth,
                26f);

        bool previous =
            GUI.enabled;

        if (loaded == null)
        {
            bool hasCompatibleInventoryLine =
                TryFindFirstCompatiblePlayerLine(
                    out _);

            GUI.enabled =
                previous &&
                !payloadDeployed &&
                hasCompatibleInventoryLine;

            if (GUI.Button(
                    buttonRect,
                    "LOAD 1"))
            {
                TryLoadOneLineIntoSlot(
                    slotIndex);
            }
        }
        else
        {
            GUI.enabled =
                previous &&
                !payloadDeployed &&
                CanReturnToPlayerInventory(
                    loaded);

            if (GUI.Button(
                    buttonRect,
                    "UNLOAD"))
            {
                TryUnloadLineSlot(
                    slotIndex);
            }
        }

        GUI.enabled =
            previous;
    }

    private static void DrawLineSlotIcon(
        Rect rect,
        ItemInstance item)
    {
        GUI.Box(
            rect,
            GUIContent.none);

        if (item == null ||
            item.Definition == null)
        {
            GUI.Label(
                new Rect(
                    rect.x +
                    5f,
                    rect.y +
                    rect.height *
                    0.5f -
                    9f,
                    rect.width -
                    10f,
                    18f),
                "EMPTY");

            return;
        }

        Sprite sprite =
            item.Definition.Icon;

        if (sprite == null ||
            sprite.texture == null)
        {
            GUI.Label(
                new Rect(
                    rect.x +
                    4f,
                    rect.y +
                    4f,
                    rect.width -
                    8f,
                    rect.height -
                    8f),
                item.Definition.DisplayName);

            return;
        }

        Rect textureRect =
            sprite.textureRect;

        Texture2D texture =
            sprite.texture;

        Rect uv =
            new Rect(
                textureRect.x /
                texture.width,
                textureRect.y /
                texture.height,
                textureRect.width /
                texture.width,
                textureRect.height /
                texture.height);

        Rect drawRect =
            new Rect(
                rect.x +
                4f,
                rect.y +
                4f,
                rect.width -
                8f,
                rect.height -
                8f);

        GUI.DrawTextureWithTexCoords(
            drawRect,
            texture,
            uv,
            true);
    }

    private string ResolveAvailableLineSummary()
    {
        int compatibleUnits =
            CountCompatibleInventoryLineUnits(
                out ItemInstance first);

        if (compatibleUnits <= 0 ||
            first == null ||
            first.Definition == null)
        {
            return
                "INVENTORY: NO COMPATIBLE TETHER LINE";
        }

        return
            $"INVENTORY: {compatibleUnits} TETHER ITEM{(compatibleUnits == 1 ? "" : "S")} AVAILABLE | NEXT: {first.Definition.DisplayName}";
    }

    private int CountCompatibleInventoryLineUnits(
        out ItemInstance first)
    {
        first =
            null;

        if (_winch == null)
            return 0;

        int total =
            0;

        if (_playerInventory != null)
        {
            for (int i = 0;
                 i < _playerInventory.HotbarSlotCount;
                 i++)
            {
                InventorySlot slot =
                    _playerInventory.GetSlot(
                        i);

                ItemInstance candidate =
                    slot != null
                        ? slot.Instance
                        : null;

                if (!IsCompatibleInventoryLine(
                        candidate))
                {
                    continue;
                }

                if (first == null)
                    first = candidate;

                total +=
                    Mathf.Max(
                        0,
                        candidate.Quantity);
            }
        }

        ItemInstance hands =
            _playerEquipment != null
                ? _playerEquipment.Get(
                    BottomBarSlotType.Hands)
                : null;

        if (IsCompatibleInventoryLine(
                hands))
        {
            if (first == null)
                first = hands;

            total +=
                Mathf.Max(
                    0,
                    hands.Quantity);
        }

        return total;
    }

    private bool TryFindFirstCompatiblePlayerLine(
        out PlayerLineSource source)
    {
        source =
            default;

        if (_winch == null)
            return false;

        if (_playerInventory != null)
        {
            for (int i = 0;
                 i < _playerInventory.HotbarSlotCount;
                 i++)
            {
                InventorySlot slot =
                    _playerInventory.GetSlot(
                        i);

                ItemInstance candidate =
                    slot != null
                        ? slot.Instance
                        : null;

                if (!IsCompatibleInventoryLine(
                        candidate))
                {
                    continue;
                }

                source =
                    new PlayerLineSource
                    {
                        Kind =
                            PlayerLineSourceKind.Hotbar,
                        HotbarIndex =
                            i,
                        Item =
                            candidate
                    };

                return true;
            }
        }

        ItemInstance hands =
            _playerEquipment != null
                ? _playerEquipment.Get(
                    BottomBarSlotType.Hands)
                : null;

        if (IsCompatibleInventoryLine(
                hands))
        {
            source =
                new PlayerLineSource
                {
                    Kind =
                        PlayerLineSourceKind.Hands,
                    HotbarIndex =
                        -1,
                    Item =
                        hands
                };

            return true;
        }

        return false;
    }

    private bool IsCompatibleInventoryLine(
        ItemInstance item)
    {
        return
            _winch != null &&
            item != null &&
            item.Definition != null &&
            item.Quantity > 0 &&
            _winch.CanAcceptLine(
                item);
    }

    private void TryLoadOneLineIntoSlot(
        int slotIndex)
    {
        if (_winch == null)
            return;

        if (_winch.HasDeployedPayload)
        {
            _statusNote =
                "STOW PAYLOAD BEFORE CHANGING LINE";

            return;
        }

        WinchLineSlotBinding binding =
            new WinchLineSlotBinding(
                _winch,
                slotIndex);

        if (binding.GetItem() != null)
        {
            _statusNote =
                "LINE SLOT IS ALREADY OCCUPIED";

            return;
        }

        if (!TryFindFirstCompatiblePlayerLine(
                out PlayerLineSource source))
        {
            _statusNote =
                "NO COMPATIBLE TETHER LINE IN INVENTORY";

            return;
        }

        if (!TryTakeOneLineFromPlayer(
                source,
                out ItemInstance oneLine) ||
            oneLine == null)
        {
            _statusNote =
                "COULD NOT TAKE ONE TETHER ITEM";

            return;
        }

        if (oneLine.Quantity != 1)
        {
            ReturnLineToPlayer(
                oneLine,
                source);

            _statusNote =
                "TETHER STACK COULD NOT BE SPLIT";

            return;
        }

        if (!binding.TryPlaceItem(
                oneLine,
                out ItemInstance displaced))
        {
            ReturnLineToPlayer(
                oneLine,
                source);

            _statusNote =
                "WINCH REJECTED TETHER ITEM";

            return;
        }

        if (displaced != null)
        {
            ReturnLineToPlayer(
                displaced,
                source);
        }

        _statusNote =
            $"LOADED 1 {oneLine.Definition.DisplayName}";
    }

    private bool TryTakeOneLineFromPlayer(
        PlayerLineSource source,
        out ItemInstance oneLine)
    {
        oneLine =
            null;

        ItemInstance item =
            source.Item;

        if (!IsCompatibleInventoryLine(
                item))
        {
            return false;
        }

        switch (source.Kind)
        {
            case PlayerLineSourceKind.Hotbar:
                {
                    if (_playerInventory == null)
                        return false;

                    InventorySlot slot =
                        _playerInventory.GetSlot(
                            source.HotbarIndex);

                    if (slot == null ||
                        slot.IsEmpty ||
                        slot.Instance == null ||
                        !ReferenceEquals(
                            slot.Instance,
                            item))
                    {
                        return false;
                    }

                    if (item.Quantity > 1)
                    {
                        if (!item.CanSplit)
                            return false;

                        oneLine =
                            item.SplitOff(
                                1);

                        if (oneLine == null)
                            return false;
                    }
                    else
                    {
                        oneLine =
                            item;

                        slot.Clear();
                    }

                    _playerInventory.NotifyChanged();

                    return
                        oneLine != null &&
                        oneLine.Quantity == 1;
                }

            case PlayerLineSourceKind.Hands:
                {
                    if (_playerEquipment == null)
                        return false;

                    ItemInstance current =
                        _playerEquipment.Get(
                            BottomBarSlotType.Hands);

                    if (current == null ||
                        !ReferenceEquals(
                            current,
                            item))
                    {
                        return false;
                    }

                    ItemInstance removed =
                        _playerEquipment.Remove(
                            BottomBarSlotType.Hands);

                    if (removed == null)
                        return false;

                    if (removed.Quantity > 1)
                    {
                        if (!removed.CanSplit)
                        {
                            _playerEquipment.TryPlace(
                                BottomBarSlotType.Hands,
                                removed,
                                out _);

                            return false;
                        }

                        oneLine =
                            removed.SplitOff(
                                1);

                        if (oneLine == null)
                        {
                            _playerEquipment.TryPlace(
                                BottomBarSlotType.Hands,
                                removed,
                                out _);

                            return false;
                        }

                        if (!_playerEquipment.TryPlace(
                                BottomBarSlotType.Hands,
                                removed,
                                out ItemInstance displaced) ||
                            displaced != null)
                        {
                            if (oneLine != null &&
                                removed.CanStackWith(
                                    oneLine))
                            {
                                int moved =
                                    removed.AddQuantity(
                                        oneLine.Quantity);

                                oneLine.RemoveQuantity(
                                    moved);
                            }

                            _playerInventory?.TryAutoInsert(
                                removed,
                                out _);

                            _playerInventory?.NotifyChanged();

                            return false;
                        }
                    }
                    else
                    {
                        oneLine =
                            removed;
                    }

                    _playerInventory?.NotifyChanged();

                    return
                        oneLine != null &&
                        oneLine.Quantity == 1;
                }
        }

        return false;
    }

    private void ReturnLineToPlayer(
        ItemInstance item,
        PlayerLineSource preferredSource)
    {
        if (item == null ||
            item.IsDepleted())
        {
            return;
        }

        if (preferredSource.Kind ==
                PlayerLineSourceKind.Hotbar &&
            _playerInventory != null)
        {
            InventorySlot slot =
                _playerInventory.GetSlot(
                    preferredSource.HotbarIndex);

            if (slot != null)
            {
                if (slot.IsEmpty)
                {
                    slot.Set(
                        item);

                    _playerInventory.NotifyChanged();

                    return;
                }

                if (slot.Instance != null &&
                    slot.Instance.CanStackWith(
                        item))
                {
                    int moved =
                        slot.Instance.AddQuantity(
                            item.Quantity);

                    item.RemoveQuantity(
                        moved);

                    if (item.IsDepleted())
                    {
                        _playerInventory.NotifyChanged();

                        return;
                    }
                }
            }
        }

        if (preferredSource.Kind ==
                PlayerLineSourceKind.Hands &&
            _playerEquipment != null &&
            _playerEquipment.Get(
                BottomBarSlotType.Hands) ==
            null)
        {
            if (_playerEquipment.TryPlace(
                    BottomBarSlotType.Hands,
                    item,
                    out ItemInstance displaced) &&
                displaced == null)
            {
                _playerInventory?.NotifyChanged();

                return;
            }
        }

        if (_playerInventory != null)
        {
            _playerInventory.TryAutoInsert(
                item,
                out _);

            _playerInventory.NotifyChanged();
        }
    }

    private bool CanReturnToPlayerInventory(
        ItemInstance item)
    {
        return
            _playerInventory != null &&
            item != null &&
            _playerInventory.CanFullyAdd(
                item);
    }

    private void TryUnloadLineSlot(
        int slotIndex)
    {
        if (_winch == null ||
            _playerInventory == null)
        {
            return;
        }

        if (_winch.HasDeployedPayload)
        {
            _statusNote =
                "STOW PAYLOAD BEFORE CHANGING LINE";

            return;
        }

        WinchLineSlotBinding binding =
            new WinchLineSlotBinding(
                _winch,
                slotIndex);

        ItemInstance loaded =
            binding.GetItem();

        if (loaded == null)
        {
            _statusNote =
                "LINE SLOT IS EMPTY";

            return;
        }

        if (!_playerInventory.CanFullyAdd(
                loaded))
        {
            _statusNote =
                "NO INVENTORY ROOM TO UNLOAD LINE";

            return;
        }

        ItemInstance removed =
            binding.RemoveItem();

        if (removed == null)
        {
            _statusNote =
                "COULD NOT REMOVE LINE";

            return;
        }

        if (!_playerInventory.TryAutoInsert(
                removed,
                out ItemInstance remainder) ||
            (remainder != null &&
             !remainder.IsDepleted()))
        {
            // Roll back only if this is already a valid one-item line slot.
            // Legacy stacks produced by Pass 03 cannot be reinserted through
            // the now-correct single-item binding; return them to player inventory
            // instead of inventing a new illegal slot state.
            if (removed.Quantity == 1)
            {
                binding.TryPlaceItem(
                    removed,
                    out _);
            }

            _statusNote =
                "FAILED TO RETURN LINE TO INVENTORY";

            return;
        }

        _playerInventory.NotifyChanged();

        _statusNote =
            $"UNLOADED {removed.Definition.DisplayName}";
    }

    private void DrawCommandButton(
        Rect rect,
        string label,
        bool enabled,
        System.Action action)
    {
        bool previous =
            GUI.enabled;

        GUI.enabled =
            previous &&
            enabled;

        if (GUI.Button(
                rect,
                label))
        {
            action?.Invoke();
        }

        GUI.enabled =
            previous;
    }

    private static void DrawStatusLine(
        float x,
        ref float y,
        float width,
        float line,
        WinchReadoutSnapshot snapshot)
    {
        string state;

        if (!snapshot.HasWinch)
        {
            state =
                "OFFLINE";
        }
        else if (!snapshot.HasLink)
        {
            state =
                "UNLINKED";
        }
        else if (!snapshot.HasDeployment)
        {
            state =
                "NO DEPLOYMENT MODULE";
        }
        else
        {
            state =
                $"{snapshot.DeploymentState} | {snapshot.Command}";
        }

        DrawLineAt(
            x,
            y,
            width,
            line,
            "STATUS:",
            state.ToUpperInvariant());

        y +=
            line;
    }

    private static string ResolvePayloadLabel(
        WinchReadoutSnapshot snapshot)
    {
        string name =
            string.IsNullOrWhiteSpace(
                snapshot.PayloadName)
                ? "NONE"
                : snapshot.PayloadName;

        if (snapshot.HasDeployedPayload)
            return $"{name} | DEPLOYED";

        if (snapshot.HasStoredPayload)
            return $"{name} | STOWED";

        return "NONE";
    }

    private static string ResolveLineLabel(
        WinchReadoutSnapshot snapshot)
    {
        if (snapshot.LoadedLineSlotCount <= 0)
        {
            return
                $"EMPTY | 0/{snapshot.LineSlotCount} SLOTS";
        }

        string summary =
            string.IsNullOrWhiteSpace(
                snapshot.LoadedLineSummary)
                ? "LOADED"
                : snapshot.LoadedLineSummary;

        return
            $"{snapshot.LoadedLineSlotCount}/{snapshot.LineSlotCount} | {summary}";
    }

    private static void DrawLineAt(
        float x,
        float y,
        float width,
        float line,
        string label,
        string value)
    {
        const float labelWidth =
            122f;

        GUI.Label(
            new Rect(
                x,
                y,
                labelWidth,
                line),
            label);

        GUI.Label(
            new Rect(
                x + labelWidth,
                y,
                Mathf.Max(
                    0f,
                    width - labelWidth),
                line),
            value);
    }

    private static void DrawSectionBox(
        Rect rect,
        string title)
    {
        GUI.Box(
            rect,
            string.Empty);

        GUI.Label(
            new Rect(
                rect.x + 10f,
                rect.y + 3f,
                rect.width - 20f,
                20f),
            title);
    }

    private static MiniGameResult Running()
    {
        return new MiniGameResult
        {
            outcome =
                MiniGameOutcome.None,
            quality01 =
                1f,
            note =
                null,
            hasMeaningfulProgress =
                false
        };
    }

    private static MiniGameResult Cancelled(
        string note)
    {
        return new MiniGameResult
        {
            outcome =
                MiniGameOutcome.Cancelled,
            quality01 =
                1f,
            note =
                note,
            hasMeaningfulProgress =
                false
        };
    }
}
