using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class InventoryDragController : MonoBehaviour
{
    [SerializeField] private Image dragIcon;
    [SerializeField] private Text dragCount;
    [SerializeField] private TMP_Text dragCargoLabel;
    [SerializeField] private int dragCargoLabelMaxCharacters = 10;
    [SerializeField] private Canvas canvas;
    [SerializeField] private DisplacedItemResolver displacedItemResolver;
    [SerializeField] private PlayerInventoryUI playerInventoryUI;
    [SerializeField] private LoadoutContainerOverlayUI loadoutOverlayUI;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerInventoryInput inventoryInput;

    [Header("World Drop Targets")]
    [SerializeField] private LayerMask worldDropTargetMask = ~0;
    [SerializeField] private float worldDropTargetRadius = 0.2f;
    [SerializeField] private Camera worldCamera;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    [System.NonSerialized] private ItemInstance draggedItem;
    private InventorySlotUI sourceSlot;
    private bool isDragging;

    // Normal UI drags are reservation-backed. The authoritative ItemInstance stays
    // in its source slot until a drop actually commits.
    private bool _reservationBackedDrag;
    private ItemInstance _reservedSourceItem;
    private string _reservedSourceInstanceId;
    private int _reservedQuantity;
    private bool _reservationPreviewIsSynthetic;

    // A deployed sounding line is special: while the UI drag is in progress,
    // the ItemInstance stays authoritatively in Hands. We only release it into
    // the world if the player actually completes the drag away from Hands.
    private bool _draggingDeployedSounder;
    private HandheldSoundingLineController _deployedSounderController;

    private readonly List<RaycastResult> _raycastResults = new();

    private sealed class WorldDropTargetCandidate
    {
        public IWorldItemDropTarget Target;
        public Collider2D Collider;
        public bool DirectTarget;
        public bool ExactPointerHit;
        public float DistanceSqr;
        public float ColliderArea;
        public int HierarchyDepth;
    }

    private readonly List<WorldDropTargetCandidate> _worldDropCandidates = new();

    // Safety state. Drag teardown must never silently become a world drop.
    private bool _resolvingLifecycleDrag;
    private bool _applicationQuitting;

    // Drag-preview invalid-target visuals only need recomputing when the dragged
    // item reference/quantity changes, not every frame.
    private ItemInstance _lastPreviewItem;
    private int _lastPreviewQuantity = int.MinValue;

    public bool IsDragging => isDragging;
    public ItemInstance DraggedItem => draggedItem;
    public bool IsReservationBackedDrag => isDragging && _reservationBackedDrag;

    public int GetReservedQuantityFor(
        InventorySlotUI slot)
    {
        if (!isDragging ||
            !_reservationBackedDrag ||
            slot == null ||
            sourceSlot != slot)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            _reservedQuantity);
    }

    /// <summary>
    /// The exact PlayerInventory this UI drag controller operates on.
    /// Used by persistence to associate scene-level UI with the correct player
    /// without relying on transform hierarchy or arbitrary object searches.
    /// </summary>
    public PlayerInventory BoundInventory { get { ResolveDropReferences(); return inventory; } }

    private void ResolveDropReferences()
    {
        // Scene UI need not be parented to the player. Reuse its exact binding.
        if (inventory == null && playerInventoryUI != null)
            inventory = playerInventoryUI.BoundInventory;
        if (inventoryInput == null && inventory != null)
            inventoryInput = inventory.GetComponentInParent<PlayerInventoryInput>(true)
                ?? inventory.GetComponentInChildren<PlayerInventoryInput>(true);
    }

    /// <summary>
    /// Persistence boundary seam.
    ///
    /// A normal UI drag temporarily removes the ItemInstance from its bound slot.
    /// Before inventory/equipment persistence captures a snapshot, that transient
    /// ownership must be reconciled back into authoritative storage or the snapshot
    /// will incorrectly omit the dragged item.
    ///
    /// This method NEVER world-drops as a fallback.
    /// </summary>
    public bool PrepareForPersistenceCapture()
    {
        if (!isDragging &&
            draggedItem == null)
        {
            return true;
        }

        if (_reservationBackedDrag)
        {
            // Reservation-backed drags never removed authoritative inventory.
            // Persistence only needs the transient UI reservation cleared.
            Log(
                $"PrepareForPersistenceCapture | cancelling reservation-backed drag | " +
                $"source={DescribeSlot(sourceSlot)} reserved={_reservedQuantity} " +
                $"item={DescribeItem(_reservedSourceItem)}");

            Cleanup();
            return true;
        }

        if (_draggingDeployedSounder)
        {
            // Deployed sounder also remains authoritatively in Hands.
            Cleanup();
            return true;
        }

        if (draggedItem == null ||
            draggedItem.IsDepleted())
        {
            Cleanup();
            return true;
        }

        // Defensive compatibility path for any legacy caller that starts a detached
        // drag through BeginDrag(ItemInstance, InventorySlotUI).
        ItemInstance itemToResolve =
            draggedItem;

        if (TryResolveItemWithoutWorldDrop(
                itemToResolve,
                sourceSlot))
        {
            Log(
                $"PrepareForPersistenceCapture | reconciled legacy detached drag | " +
                $"item={DescribeItem(itemToResolve)}");

            Cleanup();
            return true;
        }

        KeepDragActive(
            itemToResolve,
            "PrepareForPersistenceCapture | legacy detached drag had no safe storage destination.");

        return false;
    }


    private void Awake()
    {
        if (displacedItemResolver == null)
            displacedItemResolver = GetComponentInParent<DisplacedItemResolver>(true);

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (inventoryInput == null)
            inventoryInput = GetComponentInParent<PlayerInventoryInput>(true);


        HideVisual();
        Log($"Awake | canvas={(canvas != null ? canvas.name : "NULL")} | dragIcon={(dragIcon != null ? dragIcon.name : "NULL")}");
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }

    private void OnDisable()
    {
        if (_applicationQuitting)
            return;

        if (isDragging || draggedItem != null)
            ResolveInterruptedDragWithoutWorldDrop("OnDisable");
    }

    private void OnDestroy()
    {
        if (_applicationQuitting)
            return;

        if (isDragging || draggedItem != null)
            ResolveInterruptedDragWithoutWorldDrop("OnDestroy");
    }

    private void Update()
    {
        if (!isDragging)
        {
            if (dragIcon != null && dragIcon.enabled)
                HideVisual();

            return;
        }

        if (!isDragging || dragIcon == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            canvas.worldCamera,
            out Vector2 localPoint);

        dragIcon.rectTransform.anchoredPosition = localPoint;

        if (dragCount != null)
            dragCount.rectTransform.anchoredPosition = localPoint;

        if (dragCargoLabel != null)
            dragCargoLabel.rectTransform.anchoredPosition = localPoint;

        RefreshDragPreviewsIfChanged();

        if (Input.GetMouseButtonDown(1))
        {
            InventorySlotUI target = FindTargetSlotUnderMouse();

            if (target != null)
            {
                bool deposited = TryDepositSingleInto(target);
                if (deposited)
                    target.Refresh();

                return;
            }

            // Right-click over a world drop target means "deposit one into that
            // target" first. Raw world spawning is only allowed when there is no
            // world-drop target under/near the pointer at all.
            bool worldTargetHandled =
                TryDepositSingleIntoWorldTarget(
                    out bool hadWorldTargetCandidate);

            if (!worldTargetHandled &&
                !hadWorldTargetCandidate)
            {
                TryDropSingleToWorld();
            }
        }
    }

    public void BeginDrag(
        InventorySlotUI slot)
    {
        BeginDrag(
            slot,
            requestedQuantity: -1);
    }


    public void BeginDrag(
        InventorySlotUI slot,
        int requestedQuantity)
    {
        if (slot == null)
        {
            LogWarning(
                "BeginDrag ignored because slot was null.");
            return;
        }

        if (isDragging)
        {
            LogWarning(
                "BeginDrag ignored because a drag is already active.");
            return;
        }

        if (TryBeginDeployedSounderDrag(
                slot))
        {
            return;
        }

        ItemInstance sourceItem =
            slot.GetBoundItem();

        if (sourceItem == null ||
            sourceItem.Definition == null ||
            sourceItem.IsDepleted())
        {
            LogWarning(
                $"BeginDrag failed | source={DescribeSlot(slot)} | source item was null/invalid");
            return;
        }

        int quantity =
            requestedQuantity <= 0
                ? sourceItem.Quantity
                : Mathf.Clamp(
                    requestedQuantity,
                    1,
                    sourceItem.Quantity);

        // Partial reservations are only meaningful for splittable stack items.
        if (quantity < sourceItem.Quantity &&
            !sourceItem.CanSplit)
        {
            quantity =
                sourceItem.Quantity;
        }

        sourceSlot =
            slot;

        _reservationBackedDrag =
            true;

        _reservedSourceItem =
            sourceItem;

        _reservedSourceInstanceId =
            sourceItem.InstanceId;

        _reservedQuantity =
            quantity;

        _reservationPreviewIsSynthetic =
            quantity < sourceItem.Quantity;

        draggedItem =
            _reservationPreviewIsSynthetic
                ? ItemInstance.Create(
                    sourceItem.Definition,
                    quantity)
                : sourceItem;

        if (draggedItem == null ||
            draggedItem.Definition == null)
        {
            LogWarning(
                $"BeginDrag failed creating reservation preview | source={DescribeSlot(slot)}");
            ClearReservationMetadata();
            sourceSlot = null;
            return;
        }

        isDragging =
            true;

        ShowVisual(
            draggedItem);

        sourceSlot.Refresh();

        Log(
            $"BeginDrag RESERVATION | source={DescribeSlot(sourceSlot)} | " +
            $"authoritative={DescribeItem(_reservedSourceItem)} | " +
            $"reserved={_reservedQuantity} | partial={_reservationPreviewIsSynthetic}");
    }

    private bool TryValidateReservation(
        out ItemInstance currentSource)
    {
        currentSource =
            null;

        if (!isDragging ||
            !_reservationBackedDrag ||
            sourceSlot == null ||
            _reservedSourceItem == null ||
            _reservedQuantity <= 0)
        {
            return false;
        }

        currentSource =
            sourceSlot.GetBoundItem();

        if (currentSource == null ||
            currentSource.Definition == null ||
            currentSource.IsDepleted())
        {
            LogWarning(
                $"Reservation invalid because source is empty/invalid | " +
                $"source={DescribeSlot(sourceSlot)}");
            return false;
        }

        bool sameReference =
            ReferenceEquals(
                currentSource,
                _reservedSourceItem);

        bool sameId =
            !string.IsNullOrWhiteSpace(
                _reservedSourceInstanceId) &&
            string.Equals(
                currentSource.InstanceId,
                _reservedSourceInstanceId,
                System.StringComparison.Ordinal);

        if (!sameReference &&
            !sameId)
        {
            LogWarning(
                $"Reservation invalid because source item changed | " +
                $"expected={DescribeItem(_reservedSourceItem)} | " +
                $"actual={DescribeItem(currentSource)}");
            return false;
        }

        if (currentSource.Quantity <
            _reservedQuantity)
        {
            LogWarning(
                $"Reservation invalid because source quantity fell below reservation | " +
                $"sourceQty={currentSource.Quantity} reserved={_reservedQuantity}");
            return false;
        }

        return true;
    }

    private bool TryExtractReservedQuantity(
        int quantity,
        out ItemInstance extracted)
    {
        extracted =
            null;

        if (!TryValidateReservation(
                out ItemInstance currentSource))
        {
            return false;
        }

        int amount =
            Mathf.Clamp(
                quantity,
                1,
                _reservedQuantity);

        if (amount > currentSource.Quantity)
            return false;

        if (amount == currentSource.Quantity)
        {
            extracted =
                sourceSlot.RemoveItem();
        }
        else
        {
            extracted =
                currentSource.SplitOff(
                    amount);
        }

        if (extracted == null ||
            extracted.Definition == null ||
            extracted.IsDepleted())
        {
            LogWarning(
                $"TryExtractReservedQuantity failed | amount={amount} " +
                $"source={DescribeSlot(sourceSlot)}");
            return false;
        }

        sourceSlot.Refresh();

        return true;
    }

    private bool TryDetachEntireReservation(
        out ItemInstance extracted)
    {
        extracted =
            null;

        if (!_reservationBackedDrag ||
            _reservedQuantity <= 0)
        {
            return false;
        }

        int amount =
            _reservedQuantity;

        if (!TryExtractReservedQuantity(
                amount,
                out extracted))
        {
            return false;
        }

        // From this point onward, the existing transaction machinery may treat
        // the extracted object exactly like the old drag implementation did.
        // If commit fails it is restored through the normal rollback paths.
        _reservationBackedDrag =
            false;

        _reservedSourceItem =
            null;

        _reservedSourceInstanceId =
            null;

        _reservedQuantity =
            0;

        _reservationPreviewIsSynthetic =
            false;

        draggedItem =
            extracted;

        ShowVisual(
            draggedItem);

        return true;
    }

    private void CommitOneReservedUnit()
    {
        if (!_reservationBackedDrag)
            return;

        _reservedQuantity =
            Mathf.Max(
                0,
                _reservedQuantity - 1);

        if (_reservedQuantity <= 0)
        {
            Cleanup();
            return;
        }

        ItemInstance currentSource =
            sourceSlot != null
                ? sourceSlot.GetBoundItem()
                : null;

        if (currentSource == null ||
            currentSource.Definition == null)
        {
            LogWarning(
                "CommitOneReservedUnit lost its reservation source after commit. " +
                "Clearing the remaining UI reservation; authoritative inventory is unchanged.");
            Cleanup();
            return;
        }

        _reservedSourceItem =
            currentSource;

        _reservedSourceInstanceId =
            currentSource.InstanceId;

        if (_reservationPreviewIsSynthetic)
        {
            draggedItem =
                ItemInstance.Create(
                    currentSource.Definition,
                    _reservedQuantity);
        }
        else
        {
            draggedItem =
                currentSource;
        }

        ShowVisual(
            draggedItem);

        sourceSlot?.Refresh();
    }

    private bool RestoreExtractedReservationUnit(
        ItemInstance extracted)
    {
        if (extracted == null ||
            extracted.IsDepleted())
        {
            return true;
        }

        if (sourceSlot != null)
        {
            if (sourceSlot.TryPlaceItem(
                    extracted,
                    out ItemInstance remainder))
            {
                if (remainder == null ||
                    remainder.IsDepleted())
                {
                    sourceSlot.Refresh();
                    return true;
                }

                extracted =
                    remainder;
            }
        }

        if (inventory != null &&
            inventory.CanFullyAdd(
                extracted) &&
            inventory.TryAddInstance(
                extracted))
        {
            // The rollback no longer lives at the reservation source, so the
            // reservation itself is no longer valid. The uncommitted remainder
            // was never removed and remains safely in inventory.
            Cleanup();
            return true;
        }

        return false;
    }

    private void ConvertFailedReservationUnitToDetachedDrag(
        ItemInstance extracted,
        string reason)
    {
        // Any unextracted reserved quantity never left the source and is therefore
        // safe. Only the one extracted unit remains transient.
        ClearReservationMetadata();

        draggedItem =
            extracted;

        isDragging =
            extracted != null &&
            !extracted.IsDepleted();

        if (isDragging)
            ShowVisual(draggedItem);
        else
            HideVisual();

        sourceSlot?.Refresh();

        LogWarning(
            $"{reason} Remaining reservation was cancelled; extracted unit retained as detached drag.");
    }

    private void ClearReservationMetadata()
    {
        _reservationBackedDrag =
            false;

        _reservedSourceItem =
            null;

        _reservedSourceInstanceId =
            null;

        _reservedQuantity =
            0;

        _reservationPreviewIsSynthetic =
            false;
    }

    public void BeginDrag(
        ItemInstance item,
        InventorySlotUI slot)
    {
        // Compatibility overload. The normal InventorySlotUI path no longer uses
        // detached ItemInstances. If the supplied item is still the authoritative
        // source object, convert it to a reservation. Otherwise preserve it as a
        // legacy detached drag so older callers do not silently break.
        if (item == null ||
            item.Definition == null)
        {
            LogWarning(
                "BeginDrag(item, slot) failed because item was null/invalid.");
            return;
        }

        if (isDragging)
        {
            LogWarning(
                "BeginDrag(item, slot) ignored because drag is already active.");
            return;
        }

        if (slot != null &&
            ReferenceEquals(
                slot.GetBoundItem(),
                item))
        {
            BeginDrag(
                slot,
                item.Quantity);
            return;
        }

        ClearReservationMetadata();

        draggedItem =
            item;

        sourceSlot =
            slot;

        isDragging =
            true;

        ShowVisual(
            item);

        LogWarning(
            $"BeginDrag(item, slot) | LEGACY DETACHED path | " +
            $"source={DescribeSlot(sourceSlot)} | item={DescribeItem(draggedItem)}");
    }


    public void EndDrag()
    {
        if (!isDragging)
        {
            Log(
                "EndDrag ignored because not dragging.");
            return;
        }

        if (IsLikelyLifecycleInterruptedEndDrag())
        {
            ResolveInterruptedDragWithoutWorldDrop(
                "EndDrag lifecycle interruption");
            return;
        }

        InventorySlotUI target =
            FindTargetSlotUnderMouse();

        if (_draggingDeployedSounder)
        {
            ItemInstance sounder =
                draggedItem;

            if (target == sourceSlot)
            {
                Cleanup();
                return;
            }

            if (_deployedSounderController != null &&
                _deployedSounderController.IsActiveDeployedSounder(
                    sounder) &&
                _deployedSounderController.TryReleaseDeployedSounderToWorld(
                    sounder,
                    out _))
            {
                Log(
                    "EndDrag | deployed sounding line released at its existing physical proxy position.");

                Cleanup();
                return;
            }

            LogWarning(
                "EndDrag | deployed sounding-line release failed; item remains in Hands.");

            Cleanup();
            return;
        }

        if (_reservationBackedDrag)
        {
            // Returning to the source is a pure reservation cancellation. Nothing
            // authoritative ever moved.
            if (target == sourceSlot)
            {
                Cleanup();
                return;
            }

            if (!TryDetachEntireReservation(
                    out ItemInstance extracted))
            {
                LogWarning(
                    "EndDrag | reservation could not be validated/extracted. Cancelling transient drag.");
                Cleanup();
                return;
            }

            draggedItem =
                extracted;
        }

        ItemInstance working =
            draggedItem;

        Log(
            $"EndDrag COMMIT | source={DescribeSlot(sourceSlot)} | " +
            $"target={DescribeSlot(target)} | item={DescribeItem(working)}");

        if (working == null ||
            working.IsDepleted())
        {
            Cleanup();
            return;
        }

        if (target != null)
        {
            if (TryCommitUiDropTransaction(
                    target,
                    working))
            {
                return;
            }

            target.PlayInvalidTargetFeedback();

            if (TryResolveItemWithoutWorldDrop(
                    working,
                    sourceSlot))
            {
                Cleanup();
                return;
            }

            KeepDragActive(
                working,
                "EndDrag | UI target rejected/rolled back and item could not be safely restored.");
            return;
        }

        Log(
            "EndDrag | no UI target under mouse, trying world container target.");

        if (TryDepositDraggedItemIntoWorldTarget(
                working,
                out ItemInstance remainder,
                out bool hadWorldTargetCandidate))
        {
            if (remainder == null ||
                remainder.IsDepleted())
            {
                Cleanup();
                return;
            }

            if (TryResolveItemWithoutWorldDrop(
                    remainder,
                    sourceSlot))
            {
                Cleanup();
                return;
            }

            KeepDragActive(
                remainder,
                "EndDrag | partial world-target deposit succeeded but remainder could not be safely restored.");
            return;
        }

        if (hadWorldTargetCandidate)
        {
            LogWarning(
                "EndDrag | world target(s) were present but none accepted the item; cancelling drop safely.");

            if (TryResolveItemWithoutWorldDrop(
                    working,
                    sourceSlot))
            {
                Cleanup();
                return;
            }

            KeepDragActive(
                working,
                "EndDrag | rejected world target and item could not be safely restored.");
            return;
        }

        Log(
            "EndDrag | no UI/world target under mouse, dropping dragged item into world.");

        if (TryDropItemToWorld(
                working))
        {
            Cleanup();
            return;
        }

        LogWarning(
            "EndDrag | intentional world drop failed; restoring item instead.");

        if (TryResolveItemWithoutWorldDrop(
                working,
                sourceSlot))
        {
            Cleanup();
            return;
        }

        KeepDragActive(
            working,
            "EndDrag | world drop failed and item could not be safely restored.");
    }


    public bool IsDraggingFrom(InventorySlotUI slot)
    {
        return isDragging && sourceSlot == slot;
    }

    private bool TryBeginDeployedSounderDrag(
        InventorySlotUI slot)
    {
        if (slot == null ||
            slot.SlotType !=
                BottomBarSlotType.Hands ||
            inventory == null ||
            inventory.Equipment == null)
        {
            return false;
        }

        ItemInstance held =
            inventory.Equipment.Get(
                BottomBarSlotType.Hands);

        if (held == null ||
            held.Definition == null)
        {
            return false;
        }

        HandheldSoundingLineController controller =
            inventory.GetComponent<HandheldSoundingLineController>();

        if (controller == null)
        {
            controller =
                inventory.GetComponentInParent<HandheldSoundingLineController>();
        }

        if (controller == null)
        {
            controller =
                inventory.GetComponentInChildren<HandheldSoundingLineController>(
                    true);
        }

        if (controller == null ||
            !controller.IsActiveDeployedSounder(
                held))
        {
            return false;
        }

        draggedItem =
            held;

        sourceSlot =
            slot;

        isDragging =
            true;

        _draggingDeployedSounder =
            true;

        _deployedSounderController =
            controller;

        ShowVisual(
            held);

        Log(
            $"BeginDrag | deployed sounding line remains in Hands until drag commits | item={DescribeItem(held)}");

        return true;
    }

    private InventorySlotUI FindTargetSlotUnderMouse()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            LogWarning("FindTargetSlotUnderMouse failed because EventSystem.current is null.");
            return null;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        _raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject go = _raycastResults[i].gameObject;
            if (go == null)
                continue;

            InventorySlotUI slot = go.GetComponentInParent<InventorySlotUI>();
            if (slot != null)
            {
                Log($"FindTargetSlotUnderMouse | hit={go.name} | resolvedSlot={DescribeSlot(slot)}");
                return slot;
            }
        }

        Log("FindTargetSlotUnderMouse | no InventorySlotUI hit.");
        return null;
    }

    private void ShowVisual(ItemInstance item)
    {
        bool hasItem = item != null && item.Definition != null;
        bool isCargo = hasItem && CargoLabelFormatter.IsCargo(item);

        if (dragIcon != null)
        {
            dragIcon.enabled = hasItem;
            dragIcon.sprite = hasItem ? item.VisualIcon : null;
        }

        if (dragCount != null)
        {
            bool showCount = hasItem && !isCargo && item.Quantity > 1;
            dragCount.gameObject.SetActive(showCount);
            dragCount.text = showCount ? item.Quantity.ToString() : "";
        }

        if (dragCargoLabel != null)
        {
            dragCargoLabel.gameObject.SetActive(isCargo);
            dragCargoLabel.text = isCargo
                ? CargoLabelFormatter.Format(item.Definition, dragCargoLabelMaxCharacters)
                : "";

            dragCargoLabel.alignment = TextAlignmentOptions.Center;
            dragCargoLabel.color = Color.black;
            dragCargoLabel.textWrappingMode = TextWrappingModes.NoWrap;
            dragCargoLabel.raycastTarget = false;
        }

        RefreshDragPreviewsIfChanged();
    }

    private void HideVisual()
    {
        if (dragIcon != null)
        {
            dragIcon.enabled = false;
            dragIcon.sprite = null;
        }

        if (dragCount != null)
        {
            dragCount.text = "";
            dragCount.gameObject.SetActive(false);
        }

        if (dragCargoLabel != null)
        {
            dragCargoLabel.text = "";
            dragCargoLabel.gameObject.SetActive(false);
        }
    }

    private void Cleanup()
    {
        InventorySlotUI previousSource =
            sourceSlot;

        Log(
            $"Cleanup | item={DescribeItem(draggedItem)} | " +
            $"source={DescribeSlot(sourceSlot)} | reservation={_reservationBackedDrag} " +
            $"reservedQty={_reservedQuantity}");

        draggedItem =
            null;

        sourceSlot =
            null;

        isDragging =
            false;

        ClearReservationMetadata();

        _draggingDeployedSounder =
            false;

        _deployedSounderController =
            null;

        HideVisual();

        _lastPreviewItem =
            null;

        _lastPreviewQuantity =
            int.MinValue;

        playerInventoryUI?.RefreshDragPreview(
            null);

        loadoutOverlayUI?.RefreshDragPreview(
            null);

        if (previousSource != null &&
            previousSource.isActiveAndEnabled)
        {
            previousSource.Refresh();
        }
    }


    private string DescribeSlot(InventorySlotUI slot)
    {
        if (slot == null)
            return "NULL";

        return $"{slot.name} type={slot.SlotType} equip={slot.IsEquipmentSlot} hotbarIndex={slot.HotbarIndex}";
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string itemId = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{itemId} x{item.Quantity} inst={item.InstanceId}";
    }

    public bool TryDepositSingleInto(
        InventorySlotUI target)
    {
        if (!isDragging ||
            draggedItem == null ||
            target == null)
        {
            return false;
        }

        if (_draggingDeployedSounder)
        {
            if (target == sourceSlot)
            {
                Cleanup();
                return true;
            }

            bool released =
                _deployedSounderController != null &&
                _deployedSounderController.IsActiveDeployedSounder(
                    draggedItem) &&
                _deployedSounderController.TryReleaseDeployedSounderToWorld(
                    draggedItem,
                    out _);

            if (released)
                Cleanup();

            return released;
        }

        if (_reservationBackedDrag)
        {
            if (target == sourceSlot)
                return false;

            if (!TryExtractReservedQuantity(
                    1,
                    out ItemInstance single))
            {
                Cleanup();
                return false;
            }

            if (!CanAcceptWithoutDisplacingExisting(
                    target,
                    single))
            {
                target.PlayInvalidTargetFeedback();

                if (!RestoreExtractedReservationUnit(
                        single))
                {
                    ConvertFailedReservationUnitToDetachedDrag(
                        single,
                        "TryDepositSingleInto | target rejected and rollback failed.");
                }

                return false;
            }

            ItemInstance targetBefore =
                target.GetBoundItem();

            if (!target.TryPlaceItem(
                    single,
                    out ItemInstance remainder))
            {
                if (!RestoreExtractedReservationUnit(
                        single))
                {
                    ConvertFailedReservationUnitToDetachedDrag(
                        single,
                        "TryDepositSingleInto | placement failed and rollback failed.");
                }

                return false;
            }

            if (remainder != null &&
                !remainder.IsDepleted())
            {
                bool targetRolledBack =
                    TryRollbackUnexpectedSinglePlacement(
                        target,
                        targetBefore,
                        single,
                        remainder);

                if (!targetRolledBack ||
                    !RestoreExtractedReservationUnit(
                        single))
                {
                    ConvertFailedReservationUnitToDetachedDrag(
                        single,
                        "TryDepositSingleInto | unexpected displacement rollback failed.");
                }

                return false;
            }

            CommitOneReservedUnit();
            return true;
        }

        // Compatibility path for any legacy detached drag.
        if (draggedItem.Quantity <= 0)
            return false;

        if (!CanAcceptWithoutDisplacingExisting(
                target,
                draggedItem))
        {
            target.PlayInvalidTargetFeedback();
            return false;
        }

        if (draggedItem.Quantity == 1)
        {
            ItemInstance before =
                target.GetBoundItem();

            if (!target.TryPlaceItem(
                    draggedItem,
                    out ItemInstance remainder))
            {
                return false;
            }

            if (remainder != null &&
                !remainder.IsDepleted())
            {
                if (!TryRollbackUnexpectedSinglePlacement(
                        target,
                        before,
                        draggedItem,
                        remainder))
                {
                    KeepDragActive(
                        draggedItem,
                        "TryDepositSingleInto legacy path rollback failed.");
                }

                return false;
            }

            Cleanup();
            return true;
        }

        ItemInstance singleToPlace =
            draggedItem.SplitOff(
                1);

        if (singleToPlace == null)
            return false;

        ItemInstance beforeItem =
            target.GetBoundItem();

        if (!target.TryPlaceItem(
                singleToPlace,
                out ItemInstance displacedOrRemainder))
        {
            RestoreSplitQuantity(
                draggedItem,
                singleToPlace);
            return false;
        }

        if (displacedOrRemainder != null &&
            !displacedOrRemainder.IsDepleted())
        {
            if (!TryRollbackUnexpectedSinglePlacement(
                    target,
                    beforeItem,
                    singleToPlace,
                    displacedOrRemainder))
            {
                LogWarning(
                    "TryDepositSingleInto legacy path unexpected displacement rollback failed.");
                return false;
            }

            RestoreSplitQuantity(
                draggedItem,
                singleToPlace);

            ShowVisual(
                draggedItem);

            return false;
        }

        ShowVisual(
            draggedItem);

        return true;
    }


    public void CancelDrag()
    {
        if (!isDragging &&
            draggedItem == null)
        {
            HideVisual();
            playerInventoryUI?.RefreshDragPreview(null);
            loadoutOverlayUI?.RefreshDragPreview(null);
            return;
        }

        Log(
            $"CancelDrag | item={DescribeItem(draggedItem)} | " +
            $"source={DescribeSlot(sourceSlot)} | reservation={_reservationBackedDrag}");

        if (_reservationBackedDrag ||
            _draggingDeployedSounder)
        {
            // Nothing authoritative left storage.
            Cleanup();
            return;
        }

        if (draggedItem == null ||
            draggedItem.IsDepleted())
        {
            Cleanup();
            return;
        }

        if (TryResolveItemWithoutWorldDrop(
                draggedItem,
                sourceSlot))
        {
            Cleanup();
            return;
        }

        KeepDragActive(
            draggedItem,
            "CancelDrag | legacy detached item could not be restored without a world drop.");
    }


    private bool TryDropItemToWorld(ItemInstance item)
    {
        ResolveDropReferences();
        if (item == null || item.Definition == null)
            return false;

        if (inventory == null)
        {
            LogWarning($"TryDropItemToWorld failed because inventory is null | item={DescribeItem(item)}");
            return false;
        }

        Vector3 worldPos = inventoryInput != null
            ? inventoryInput.GetDropWorldPositionForUI()
            : inventory.transform.position + Vector3.right * .75f;

        bool ok = inventory.TryDropInstance(item, worldPos);
        Log($"TryDropItemToWorld | item={DescribeItem(item)} | pos={worldPos} | ok={ok}");
        return ok;
    }

    private bool TryDropSingleToWorld()
    {
        if (!isDragging ||
            draggedItem == null)
        {
            return false;
        }

        if (_draggingDeployedSounder)
        {
            bool released =
                _deployedSounderController != null &&
                _deployedSounderController.IsActiveDeployedSounder(
                    draggedItem) &&
                _deployedSounderController.TryReleaseDeployedSounderToWorld(
                    draggedItem,
                    out _);

            if (released)
                Cleanup();

            return released;
        }

        if (_reservationBackedDrag)
        {
            if (!TryExtractReservedQuantity(
                    1,
                    out ItemInstance single))
            {
                Cleanup();
                return false;
            }

            if (TryDropItemToWorld(
                    single))
            {
                CommitOneReservedUnit();
                return true;
            }

            if (!RestoreExtractedReservationUnit(
                    single))
            {
                ConvertFailedReservationUnitToDetachedDrag(
                    single,
                    "TryDropSingleToWorld | world drop failed and rollback failed.");
            }

            return false;
        }

        if (draggedItem.Quantity == 1)
        {
            if (TryDropItemToWorld(
                    draggedItem))
            {
                Cleanup();
                return true;
            }

            return false;
        }

        ItemInstance legacySingle =
            draggedItem.SplitOff(
                1);

        if (legacySingle == null)
            return false;

        if (TryDropItemToWorld(
                legacySingle))
        {
            ShowVisual(
                draggedItem);
            return true;
        }

        RestoreSplitQuantity(
            draggedItem,
            legacySingle);

        return false;
    }


    private int CollectWorldDropTargetCandidates(ItemInstance item)
    {
        ResolveDropReferences();
        _worldDropCandidates.Clear();

        Camera cam = worldCamera != null ? worldCamera : CameraManager.CameraForActor(inventory);
        if (cam == null)
        {
            LogWarning("CollectWorldDropTargetCandidates failed because no camera was available.");
            return 0;
        }

        Vector3 mouse = Input.mousePosition;
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        Vector2 point = new Vector2(world.x, world.y);

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            point,
            worldDropTargetRadius,
            worldDropTargetMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
                continue;

            // A target physically attached to the hit collider owns that collider.
            // Only fall back to a parent target when the collider has no direct target.
            // This prevents a dedicated child target (ballast slot, chest, rack slot, etc.)
            // from simultaneously impersonating a generic ancestor target.
            IWorldItemDropTarget target =
                col.GetComponent<IWorldItemDropTarget>();

            bool direct =
                target != null;

            if (target == null)
            {
                target =
                    col.GetComponentInParent<IWorldItemDropTarget>();
            }

            if (target == null)
                continue;

            WorldDropTargetCandidate candidate =
                BuildWorldDropTargetCandidate(
                    target,
                    col,
                    direct,
                    point);

            AddOrImproveWorldDropCandidate(
                candidate);
        }

        _worldDropCandidates.Sort(
            CompareWorldDropCandidates);

        if (_worldDropCandidates.Count == 0)
        {
            Log("CollectWorldDropTargetCandidates | no world drop target hit.");
            return 0;
        }

        if (verboseLogging)
        {
            for (int i = 0; i < _worldDropCandidates.Count; i++)
            {
                WorldDropTargetCandidate candidate =
                    _worldDropCandidates[i];

                Log(
                    $"World drop candidate[{i}] | " +
                    $"target={DescribeWorldDropTarget(candidate.Target)} | " +
                    $"collider={(candidate.Collider != null ? candidate.Collider.name : "NULL")} | " +
                    $"direct={candidate.DirectTarget} | " +
                    $"exact={candidate.ExactPointerHit} | " +
                    $"distance={Mathf.Sqrt(candidate.DistanceSqr):F3} | " +
                    $"area={candidate.ColliderArea:F3} | " +
                    $"depth={candidate.HierarchyDepth} | " +
                    $"item={DescribeItem(item)}");
            }
        }

        return _worldDropCandidates.Count;
    }

    private WorldDropTargetCandidate BuildWorldDropTargetCandidate(
        IWorldItemDropTarget target,
        Collider2D collider,
        bool directTarget,
        Vector2 pointerWorld)
    {
        bool exactPointerHit =
            collider != null &&
            collider.OverlapPoint(pointerWorld);

        Vector2 closest =
            collider != null
                ? collider.ClosestPoint(pointerWorld)
                : pointerWorld;

        float distanceSqr =
            exactPointerHit
                ? 0f
                : (closest - pointerWorld).sqrMagnitude;

        float area =
            float.MaxValue;

        int hierarchyDepth =
            0;

        if (collider != null)
        {
            Bounds bounds =
                collider.bounds;

            area =
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(
                        bounds.size.x *
                        bounds.size.y));

            hierarchyDepth =
                GetHierarchyDepth(
                    collider.transform);
        }

        return new WorldDropTargetCandidate
        {
            Target = target,
            Collider = collider,
            DirectTarget = directTarget,
            ExactPointerHit = exactPointerHit,
            DistanceSqr = distanceSqr,
            ColliderArea = area,
            HierarchyDepth = hierarchyDepth
        };
    }

    private void AddOrImproveWorldDropCandidate(
        WorldDropTargetCandidate candidate)
    {
        if (candidate == null ||
            candidate.Target == null)
        {
            return;
        }

        for (int i = 0; i < _worldDropCandidates.Count; i++)
        {
            WorldDropTargetCandidate existing =
                _worldDropCandidates[i];

            if (!ReferenceEquals(
                    existing.Target,
                    candidate.Target))
            {
                continue;
            }

            // The same parent target can be discovered through many child colliders.
            // Keep only its best spatial representation so it is evaluated once.
            if (CompareWorldDropCandidates(
                    candidate,
                    existing) < 0)
            {
                _worldDropCandidates[i] =
                    candidate;
            }

            return;
        }

        _worldDropCandidates.Add(
            candidate);
    }

    private static int CompareWorldDropCandidates(
        WorldDropTargetCandidate a,
        WorldDropTargetCandidate b)
    {
        if (ReferenceEquals(a, b))
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        // 1) A collider actually under the pointer beats one touched only by the
        //    forgiving overlap radius.
        if (a.ExactPointerHit !=
            b.ExactPointerHit)
        {
            return
                a.ExactPointerHit
                    ? -1
                    : 1;
        }

        // 2) Otherwise prefer the spatially closest candidate.
        int distanceCompare =
            a.DistanceSqr.CompareTo(
                b.DistanceSqr);

        if (distanceCompare != 0)
            return distanceCompare;

        // 3) Smaller colliders are normally the more intentional/specific target
        //    when several exact surfaces overlap (for example a chest sitting on a rack).
        int areaCompare =
            a.ColliderArea.CompareTo(
                b.ColliderArea);

        if (areaCompare != 0)
            return areaCompare;

        // 4) Prefer the deeper/more local hierarchy surface over an ancestor fixture.
        int depthCompare =
            b.HierarchyDepth.CompareTo(
                a.HierarchyDepth);

        if (depthCompare != 0)
            return depthCompare;

        // 5) Finally prefer a target attached directly to the collider over one
        //    inherited from a parent.
        if (a.DirectTarget !=
            b.DirectTarget)
        {
            return
                a.DirectTarget
                    ? -1
                    : 1;
        }

        Component aComponent =
            a.Target as Component;

        Component bComponent =
            b.Target as Component;

        int aId =
            aComponent != null
                ? aComponent.GetInstanceID()
                : 0;

        int bId =
            bComponent != null
                ? bComponent.GetInstanceID()
                : 0;

        return
            aId.CompareTo(
                bId);
    }

    private static int GetHierarchyDepth(
        Transform transform)
    {
        int depth =
            0;

        Transform current =
            transform;

        while (current != null)
        {
            depth++;
            current =
                current.parent;
        }

        return depth;
    }

    private string DescribeWorldDropTarget(
        IWorldItemDropTarget target)
    {
        if (target == null)
            return "NULL";

        Component component =
            target as Component;

        if (component == null)
            return target.GetType().Name;

        return
            $"{target.GetType().Name}('{component.name}')";
    }

    private bool TryDepositDraggedItemIntoWorldTarget(
        ItemInstance item,
        out ItemInstance remainder,
        out bool hadCandidates)
    {
        remainder = item;
        hadCandidates = false;

        if (item == null)
            return false;

        WorldItemDropContext dropContext =
            BuildWorldItemDropContext();

        if (!dropContext.HasRequester)
        {
            LogWarning(
                "TryDepositDraggedItemIntoWorldTarget failed because no requester could be resolved.");
            return false;
        }

        int candidateCount =
            CollectWorldDropTargetCandidates(
                item);

        hadCandidates =
            candidateCount > 0;

        if (candidateCount <= 0)
            return false;

        for (int i = 0;
             i < candidateCount;
             i++)
        {
            WorldDropTargetCandidate candidate =
                _worldDropCandidates[i];

            if (candidate == null ||
                candidate.Target == null)
            {
                continue;
            }

            IWorldItemDropTarget target =
                candidate.Target;

            if (!target.CanAcceptWorldDrop(
                    in dropContext,
                    item))
            {
                Log(
                    $"TryDepositDraggedItemIntoWorldTarget | candidate rejected preview | " +
                    $"candidate={i + 1}/{candidateCount} | " +
                    $"target={DescribeWorldDropTarget(target)} | " +
                    $"collider={(candidate.Collider != null ? candidate.Collider.name : "NULL")} | " +
                    $"item={DescribeItem(item)}");

                continue;
            }

            ItemInstance candidateRemainder =
                item;

            bool ok =
                target.TryAcceptWorldDrop(
                    in dropContext,
                    item,
                    out candidateRemainder);

            Log(
                $"TryDepositDraggedItemIntoWorldTarget | candidate attempted | " +
                $"candidate={i + 1}/{candidateCount} | " +
                $"target={DescribeWorldDropTarget(target)} | " +
                $"collider={(candidate.Collider != null ? candidate.Collider.name : "NULL")} | " +
                $"ok={ok} | remainder={DescribeItem(candidateRemainder)}");

            if (!ok)
                continue;

            remainder =
                candidateRemainder;

            return true;
        }

        remainder =
            item;

        Log(
            $"TryDepositDraggedItemIntoWorldTarget | all {candidateCount} candidates rejected/failed | " +
            $"item={DescribeItem(item)}");

        return false;
    }

    private bool TryDepositSingleIntoWorldTarget(
        out bool hadCandidates)
    {
        hadCandidates =
            false;

        if (!isDragging ||
            draggedItem == null ||
            draggedItem.IsDepleted())
        {
            return false;
        }

        if (_draggingDeployedSounder)
            return false;

        if (_reservationBackedDrag)
        {
            if (!TryExtractReservedQuantity(
                    1,
                    out ItemInstance single))
            {
                Cleanup();
                return false;
            }

            if (!TryDepositDraggedItemIntoWorldTarget(
                    single,
                    out ItemInstance remainder,
                    out hadCandidates))
            {
                if (!RestoreExtractedReservationUnit(
                        single))
                {
                    ConvertFailedReservationUnitToDetachedDrag(
                        single,
                        "TryDepositSingleIntoWorldTarget | target rejected and rollback failed.");
                }

                return false;
            }

            int remainderQuantity =
                remainder == null ||
                remainder.IsDepleted()
                    ? 0
                    : remainder.Quantity;

            if (remainderQuantity > 0)
            {
                if (!RestoreExtractedReservationUnit(
                        remainder))
                {
                    ConvertFailedReservationUnitToDetachedDrag(
                        remainder,
                        "TryDepositSingleIntoWorldTarget | partial remainder rollback failed.");
                    return true;
                }

                // A one-unit deposit that returns one unit accepted nothing.
                return false;
            }

            CommitOneReservedUnit();
            return true;
        }

        if (draggedItem.Quantity == 1)
        {
            if (!TryDepositDraggedItemIntoWorldTarget(
                    draggedItem,
                    out ItemInstance remainder,
                    out hadCandidates))
            {
                return false;
            }

            if (remainder == null ||
                remainder.IsDepleted())
            {
                Cleanup();
                return true;
            }

            draggedItem =
                remainder;

            ShowVisual(
                draggedItem);

            return true;
        }

        ItemInstance legacySingle =
            draggedItem.SplitOff(
                1);

        if (legacySingle == null)
            return false;

        if (!TryDepositDraggedItemIntoWorldTarget(
                legacySingle,
                out ItemInstance legacyRemainder,
                out hadCandidates))
        {
            RestoreSplitQuantity(
                draggedItem,
                legacySingle);

            ShowVisual(
                draggedItem);

            return false;
        }

        if (legacyRemainder != null &&
            !legacyRemainder.IsDepleted())
        {
            RestoreSplitQuantity(
                draggedItem,
                legacyRemainder);
        }

        ShowVisual(
            draggedItem);

        return true;
    }



    private WorldItemDropContext BuildWorldItemDropContext()
    {
        ResolveDropReferences();
        GameObject requester =
            inventory != null
                ? inventory.gameObject
                : null;

        Vector2 origin =
            requester != null
                ? (Vector2)requester.transform.position
                : (Vector2)transform.position;

        return new WorldItemDropContext(
            requester,
            origin);
    }

    private bool TryCommitUiDropTransaction(
        InventorySlotUI target,
        ItemInstance working)
    {
        if (target == null ||
            working == null ||
            working.IsDepleted())
        {
            return false;
        }

        ItemInstance targetBefore =
            target.GetBoundItem();

        bool trueSwapExpected =
            WouldDisplaceExisting(
                targetBefore,
                working);

        // If this is a real swap, prove BEFORE mutating the target that the old
        // item has a safe non-world destination. Otherwise reject transaction.
        if (trueSwapExpected &&
            !CanResolveItemWithoutWorldDrop(
                targetBefore,
                sourceSlot))
        {
            LogWarning(
                $"EndDrag | swap rejected before mutation because displaced item has no safe destination | " +
                $"target={DescribeSlot(target)} | displaced={DescribeItem(targetBefore)}");

            return false;
        }

        if (!target.TryPlaceItem(
                working,
                out ItemInstance displacedOrRemainder))
        {
            LogWarning(
                $"EndDrag | target rejected item | target={DescribeSlot(target)} | item={DescribeItem(working)}");
            return false;
        }

        Log(
            $"EndDrag | target accepted item | target={DescribeSlot(target)} | " +
            $"displaced/remainder={DescribeItem(displacedOrRemainder)}");

        if (displacedOrRemainder == null ||
            displacedOrRemainder.IsDepleted())
        {
            Cleanup();
            return true;
        }

        // Partial stack/container placement returns the incoming item/remainder.
        // Resolve that remainder safely without interpreting it as a swap.
        if (ReferenceEquals(
                displacedOrRemainder,
                working))
        {
            draggedItem =
                displacedOrRemainder;

            if (TryResolveItemWithoutWorldDrop(
                    displacedOrRemainder,
                    sourceSlot))
            {
                Cleanup();
                return true;
            }

            KeepDragActive(
                displacedOrRemainder,
                "EndDrag | partial UI placement left a remainder with no safe destination.");

            return true;
        }

        // True swap: resolve the old target item without any world-drop fallback.
        if (TryResolveItemWithoutWorldDrop(
                displacedOrRemainder,
                sourceSlot))
        {
            Cleanup();
            return true;
        }

        LogWarning(
            "EndDrag | displaced item could not be resolved safely. Rolling back swap.");

        if (TryRollbackUiSwap(
                target,
                working,
                displacedOrRemainder,
                targetBefore))
        {
            if (TryResolveItemWithoutWorldDrop(
                    working,
                    sourceSlot))
            {
                Cleanup();
                return true;
            }

            KeepDragActive(
                working,
                "EndDrag | swap rolled back but dragged item could not be restored to storage.");

            return true;
        }

        // Extremely defensive fallback. Do not call Cleanup because that would
        // abandon authoritative item references.
        KeepDragActive(
            working,
            "EndDrag | swap rollback itself failed. Drag retained for recovery.");

        return true;
    }

    private bool TryRollbackUiSwap(
        InventorySlotUI target,
        ItemInstance working,
        ItemInstance displaced,
        ItemInstance targetBefore)
    {
        if (target == null ||
            working == null ||
            displaced == null)
        {
            return false;
        }

        ItemInstance current =
            target.GetBoundItem();

        if (!ReferenceEquals(
                current,
                working))
        {
            LogWarning(
                $"Rollback aborted because target no longer contains dragged item | " +
                $"targetNow={DescribeItem(current)} | working={DescribeItem(working)}");
            return false;
        }

        ItemInstance removedWorking =
            target.RemoveItem();

        if (!ReferenceEquals(
                removedWorking,
                working))
        {
            LogWarning(
                $"Rollback removed unexpected item | removed={DescribeItem(removedWorking)} | " +
                $"working={DescribeItem(working)}");
            return false;
        }

        if (!target.TryPlaceItem(
                displaced,
                out ItemInstance restoreRemainder) ||
            (restoreRemainder != null &&
             !restoreRemainder.IsDepleted()))
        {
            // Try to put the dragged item back where we found it so the failed
            // rollback does not make the situation worse.
            target.TryPlaceItem(
                removedWorking,
                out _);

            LogWarning(
                $"Rollback failed restoring original target item | " +
                $"expectedBefore={DescribeItem(targetBefore)} | displaced={DescribeItem(displaced)} | " +
                $"remainder={DescribeItem(restoreRemainder)}");

            return false;
        }

        draggedItem =
            removedWorking;

        return true;
    }

    private bool TryRollbackUnexpectedSinglePlacement(
        InventorySlotUI target,
        ItemInstance targetBefore,
        ItemInstance placedSingle,
        ItemInstance displacedOrRemainder)
    {
        if (target == null ||
            placedSingle == null)
        {
            return false;
        }

        ItemInstance targetAfter =
            target.GetBoundItem();

        // A container/stack may legally return the incoming object as an
        // unaccepted remainder while leaving the target object itself unchanged.
        // That is not a displaced target item, so no target rollback is needed.
        if (ReferenceEquals(
                targetAfter,
                targetBefore))
        {
            return true;
        }

        if (!ReferenceEquals(
                targetAfter,
                placedSingle))
        {
            return false;
        }

        ItemInstance removed =
            target.RemoveItem();

        if (!ReferenceEquals(
                removed,
                placedSingle))
        {
            return false;
        }

        if (targetBefore == null)
            return true;

        if (!ReferenceEquals(
                displacedOrRemainder,
                targetBefore))
        {
            // We know the target changed, but the returned object is not the item
            // that used to occupy it. Do not guess.
            target.TryPlaceItem(
                placedSingle,
                out _);
            return false;
        }

        if (!target.TryPlaceItem(
                targetBefore,
                out ItemInstance restoreRemainder) ||
            (restoreRemainder != null &&
             !restoreRemainder.IsDepleted()))
        {
            target.TryPlaceItem(
                placedSingle,
                out _);
            return false;
        }

        return true;
    }

    private bool CanAcceptWithoutDisplacingExisting(
        InventorySlotUI slot,
        ItemInstance incoming)
    {
        if (slot == null ||
            incoming == null ||
            incoming.IsDepleted())
        {
            return false;
        }

        if (!slot.CanAcceptPreview(
                incoming))
        {
            return false;
        }

        ItemInstance existing =
            slot.GetBoundItem();

        if (existing == null)
            return true;

        if (ReferenceEquals(
                existing,
                incoming))
        {
            return true;
        }

        if (existing.IsContainer &&
            !incoming.IsContainer)
        {
            return true;
        }

        if (existing.CanStackWith(
                incoming) &&
            existing.RemainingStackSpace > 0)
        {
            return true;
        }

        return false;
    }

    private static bool WouldDisplaceExisting(
        ItemInstance existing,
        ItemInstance incoming)
    {
        if (existing == null ||
            incoming == null ||
            ReferenceEquals(existing, incoming))
        {
            return false;
        }

        if (existing.IsContainer &&
            !incoming.IsContainer)
        {
            return false;
        }

        if (existing.CanStackWith(
                incoming) &&
            existing.RemainingStackSpace > 0)
        {
            return false;
        }

        return true;
    }

    private bool CanResolveItemWithoutWorldDrop(
        ItemInstance item,
        InventorySlotUI preferredSlot)
    {
        if (item == null ||
            item.IsDepleted())
        {
            return true;
        }

        if (preferredSlot != null &&
            CanAcceptWithoutDisplacingExisting(
                preferredSlot,
                item))
        {
            return true;
        }

        return
            inventory != null &&
            inventory.CanFullyAdd(item);
    }

    private bool TryResolveItemWithoutWorldDrop(
        ItemInstance item,
        InventorySlotUI preferredSlot)
    {
        if (item == null ||
            item.IsDepleted())
        {
            return true;
        }

        ItemInstance unresolved =
            item;

        if (preferredSlot != null &&
            CanAcceptWithoutDisplacingExisting(
                preferredSlot,
                unresolved))
        {
            if (preferredSlot.TryPlaceItem(
                    unresolved,
                    out ItemInstance remainder))
            {
                if (remainder == null ||
                    remainder.IsDepleted())
                {
                    return true;
                }

                unresolved =
                    remainder;
            }
        }

        if (unresolved == null ||
            unresolved.IsDepleted())
        {
            return true;
        }

        if (inventory != null &&
            inventory.CanFullyAdd(
                unresolved) &&
            inventory.TryAddInstance(
                unresolved))
        {
            return true;
        }

        return false;
    }

    private void ResolveInterruptedDragWithoutWorldDrop(
        string reason)
    {
        if (_resolvingLifecycleDrag)
            return;

        if (!isDragging &&
            draggedItem == null)
        {
            return;
        }

        _resolvingLifecycleDrag =
            true;

        try
        {
            LogWarning(
                $"{reason} | terminating transient drag safely | " +
                $"item={DescribeItem(draggedItem)} | source={DescribeSlot(sourceSlot)} | " +
                $"reservation={_reservationBackedDrag}");

            if (_reservationBackedDrag ||
                _draggingDeployedSounder)
            {
                // Reservation-backed inventory and the deployed sounder never
                // surrendered authoritative ownership.
                Cleanup();
                return;
            }

            if (draggedItem == null ||
                draggedItem.IsDepleted())
            {
                Cleanup();
                return;
            }

            if (TryResolveItemWithoutWorldDrop(
                    draggedItem,
                    sourceSlot))
            {
                Cleanup();
                return;
            }

            KeepDragActive(
                draggedItem,
                $"{reason} | legacy detached drag had no safe storage destination.");
        }
        finally
        {
            _resolvingLifecycleDrag =
                false;
        }
    }


    private bool IsLikelyLifecycleInterruptedEndDrag()
    {
        if (_applicationQuitting)
            return true;

        // A real pointer-release EndDrag occurs after the primary button is up.
        // If Unity is ending the drag while it remains held, UI teardown is the
        // overwhelmingly likely cause.
        if (Input.GetMouseButton(0))
            return true;

        if (sourceSlot == null)
            return true;

        if (!sourceSlot.isActiveAndEnabled ||
            !sourceSlot.gameObject.activeInHierarchy)
        {
            return true;
        }

        if (canvas == null ||
            !canvas.isActiveAndEnabled ||
            !canvas.gameObject.activeInHierarchy)
        {
            return true;
        }

        return false;
    }

    private void KeepDragActive(
        ItemInstance item,
        string reason)
    {
        ClearReservationMetadata();

        draggedItem =
            item;

        isDragging =
            item != null &&
            !item.IsDepleted();

        if (isDragging)
        {
            ShowVisual(
                draggedItem);
        }
        else
        {
            HideVisual();
        }

        sourceSlot?.Refresh();

        LogWarning(
            $"{reason} Keeping detached item authoritative in drag state | item={DescribeItem(draggedItem)}");
    }


    private static void RestoreSplitQuantity(
        ItemInstance destinationStack,
        ItemInstance splitRemainder)
    {
        if (destinationStack == null ||
            splitRemainder == null ||
            splitRemainder.IsDepleted())
        {
            return;
        }

        if (!destinationStack.CanStackWith(
                splitRemainder))
        {
            return;
        }

        int amount =
            splitRemainder.Quantity;

        int restored =
            destinationStack.AddQuantity(
                amount);

        splitRemainder.RemoveQuantity(
            restored);
    }

    private void RefreshDragPreviewsIfChanged()
    {
        ItemInstance previewItem =
            isDragging
                ? draggedItem
                : null;

        int quantity =
            previewItem != null
                ? previewItem.Quantity
                : int.MinValue;

        if (ReferenceEquals(
                _lastPreviewItem,
                previewItem) &&
            _lastPreviewQuantity == quantity)
        {
            return;
        }

        _lastPreviewItem =
            previewItem;

        _lastPreviewQuantity =
            quantity;

        playerInventoryUI?.RefreshDragPreview(
            previewItem);

        loadoutOverlayUI?.RefreshDragPreview(
            previewItem);
    }

    private void Log(string msg)
    {
        if (!verboseLogging) return;
        Debug.Log($"[InventoryDragController:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging) return;
        Debug.LogWarning($"[InventoryDragController:{name}] {msg}", this);
    }
}
