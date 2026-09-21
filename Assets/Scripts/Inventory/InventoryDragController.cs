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
    [SerializeField] private bool verboseLogging = true;

    [System.NonSerialized] private ItemInstance draggedItem;
    private InventorySlotUI sourceSlot;
    private bool isDragging;

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

    public bool IsDragging => isDragging;
    public ItemInstance DraggedItem => draggedItem;

    private void Awake()
    {
        if (displacedItemResolver == null)
            displacedItemResolver = GetComponentInParent<DisplacedItemResolver>(true);

        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>(true);

        if (inventoryInput == null)
            inventoryInput = GetComponentInParent<PlayerInventoryInput>(true);

        if (worldCamera == null)
            worldCamera = Camera.main;

        HideVisual();
        Log($"Awake | canvas={(canvas != null ? canvas.name : "NULL")} | dragIcon={(dragIcon != null ? dragIcon.name : "NULL")}");
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

        if (isDragging)
        {
            playerInventoryUI?.RefreshDragPreview(draggedItem);
            loadoutOverlayUI?.RefreshDragPreview(draggedItem);
        }
        else
        {
            playerInventoryUI?.RefreshDragPreview(null);
            loadoutOverlayUI?.RefreshDragPreview(null);
        }

        if (Input.GetMouseButtonDown(1))
        {
            InventorySlotUI target = FindTargetSlotUnderMouse();

            if (target != null)
            {
                bool deposited = TryDepositSingleInto(target);
                if (deposited)
                    target.Refresh();
            }
            else
            {
                TryDropSingleToWorld();
            }
        }
    }

    public void BeginDrag(InventorySlotUI slot)
    {
        if (slot == null)
        {
            LogWarning("BeginDrag ignored because slot was null.");
            return;
        }

        if (TryBeginDeployedSounderDrag(
                slot))
        {
            return;
        }

        ItemInstance item = slot.RemoveItem();
        if (item == null || item.Definition == null)
        {
            LogWarning($"BeginDrag failed | source={DescribeSlot(slot)} | removed item was null/invalid");
            return;
        }

        draggedItem = item;
        sourceSlot = slot;
        isDragging = true;

        ShowVisual(item);

        Log($"BeginDrag | source={DescribeSlot(sourceSlot)} | item={DescribeItem(draggedItem)}");
    }

    public void BeginDrag(ItemInstance item, InventorySlotUI slot)
    {
        if (item == null || item.Definition == null)
        {
            LogWarning("BeginDrag(item, slot) failed because item was null/invalid.");
            return;
        }

        if (isDragging)
        {
            LogWarning("BeginDrag(item, slot) ignored because drag is already active.");
            return;
        }

        draggedItem = item;
        sourceSlot = slot;
        isDragging = true;

        ShowVisual(item);

        Log($"BeginDrag(item, slot) | source={DescribeSlot(sourceSlot)} | item={DescribeItem(draggedItem)}");
    }

    public void EndDrag()
    {
        if (!isDragging)
        {
            Log("EndDrag ignored because not dragging.");
            return;
        }

        InventorySlotUI target = FindTargetSlotUnderMouse();
        ItemInstance working = draggedItem;

        Log($"EndDrag BEGIN | source={DescribeSlot(sourceSlot)} | target={DescribeSlot(target)} | item={DescribeItem(working)}");

        if (_draggingDeployedSounder)
        {
            // Dropping it back onto its own Hands slot is just a cancelled drag.
            if (target == sourceSlot)
            {
                Cleanup();
                return;
            }

            if (_deployedSounderController != null &&
                _deployedSounderController.IsActiveDeployedSounder(
                    working) &&
                _deployedSounderController.TryReleaseDeployedSounderToWorld(
                    working,
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

        if (target != null)
        {
            if (target.TryPlaceItem(working, out ItemInstance displaced))
            {
                Log($"EndDrag | target accepted item | target={DescribeSlot(target)} | displaced={DescribeItem(displaced)}");

                if (displaced == null || displaced.IsDepleted())
                {
                    Cleanup();
                    return;
                }

                bool resolved = false;

                if (sourceSlot != null && sourceSlot != target)
                {
                    if (sourceSlot.TryPlaceItem(displaced, out ItemInstance returned))
                    {
                        Log($"EndDrag | source accepted displaced item back | source={DescribeSlot(sourceSlot)} | returned={DescribeItem(returned)}");

                        if (returned == null || returned.IsDepleted())
                        {
                            Cleanup();
                            return;
                        }

                        displaced = returned;
                    }
                }

                if (displacedItemResolver != null)
                    resolved = displacedItemResolver.TryResolve(displaced, sourceSlot);

                if (resolved)
                {
                    Cleanup();
                    return;
                }

                // Full rollback if displaced item could not be resolved anywhere.
                LogWarning("EndDrag | displaced item could not be resolved. Rolling back swap.");

                ItemInstance rolledBackWorking = target.RemoveItem();
                if (rolledBackWorking == null)
                    rolledBackWorking = working;

                bool targetRestored = target.TryPlaceItem(displaced, out ItemInstance targetRestoreRemainder);
                bool sourceRestored = sourceSlot != null && sourceSlot.TryPlaceItem(rolledBackWorking, out ItemInstance sourceRestoreRemainder);

                //LogWarning($"EndDrag rollback | targetRestored={targetRestored} remainder={DescribeItem(targetRestoreRemainder)} | sourceRestored={sourceRestored} remainder={DescribeItem(sourceRestoreRemainder)}");

                Cleanup();
                return;
            }
            else
            {
                LogWarning($"EndDrag | target rejected item | target={DescribeSlot(target)} | item={DescribeItem(working)}");
                target.PlayInvalidTargetFeedback();
            }
        }
        else
        {
            Log("EndDrag | no UI target under mouse, trying world container target.");

            if (TryDepositDraggedItemIntoWorldTarget(working, out ItemInstance remainder))
            {
                if (remainder == null || remainder.IsDepleted())
                {
                    Cleanup();
                    return;
                }

                // A world target may intentionally accept only part of a stack.
                // The dragged stack originated from sourceSlot, which is normally
                // empty for the duration of the drag, so return the remainder there
                // automatically instead of leaving a stranded partial stack attached
                // to the cursor after the mouse button has already been released.
                if (TryResolvePartialWorldDepositRemainder(remainder))
                {
                    Cleanup();
                    return;
                }

                // Absolute safety fallback: never destroy or silently lose an item if
                // the original slot disappeared and the normal displaced-item resolver
                // also has nowhere legal to put it. Keep the remainder in the active
                // drag only in that exceptional case.
                ShowVisual(draggedItem);
                LogWarning(
                    $"EndDrag | partial world deposit succeeded, but remainder could not be auto-resolved; " +
                    $"keeping drag active | remainder={DescribeItem(draggedItem)}");
                return;
            }

            Log("EndDrag | no valid world container target, dropping dragged item into world.");

            if (TryDropItemToWorld(working))
            {
                Cleanup();
                return;
            }

            LogWarning("EndDrag | world drop failed, returning item to source.");
        }

        if (sourceSlot != null)
        {
            sourceSlot.TryPlaceItem(working, out _);
            Log($"EndDrag | returned item to source | source={DescribeSlot(sourceSlot)} | item={DescribeItem(working)}");
        }

        Cleanup();
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
            dragIcon.sprite = hasItem ? item.Definition.Icon : null;
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
        Log($"Cleanup | item={DescribeItem(draggedItem)} | source={DescribeSlot(sourceSlot)}");

        draggedItem = null;
        sourceSlot = null;
        isDragging = false;

        _draggingDeployedSounder =
            false;

        _deployedSounderController =
            null;

        HideVisual();

        playerInventoryUI?.RefreshDragPreview(null);
        loadoutOverlayUI?.RefreshDragPreview(null);
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

    public bool TryDepositSingleInto(InventorySlotUI target)
    {
        if (!isDragging || draggedItem == null || target == null)
            return false;

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

        if (draggedItem.Quantity <= 0)
            return false;

        if (draggedItem.Quantity == 1)
        {
            if (target.TryPlaceItem(draggedItem, out ItemInstance displaced))
            {
                if (displaced != null && !displaced.IsDepleted())
                {
                    LogWarning("TryDepositSingleInto | target displaced item unexpectedly during single final place.");
                    return false;
                }

                Cleanup();
                return true;
            }

            return false;
        }

        ItemInstance singleToPlace = draggedItem.SplitOff(1);
        if (singleToPlace == null)
            return false;

        if (target.TryPlaceItem(singleToPlace, out ItemInstance displacedRemainder))
        {
            if (displacedRemainder != null && !displacedRemainder.IsDepleted())
            {
                draggedItem.AddQuantity(1);
                return false;
            }

            ShowVisual(draggedItem);
            return true;
        }

        draggedItem.AddQuantity(1);
        return false;
    }

    public void CancelDrag()
    {
        if (!isDragging && draggedItem == null)
        {
            HideVisual();
            playerInventoryUI?.RefreshDragPreview(null);
            return;
        }

        Log($"CancelDrag | item={DescribeItem(draggedItem)} | source={DescribeSlot(sourceSlot)}");

        if (_draggingDeployedSounder)
        {
            // We never removed it from Hands, so cancelling needs no inventory rollback.
            Cleanup();
            return;
        }

        bool restored = false;

        if (draggedItem != null && sourceSlot != null && sourceSlot.isActiveAndEnabled && sourceSlot.gameObject.activeInHierarchy)
        {
            if (sourceSlot.TryPlaceItem(draggedItem, out ItemInstance displaced))
            {
                if (displaced == null || displaced.IsDepleted())
                    restored = true;
            }
        }

        if (!restored && draggedItem != null && displacedItemResolver != null)
            restored = displacedItemResolver.TryResolve(draggedItem, sourceSlot);

        if (!restored && draggedItem != null)
            LogWarning($"CancelDrag | failed to restore dragged item cleanly | item={DescribeItem(draggedItem)}");

        Cleanup();
    }

    private bool TryDropItemToWorld(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        if (inventory == null)
        {
            LogWarning($"TryDropItemToWorld failed because inventory is null | item={DescribeItem(item)}");
            return false;
        }

        Vector3 worldPos = inventoryInput != null
            ? inventoryInput.GetDropWorldPositionForUI()
            : transform.position;

        bool ok = inventory.TryDropInstance(item, worldPos);
        Log($"TryDropItemToWorld | item={DescribeItem(item)} | pos={worldPos} | ok={ok}");
        return ok;
    }

    private bool TryDropSingleToWorld()
    {
        if (!isDragging || draggedItem == null)
            return false;

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

        if (draggedItem.Quantity == 1)
        {
            if (TryDropItemToWorld(draggedItem))
            {
                Cleanup();
                return true;
            }

            return false;
        }

        ItemInstance single = draggedItem.SplitOff(1);
        if (single == null)
            return false;

        if (TryDropItemToWorld(single))
        {
            ShowVisual(draggedItem);
            return true;
        }

        draggedItem.AddQuantity(1);
        return false;
    }

    private int CollectWorldDropTargetCandidates(ItemInstance item)
    {
        _worldDropCandidates.Clear();

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
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

    private bool TryDepositDraggedItemIntoWorldTarget(ItemInstance item, out ItemInstance remainder)
    {
        remainder = item;

        if (item == null)
            return false;

        int candidateCount =
            CollectWorldDropTargetCandidates(
                item);

        if (candidateCount <= 0)
            return false;

        for (int i = 0; i < candidateCount; i++)
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

            if (!target.CanAcceptWorldDrop(item))
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

    /// <summary>
    /// Resolves the unaccepted portion of a partial world-target deposit.
    /// Prefer the exact slot the drag originated from; if that is no longer a
    /// legal destination, fall back to the project's existing displaced-item
    /// resolver. Returns true only when the remainder is fully accounted for.
    /// </summary>
    private bool TryResolvePartialWorldDepositRemainder(ItemInstance remainder)
    {
        if (remainder == null || remainder.IsDepleted())
            return true;

        ItemInstance unresolved = remainder;

        if (sourceSlot != null)
        {
            if (sourceSlot.TryPlaceItem(unresolved, out ItemInstance returned))
            {
                Log(
                    $"Partial world deposit | returned remainder to source | " +
                    $"source={DescribeSlot(sourceSlot)} | returned={DescribeItem(returned)}");

                if (returned == null || returned.IsDepleted())
                    return true;

                unresolved = returned;
            }
        }

        if (displacedItemResolver != null &&
            displacedItemResolver.TryResolve(unresolved, sourceSlot))
        {
            Log(
                $"Partial world deposit | displaced-item resolver accepted remainder | " +
                $"item={DescribeItem(unresolved)}");

            return true;
        }

        draggedItem = unresolved;
        return false;
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