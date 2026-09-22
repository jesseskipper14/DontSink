using UnityEngine;
using MiniGames;

/// <summary>
/// Dedicated Winch cartridge.
/// Presentation and intent emission only. WinchModule and the authoritative line
/// transfer router remain runtime authorities.
/// </summary>
public sealed class WinchCartridge :
    IMiniGameCartridge,
    IOverlayRenderable
{
    private readonly Hardpoint _hardpoint;
    private readonly WinchModule _winch;
    private readonly WinchReadoutSource _readout;
    private readonly GameObject _requester;

    private MiniGameContext _ctx;
    private bool _requestedClose;
    private string _statusNote;

    private PlayerInventory _playerInventory;
    private PlayerEquipment _playerEquipment;

    public WinchCartridge(
        Hardpoint hardpoint,
        WinchModule winch,
        WinchReadoutSource readout,
        GameObject requester)
    {
        _hardpoint = hardpoint;
        _winch = winch;
        _readout = readout;
        _requester = requester;
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

        ResolveRequesterInventoryContext();
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
        EmitControlIntent(
            WinchControlIntent.Lower);
    }

    private void TryRaise()
    {
        EmitControlIntent(
            WinchControlIntent.Raise);
    }

    private void Stop()
    {
        EmitControlIntent(
            WinchControlIntent.Stop);
    }

    private void QuickRelease()
    {
        EmitControlIntent(
            WinchControlIntent.QuickRelease);
    }

    private void CutLine()
    {
        EmitControlIntent(
            WinchControlIntent.CutLine);
    }

    private void EmitControlIntent(
        WinchControlIntent intent)
    {
        if (_ctx == null ||
            _ctx.emitEffect == null)
        {
            _statusNote =
                "WINCH CONTROL ROUTER UNAVAILABLE";

            return;
        }

        WinchControlIntentPayload payload =
            new WinchControlIntentPayload
            {
                version =
                    WinchControlIntentPayload.CurrentVersion,

                intent =
                    intent
            };

        _statusNote =
            $"{intent.ToString().ToUpperInvariant()} REQUESTED";

        _ctx.emitEffect.Invoke(
            new MiniGameEffect
            {
                kind =
                    MiniGameEffectKind.Control,

                system =
                    WinchControlIntentPayload.EffectSystem,

                targetId =
                    _ctx.targetId,

                value01 =
                    1f,

                quality01 =
                    1f,

                durationSeconds =
                    0f,

                v2 =
                    Vector2.zero,

                payloadJson =
                    JsonUtility.ToJson(
                        payload)
            });
    }

    /// <summary>
    /// Result callback from the authoritative control router.
    /// The cartridge displays the result but never applies winch state itself.
    /// </summary>
    public void NotifyControlIntentApplied(
        WinchControlIntent intent,
        bool success,
        string message)
    {
        if (!string.IsNullOrWhiteSpace(
                message))
        {
            _statusNote =
                message;

            return;
        }

        _statusNote =
            success
                ? $"{intent.ToString().ToUpperInvariant()} ACCEPTED"
                : $"{intent.ToString().ToUpperInvariant()} REJECTED";
    }

    /// <summary>
    /// Result callback from the authoritative line-transfer router.
    /// </summary>
    public void NotifyLineTransferApplied(
        WinchLineTransferOperation operation,
        bool success,
        string message)
    {
        if (!string.IsNullOrWhiteSpace(
                message))
        {
            _statusNote =
                message;

            return;
        }

        _statusNote =
            success
                ? $"{operation.ToString().ToUpperInvariant()} ACCEPTED"
                : $"{operation.ToString().ToUpperInvariant()} REJECTED";
    }

    private void EmitLineTransferIntent(
        WinchLineTransferIntentPayload payload)
    {
        if (payload == null)
            return;

        if (_ctx == null ||
            _ctx.emitEffect == null)
        {
            _statusNote =
                "WINCH LINE TRANSFER ROUTER UNAVAILABLE";

            return;
        }

        payload.version =
            WinchLineTransferIntentPayload.CurrentVersion;

        _statusNote =
            payload.operation ==
                WinchLineTransferOperation.LoadOneFromPlayer
                ? "LOAD LINE REQUESTED"
                : "UNLOAD LINE REQUESTED";

        _ctx.emitEffect.Invoke(
            new MiniGameEffect
            {
                kind =
                    MiniGameEffectKind.Transaction,

                system =
                    WinchLineTransferIntentPayload.EffectSystem,

                targetId =
                    _ctx.targetId,

                value01 =
                    1f,

                quality01 =
                    1f,

                durationSeconds =
                    0f,

                v2 =
                    Vector2.zero,

                payloadJson =
                    JsonUtility.ToJson(
                        payload)
            });
    }

    private struct PlayerLineSource
    {
        public WinchLinePlayerSourceKind Kind;
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

    private void ResolveRequesterInventoryContext()
    {
        _playerInventory =
            null;

        _playerEquipment =
            null;

        if (_requester == null)
            return;

        _playerInventory =
            _requester.GetComponent<PlayerInventory>() ??
            _requester.GetComponentInChildren<PlayerInventory>(
                true) ??
            _requester.GetComponentInParent<PlayerInventory>(
                true);

        if (_playerInventory != null)
        {
            _playerEquipment =
                _playerInventory.Equipment;
        }

        if (_playerEquipment == null)
        {
            _playerEquipment =
                _requester.GetComponent<PlayerEquipment>() ??
                _requester.GetComponentInChildren<PlayerEquipment>(
                    true) ??
                _requester.GetComponentInParent<PlayerEquipment>(
                    true);
        }
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
                            WinchLinePlayerSourceKind.Hotbar,
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
                        WinchLinePlayerSourceKind.Hands,
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
                out PlayerLineSource source) ||
            source.Item == null)
        {
            _statusNote =
                "NO COMPATIBLE TETHER LINE IN INVENTORY";

            return;
        }

        EmitLineTransferIntent(
            new WinchLineTransferIntentPayload
            {
                operation =
                    WinchLineTransferOperation.LoadOneFromPlayer,

                winchSlotIndex =
                    slotIndex,

                playerSourceKind =
                    source.Kind,

                playerHotbarIndex =
                    source.HotbarIndex,

                expectedInstanceId =
                    source.Item.InstanceId
            });
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

        ItemInstance loaded =
            binding.GetItem();

        if (loaded == null)
        {
            _statusNote =
                "LINE SLOT IS EMPTY";

            return;
        }

        EmitLineTransferIntent(
            new WinchLineTransferIntentPayload
            {
                operation =
                    WinchLineTransferOperation.UnloadToPlayer,

                winchSlotIndex =
                    slotIndex,

                playerSourceKind =
                    WinchLinePlayerSourceKind.None,

                playerHotbarIndex =
                    -1,

                expectedInstanceId =
                    loaded.InstanceId
            });
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
