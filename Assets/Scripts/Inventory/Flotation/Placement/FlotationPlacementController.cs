using UnityEngine;

/// <summary>
/// Authoritative placement transaction for inventory lift bags.
///
/// The selected ItemInstance stays in inventory while aiming. On commit, one
/// item is extracted transactionally, manifested through its normal WorldPrefab,
/// and attached to either a moving Rigidbody2D-local point or a fixed world
/// point. Any failure rolls the inventory mutation back.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlotationPlacementController : MonoBehaviour, IEscapeClosable
{
    [Header("Authority")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Placement")]
    [SerializeField] private Transform placementOrigin;
    [SerializeField, Min(0.1f)] private float maxPlacementRange = 2.25f;
    [SerializeField, Min(0f)] private float targetAssistRadius = 0.08f;
    [SerializeField] private LayerMask targetMask = Physics2D.DefaultRaycastLayers;
    [SerializeField] private bool allowTriggerTargets;

    [Tooltip("Where the packed bag appears relative to the clicked attachment point.")]
    [SerializeField]
    private Vector2 bagSpawnOffsetFromTarget = new Vector2(0f, 0.35f);

    [Header("Optional Local Preview")]
    [SerializeField] private Transform previewIndicator;
    [SerializeField] private SpriteRenderer previewIndicatorRenderer;
    [SerializeField] private LineRenderer previewLine;
    [SerializeField] private Color validPreviewColor = new Color(0.35f, 1f, 0.45f, 0.9f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0.35f, 0.35f, 0.9f);

    [Header("Escape Routing")]
    [Tooltip("Placement mode closes before ordinary menus when Escape is pressed.")]
    [SerializeField] private int escapePriority = 500;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;
    [SerializeField] private bool placementActive;
    [SerializeField] private string pendingItemInstanceId;
    [SerializeField] private string lastResult;

    private ItemInstance _pendingItem;
    private bool _interactionGateHeld;
    private bool _escapeRegistered;

    public bool IsPlacementActive => placementActive;
    public bool HasGameplayAuthority =>
        GameplayAuthority.CanRun(gameplayAuthorityMode);
    public string LastResult => lastResult;
    public int EscapePriority => escapePriority;
    public bool IsEscapeOpen => placementActive;

    public bool HasSelectedDeployableFlotationItem
    {
        get
        {
            ResolveRefs();

            return
                TryGetSelectedItem(out ItemInstance selected, out _) &&
                IsDeployableFlotationItem(selected, out _);
        }
    }

    private Vector2 PlacementOriginWorld =>
        placementOrigin != null
            ? (Vector2)placementOrigin.position
            : (Vector2)transform.position;

    private void Reset()
    {
        ResolveRefs();
        ConfigurePreview();
        HidePreview();
    }

    private void Awake()
    {
        ResolveRefs();
        ConfigurePreview();
        HidePreview();
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (inventory != null)
            inventory.SelectionChanged += HandleInventorySelectionChanged;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.SelectionChanged -= HandleInventorySelectionChanged;

        CancelLocalPlacement();
        UnregisterEscape();
    }

    private void OnDestroy()
    {
        ReleaseInteractionGate();
        UnregisterEscape();
    }

    public bool TryApplyIntent(
        FlotationPlacementIntentRequest request,
        out string message)
    {
        switch (request.Kind)
        {
            case FlotationPlacementIntentKind.TogglePlacement:
                if (placementActive)
                {
                    CancelLocalPlacement();
                    return Finish(true, "Placement cancelled.", out message);
                }

                return TryBeginPlacement(out message);

            case FlotationPlacementIntentKind.CommitPlacement:
                if (!placementActive)
                    return Finish(false, "Placement mode is not active.", out message);

                if (!request.HasAimWorld)
                    return Finish(false, "No world aim point is available.", out message);

                return TryCommitPlacement(request.AimWorld, out message);

            case FlotationPlacementIntentKind.CancelPlacement:
                CancelLocalPlacement();
                return Finish(true, "Placement cancelled.", out message);

            default:
                return Finish(false, "No flotation placement intent.", out message);
        }
    }

    public void SetLocalAimPreview(Vector2 aimWorld, bool hasAimWorld)
    {
        if (!placementActive)
        {
            HidePreview();
            return;
        }

        if (!StillHasPendingSelectedItem())
        {
            CancelLocalPlacement();
            return;
        }

        if (!hasAimWorld)
        {
            ShowPreview(aimWorld, false);
            return;
        }

        bool valid =
            TryResolveCandidate(
                aimWorld,
                out PlacementCandidate candidate,
                out _);

        ShowPreview(
            valid ? candidate.AttachmentWorldPoint : aimWorld,
            valid);
    }

    public void CancelLocalPlacement()
    {
        placementActive = false;
        _pendingItem = null;
        pendingItemInstanceId = string.Empty;
        ReleaseInteractionGate();
        UnregisterEscape();
        HidePreview();
    }

    public bool CloseFromEscape()
    {
        if (!placementActive)
            return false;

        CancelLocalPlacement();
        return true;
    }

    private bool TryBeginPlacement(out string message)
    {
        ResolveRefs();

        if (!HasGameplayAuthority)
            return Finish(false, "This peer does not own flotation placement authority.", out message);

        if (inventory == null)
            return Finish(false, "PlayerInventory could not be resolved.", out message);

        if (!TryGetSelectedItem(out ItemInstance selected, out _))
            return Finish(false, "Select a lift bag first.", out message);

        if (!IsDeployableFlotationItem(selected, out string reason))
            return Finish(false, reason, out message);

        _pendingItem = selected;
        pendingItemInstanceId = selected.InstanceId;
        placementActive = true;
        AcquireInteractionGate();
        RegisterEscape();

        return Finish(true, "Flotation attachment mode active.", out message);
    }

    private bool TryCommitPlacement(Vector2 aimWorld, out string message)
    {
        if (!HasGameplayAuthority)
            return Finish(false, "This peer does not own flotation placement authority.", out message);

        if (!StillHasPendingSelectedItem())
        {
            CancelLocalPlacement();
            return Finish(false, "The selected lift bag changed before placement committed.", out message);
        }

        if (!TryResolveCandidate(
                aimWorld,
                out PlacementCandidate candidate,
                out string targetReason))
        {
            return Finish(false, targetReason, out message);
        }

        if (!TryExtractOneSelected(
                _pendingItem,
                out ItemInstance deployedItem,
                out SelectedItemExtraction extraction,
                out string extractionReason))
        {
            return Finish(false, extractionReason, out message);
        }

        Vector3 spawnPosition =
            (Vector3)(candidate.AttachmentWorldPoint + bagSpawnOffsetFromTarget);

        if (!WorldItemDropUtility.TryDrop(
                deployedItem,
                spawnPosition,
                gameObject,
                out WorldItem spawnedWorldItem) ||
            spawnedWorldItem == null)
        {
            RollbackExtraction(extraction, deployedItem);
            return Finish(false, "The lift bag WorldItem could not be spawned.", out message);
        }

        RuntimeFlotationAttachment attachment =
            spawnedWorldItem.GetComponent<RuntimeFlotationAttachment>();

        DeployableFlotationBag bag =
            spawnedWorldItem.GetComponent<DeployableFlotationBag>();

        if (attachment == null || bag == null)
        {
            Destroy(spawnedWorldItem.gameObject);
            RollbackExtraction(extraction, deployedItem);
            return Finish(
                false,
                "The selected item's WorldPrefab is missing flotation runtime components.",
                out message);
        }

        bool attached =
            candidate.TargetBody != null
                ? attachment.TryAttachToBody(
                    candidate.TargetBody,
                    candidate.AttachmentWorldPoint)
                : attachment.TryAttachToWorld(
                    candidate.AttachmentWorldPoint);

        if (!attached)
        {
            Destroy(spawnedWorldItem.gameObject);
            RollbackExtraction(extraction, deployedItem);
            return Finish(false, "Authority rejected the flotation attachment.", out message);
        }

        string targetLabel =
            candidate.TargetBody != null
                ? candidate.TargetBody.name
                : "fixed world";

        CancelLocalPlacement();

        return Finish(
            true,
            $"Lift bag attached to {targetLabel}. Interact with the bag to inflate it.",
            out message);
    }

    private bool TryResolveCandidate(
        Vector2 aimWorld,
        out PlacementCandidate candidate,
        out string reason)
    {
        candidate = default;
        reason = null;

        float range = Vector2.Distance(PlacementOriginWorld, aimWorld);
        if (range > Mathf.Max(0.1f, maxPlacementRange))
        {
            reason = $"Attachment point is too far away ({range:F2}m).";
            return false;
        }

        Collider2D[] hits =
            targetAssistRadius > 0f
                ? Physics2D.OverlapCircleAll(
                    aimWorld,
                    targetAssistRadius,
                    targetMask)
                : Physics2D.OverlapPointAll(
                    aimWorld,
                    targetMask);

        if (hits == null || hits.Length == 0)
        {
            reason = "Click a physical object or terrain collider.";
            return false;
        }

        Collider2D best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !hit.enabled)
                continue;

            if (!allowTriggerTargets && hit.isTrigger)
                continue;

            Rigidbody2D body = hit.attachedRigidbody;
            bool movingBody =
                body != null &&
                body.bodyType != RigidbodyType2D.Static;

            Vector2 closest = hit.ClosestPoint(aimWorld);
            float distance = Vector2.Distance(aimWorld, closest);
            float area =
                Mathf.Max(
                    0.0001f,
                    hit.bounds.size.x * hit.bounds.size.y);

            // Prefer an actual moving physical body over coincident terrain,
            // then prefer a precise/smaller collider under the cursor. This is
            // intentionally deterministic enough to avoid "first collider wins"
            // nonsense when several physics shapes overlap.
            float score =
                (movingBody ? 10000f : 0f) -
                (distance * 1000f) -
                Mathf.Min(area, 1000f);

            if (best == null || score > bestScore)
            {
                best = hit;
                bestScore = score;
            }
        }

        if (best == null)
        {
            reason = allowTriggerTargets
                ? "No valid attachment collider was found."
                : "Only trigger/sensor colliders are under the cursor.";
            return false;
        }

        Rigidbody2D bestBody = best.attachedRigidbody;

        if (bestBody != null &&
            bestBody.bodyType == RigidbodyType2D.Static)
        {
            bestBody = null;
        }

        Vector2 attachmentPoint =
            best.ClosestPoint(aimWorld);

        candidate =
            new PlacementCandidate(
                best,
                bestBody,
                attachmentPoint);

        return true;
    }

    private bool TryGetSelectedItem(
        out ItemInstance item,
        out BottomBarSlotType slotType)
    {
        item = null;
        slotType = BottomBarSlotType.None;

        if (inventory == null)
            return false;

        slotType = inventory.SelectedSlot;
        int hotbarIndex = PlayerInventory.SlotTypeToHotbarIndex(slotType);

        if (hotbarIndex >= 0)
        {
            InventorySlot slot = inventory.GetSlot(hotbarIndex);
            item = slot != null && !slot.IsEmpty ? slot.Instance : null;
            return item != null;
        }

        if (inventory.Equipment == null)
            return false;

        item = inventory.Equipment.Get(slotType);
        return item != null;
    }

    private bool StillHasPendingSelectedItem()
    {
        return
            placementActive &&
            _pendingItem != null &&
            TryGetSelectedItem(out ItemInstance current, out _) &&
            ReferenceEquals(current, _pendingItem) &&
            current.Quantity > 0;
    }

    private static bool IsDeployableFlotationItem(
        ItemInstance item,
        out string reason)
    {
        reason = null;

        if (item == null || item.Definition == null || item.Quantity <= 0)
        {
            reason = "Select a valid lift bag first.";
            return false;
        }

        WorldItem prefab = item.Definition.WorldPrefab;
        if (prefab == null)
        {
            reason = "The selected item has no WorldPrefab.";
            return false;
        }

        if (prefab.GetComponent<DeployableFlotationBag>() == null ||
            prefab.GetComponent<RuntimeFlotationAttachment>() == null)
        {
            reason = $"{item.Definition.DisplayName} is not a deployable flotation item.";
            return false;
        }

        if (!item.Definition.Droppable)
        {
            reason = "The lift bag ItemDefinition must be droppable so it can manifest in the world.";
            return false;
        }

        return true;
    }

    private bool TryExtractOneSelected(
        ItemInstance expected,
        out ItemInstance extracted,
        out SelectedItemExtraction extraction,
        out string reason)
    {
        extracted = null;
        extraction = default;
        reason = null;

        if (!TryGetSelectedItem(out ItemInstance current, out BottomBarSlotType slotType) ||
            !ReferenceEquals(current, expected))
        {
            reason = "The selected inventory item changed.";
            return false;
        }

        int hotbarIndex = PlayerInventory.SlotTypeToHotbarIndex(slotType);

        if (current.Quantity > 1)
        {
            extracted = current.SplitOff(1);
            if (extracted == null)
            {
                reason = "The selected lift-bag stack could not split one item.";
                return false;
            }

            extraction =
                new SelectedItemExtraction(
                    slotType,
                    hotbarIndex,
                    current,
                    removedWholeItem: false);

            NotifySourceChanged(slotType);
            return true;
        }

        if (hotbarIndex >= 0)
        {
            InventorySlot slot = inventory.GetSlot(hotbarIndex);
            if (slot == null || !ReferenceEquals(slot.Instance, current))
            {
                reason = "The selected hotbar slot changed.";
                return false;
            }

            slot.Clear();
            extracted = current;
            extraction =
                new SelectedItemExtraction(
                    slotType,
                    hotbarIndex,
                    current,
                    removedWholeItem: true);

            inventory.NotifyChanged();
            return true;
        }

        PlayerEquipment equipment = inventory.Equipment;
        if (equipment == null)
        {
            reason = "PlayerEquipment could not be resolved.";
            return false;
        }

        ItemInstance removed = equipment.Remove(slotType);
        if (!ReferenceEquals(removed, current))
        {
            if (removed != null)
                equipment.TryPlace(slotType, removed, out _);

            reason = "The selected equipment slot could not yield the lift bag.";
            return false;
        }

        extracted = removed;
        extraction =
            new SelectedItemExtraction(
                slotType,
                hotbarIndex: -1,
                current,
                removedWholeItem: true);

        inventory.NotifyChanged();
        return true;
    }

    private void RollbackExtraction(
        SelectedItemExtraction extraction,
        ItemInstance extracted)
    {
        if (inventory == null || extracted == null)
            return;

        if (!extraction.RemovedWholeItem)
        {
            if (extraction.SourceInstance != null &&
                extraction.SourceInstance.AddQuantity(extracted.Quantity) == extracted.Quantity)
            {
                NotifySourceChanged(extraction.SlotType);
                return;
            }

            inventory.TryAddInstance(extracted);
            return;
        }

        if (extraction.HotbarIndex >= 0)
        {
            InventorySlot slot = inventory.GetSlot(extraction.HotbarIndex);
            if (slot != null && slot.IsEmpty)
            {
                slot.Set(extracted);
                inventory.NotifyChanged();
                return;
            }

            inventory.TryAddInstance(extracted);
            return;
        }

        PlayerEquipment equipment = inventory.Equipment;
        if (equipment != null &&
            equipment.TryPlace(extraction.SlotType, extracted, out _))
        {
            inventory.NotifyChanged();
            return;
        }

        inventory.TryAddInstance(extracted);
    }

    private void NotifySourceChanged(BottomBarSlotType slotType)
    {
        inventory?.NotifyChanged();

        if (PlayerInventory.SlotTypeToHotbarIndex(slotType) < 0)
            inventory?.Equipment?.NotifyChanged();
    }

    private void HandleInventorySelectionChanged()
    {
        if (placementActive && !StillHasPendingSelectedItem())
            CancelLocalPlacement();
    }

    private void ResolveRefs()
    {
        if (inventory == null)
        {
            inventory =
                GetComponent<PlayerInventory>() ??
                GetComponentInParent<PlayerInventory>(true) ??
                GetComponentInChildren<PlayerInventory>(true);
        }

        if (placementOrigin == null)
            placementOrigin = transform;

        if (previewIndicatorRenderer == null && previewIndicator != null)
            previewIndicatorRenderer = previewIndicator.GetComponent<SpriteRenderer>();
    }

    private void ConfigurePreview()
    {
        if (previewLine != null)
        {
            previewLine.useWorldSpace = true;
            previewLine.positionCount = 2;
        }
    }

    private void ShowPreview(Vector2 worldPoint, bool valid)
    {
        if (previewIndicator != null)
        {
            if (!previewIndicator.gameObject.activeSelf)
                previewIndicator.gameObject.SetActive(true);

            previewIndicator.position = worldPoint;
        }

        if (previewIndicatorRenderer != null)
        {
            previewIndicatorRenderer.color =
                valid ? validPreviewColor : invalidPreviewColor;
        }

        if (previewLine != null)
        {
            previewLine.enabled = true;
            previewLine.positionCount = 2;
            previewLine.SetPosition(0, PlacementOriginWorld);
            previewLine.SetPosition(1, worldPoint);
            previewLine.startColor = valid ? validPreviewColor : invalidPreviewColor;
            previewLine.endColor = valid ? validPreviewColor : invalidPreviewColor;
        }
    }

    private void HidePreview()
    {
        if (previewIndicator != null && previewIndicator.gameObject.activeSelf)
            previewIndicator.gameObject.SetActive(false);

        if (previewLine != null)
            previewLine.enabled = false;
    }

    private void RegisterEscape()
    {
        if (_escapeRegistered)
            return;

        EscapeCloseRegistry registry = EscapeCloseRegistry.GetOrFind();
        if (registry == null)
            return;

        registry.Register(this);
        _escapeRegistered = true;
    }

    private void UnregisterEscape()
    {
        if (!_escapeRegistered)
            return;

        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);

        _escapeRegistered = false;
    }

    private void AcquireInteractionGate()
    {
        if (_interactionGateHeld)
            return;

        InteractionInputBlocker.Push(this);
        _interactionGateHeld = true;
    }

    private void ReleaseInteractionGate()
    {
        if (!_interactionGateHeld)
            return;

        InteractionInputBlocker.Pop(this);
        _interactionGateHeld = false;
    }

    private bool Finish(bool ok, string result, out string message)
    {
        lastResult = result ?? string.Empty;
        message = lastResult;

        if (verboseLogging)
        {
            Debug.Log(
                $"[FlotationPlacement:{name}] {(ok ? "OK" : "FAIL")} | {lastResult}",
                this);
        }

        return ok;
    }

    private readonly struct PlacementCandidate
    {
        public readonly Collider2D Collider;
        public readonly Rigidbody2D TargetBody;
        public readonly Vector2 AttachmentWorldPoint;

        public PlacementCandidate(
            Collider2D collider,
            Rigidbody2D targetBody,
            Vector2 attachmentWorldPoint)
        {
            Collider = collider;
            TargetBody = targetBody;
            AttachmentWorldPoint = attachmentWorldPoint;
        }
    }

    private readonly struct SelectedItemExtraction
    {
        public readonly BottomBarSlotType SlotType;
        public readonly int HotbarIndex;
        public readonly ItemInstance SourceInstance;
        public readonly bool RemovedWholeItem;

        public SelectedItemExtraction(
            BottomBarSlotType slotType,
            int hotbarIndex,
            ItemInstance sourceInstance,
            bool removedWholeItem)
        {
            SlotType = slotType;
            HotbarIndex = hotbarIndex;
            SourceInstance = sourceInstance;
            RemovedWholeItem = removedWholeItem;
        }
    }
}
