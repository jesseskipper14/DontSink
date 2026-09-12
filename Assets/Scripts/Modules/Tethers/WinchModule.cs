using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InstalledModule))]
public sealed class WinchModule : MonoBehaviour, IInstalledModuleLifecycle, IInstalledModuleRemovalGuard, IInstalledModuleAdditionalMass
{
    [Header("Rigging")]
    [SerializeField] private TetherLineCatalog lineCatalog;
    [SerializeField, Min(1)] private int lineSlotCount = 1;

    [Header("Winch")]
    [SerializeField, Min(0.01f)] private float reelSpeedMetersPerSecond = 1.5f;
    [SerializeField, Min(0f)] private float ratedPullNewtons = 5000f;
    [SerializeField, Min(0.01f)] private float minimumDeployedLength = 0.2f;
    [SerializeField, Min(0.01f)] private float dockingDistance = 0.45f;
    [SerializeField, Min(0f)] private float quickReleaseSlackMeters = 0.25f;

    [Header("Runtime")]
    [SerializeField] private ItemContainerState lineContainer;
    [SerializeField] private WinchCommand command = WinchCommand.Stop;
    [SerializeField] private float deployedLength;

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private ItemDefinition debugLineItem;
#endif

    private InstalledModule _installedModule;
    private Hardpoint _ownerHardpoint;
    private TetherWinchLink _link;
    private TetherConstraint2D _constraint;
    private TetherDeploymentModule _deployment;
    private TetherPayload _payload;
    private ItemContainerState _subscribedLineContainer;

    private const float SegmentBoundaryEpsilonMeters = 0.001f;

    private struct CutLineLossPlanEntry
    {
        public int SlotIndex;
        public ItemInstance ExpectedInstance;
        public int QuantityToRemove;
        public float SegmentLengthMeters;
    }

    public ItemContainerState LineContainer
    {
        get
        {
            EnsureLineContainer();
            return lineContainer;
        }
    }
    public int LineSlotCount => Mathf.Max(1, lineSlotCount);
    public WinchCommand Command => command;
    public float DeployedLength => deployedLength;
    public float AvailableLineMeters => CalculateAvailableLine(out _, out _);
    public float WorkingLoadNewtons
    {
        get
        {
            CalculateAvailableLine(out float working, out _);
            return working;
        }
    }
    public float BreakingLoadNewtons
    {
        get
        {
            CalculateAvailableLine(out _, out float breaking);
            return breaking;
        }
    }
    public float CurrentTension => _constraint != null ? _constraint.CurrentTension : 0f;
    public float CurrentDistance => _constraint != null ? _constraint.CurrentDistance : 0f;
    public bool IsOverWorkingLoad => _constraint != null && _constraint.IsOverWorkingLoad;
    public bool IsOverBreakingLoad => _constraint != null && _constraint.IsOverBreakingLoad;
    public bool HasDeployedPayload => _deployment != null && _deployment.HasDeployedPayload;

    private void Awake()
    {
        CacheRefs();
        EnsureLineContainer();
    }

    private void OnDestroy()
    {
        UnbindLineContainer();
    }

    private void FixedUpdate()
    {
        RefreshDeploymentRuntimeRefs();

        if (_deployment == null ||
            !_deployment.HasDeployedPayload ||
            _payload == null ||
            _constraint == null)
        {
            if (command != WinchCommand.Stop)
                command = WinchCommand.Stop;

            return;
        }

        float available = AvailableLineMeters;
        if (available <= 0f)
        {
            _deployment.EndPayloadRetrieval();
            command = WinchCommand.Stop;
            return;
        }

        switch (command)
        {
            case WinchCommand.Lower:
                _deployment.EndPayloadRetrieval();

                deployedLength = Mathf.Min(
                    available,
                    deployedLength + reelSpeedMetersPerSecond * Time.fixedDeltaTime);
                break;

            case WinchCommand.Raise:
                // Raise is authoritative retrieval intent for the entire time
                // this command remains active. Reasserting it each physics step
                // makes the behavior deterministic even if another caller touched
                // retrieval state between command changes.
                _deployment.BeginPayloadRetrieval();

                float speedScale =
                    ratedPullNewtons <= 0f ||
                    CurrentTension <= ratedPullNewtons
                        ? 1f
                        : 0f;

                deployedLength = Mathf.Max(
                    minimumDeployedLength,
                    deployedLength -
                    reelSpeedMetersPerSecond *
                    speedScale *
                    Time.fixedDeltaTime);

                TryAutoDock();
                break;

            case WinchCommand.QuickRelease:
                _deployment.EndPayloadRetrieval();

                float target = Mathf.Min(
                    available,
                    Mathf.Max(
                        deployedLength,
                        CurrentDistance + quickReleaseSlackMeters));

                deployedLength = target;
                break;
        }

        if (_constraint != null &&
            _deployment != null &&
            _deployment.HasDeployedPayload)
        {
            _constraint.SetDeployedLength(deployedLength);
            _constraint.SetLineRatings(
                WorkingLoadNewtons,
                BreakingLoadNewtons);
        }
    }

    public void OnInstalled(Hardpoint hardpoint)
    {
        _ownerHardpoint = hardpoint;
        CacheRefs();
        EnsureLineContainer();
    }

    public void OnRemoved()
    {
        _deployment?.EndPayloadRetrieval();

        command = WinchCommand.Stop;
        _deployment = null;
        _payload = null;
        _constraint = null;
    }

    public bool TryLower()
    {
        if (!EnsureDeploymentBound())
            return false;

        float available = AvailableLineMeters;
        if (available <= 0f)
        {
            Debug.LogWarning(
                $"[Winch:{name}] Cannot lower: no valid tether line is loaded.",
                this);

            return false;
        }

        if (!_deployment.HasDeployedPayload)
        {
            if (!_deployment.HasStoredPayload)
            {
                Debug.LogWarning(
                    $"[Winch:{name}] Cannot lower: linked tether deployment module has no stored payload.",
                    this);

                return false;
            }

            if (!_deployment.TryDeployStoredPayload(out TetherPayload payload) ||
                payload == null)
            {
                return false;
            }

            _payload = payload;
            _constraint = _deployment.TetherConstraint;

            if (_constraint == null ||
                !_constraint.IsAttached)
            {
                Debug.LogWarning(
                    $"[Winch:{name}] Payload deployed, but its TetherConstraint2D is not attached.",
                    this);

                return false;
            }

            // TetherDeploymentModule creates the physical payload and performs
            // the initial bind. The winch now becomes authoritative for payout.
            deployedLength = Mathf.Clamp(
                Mathf.Max(
                    minimumDeployedLength,
                    _constraint.DeployedLength),
                minimumDeployedLength,
                available);

            _constraint.SetDeployedLength(deployedLength);
            _constraint.SetLineRatings(
                WorkingLoadNewtons,
                BreakingLoadNewtons);
        }
        else
        {
            RefreshDeploymentRuntimeRefs();

            if (_payload == null ||
                _constraint == null ||
                !_constraint.IsAttached)
            {
                Debug.LogWarning(
                    $"[Winch:{name}] Linked deployment reports a deployed payload, " +
                    "but the payload/constraint could not be resolved.",
                    this);

                return false;
            }

            // If something else adjusted the constraint while stopped,
            // resume from the actual current deployed length.
            deployedLength = Mathf.Clamp(
                _constraint.DeployedLength,
                minimumDeployedLength,
                available);
        }

        command = WinchCommand.Lower;
        return true;
    }

    public bool TryRaise()
    {
        if (!EnsureDeploymentBound())
            return false;

        RefreshDeploymentRuntimeRefs();

        if (!_deployment.HasDeployedPayload ||
            _payload == null)
        {
            command = WinchCommand.Stop;
            return true;
        }

        if (_constraint == null ||
            !_constraint.IsAttached)
        {
            Debug.LogWarning(
                $"[Winch:{name}] Cannot raise: deployed payload has no attached TetherConstraint2D.",
                this);

            return false;
        }

        float available = AvailableLineMeters;
        if (available <= 0f)
        {
            Debug.LogWarning(
                $"[Winch:{name}] Cannot raise: no valid tether line is loaded.",
                this);

            return false;
        }

        deployedLength = Mathf.Clamp(
            _constraint.DeployedLength,
            minimumDeployedLength,
            available);

        // Raising is an explicit player intent to dislodge/retrieve the payload.
        // Anchors use this signal to release their world-space hold before line
        // length begins decreasing.
        _deployment.BeginPayloadRetrieval();

        command = WinchCommand.Raise;
        return true;
    }

    public void Stop()
    {
        _deployment?.EndPayloadRetrieval();
        command = WinchCommand.Stop;
    }

    public bool QuickRelease()
    {
        if (!EnsureDeploymentBound())
            return false;

        if (!_deployment.HasDeployedPayload &&
            !TryLower())
        {
            return false;
        }

        RefreshDeploymentRuntimeRefs();

        if (_payload == null ||
            _constraint == null)
        {
            return false;
        }

        _deployment.EndPayloadRetrieval();

        command = WinchCommand.QuickRelease;
        return true;
    }

    public bool CutLine()
    {
        if (!EnsureDeploymentBound())
            return false;

        RefreshDeploymentRuntimeRefs();

        if (_deployment == null ||
            !_deployment.HasDeployedPayload)
        {
            command =
                WinchCommand.Stop;

            return false;
        }

        float constraintLength =
            _constraint != null
                ? _constraint.DeployedLength
                : 0f;

        float cutLength =
            Mathf.Max(
                minimumDeployedLength,
                Mathf.Max(
                    deployedLength,
                    constraintLength));

        if (!TryBuildCutLineLossPlan(
                cutLength,
                out List<CutLineLossPlanEntry> lossPlan,
                out float coveredLength))
        {
            Debug.LogWarning(
                $"[Winch:{name}] Cannot cut line safely: deployed length is {cutLength:F2} m, " +
                $"but valid loaded line only covers {coveredLength:F2} m in slot order. " +
                "No payload or line state was changed.",
                this);

            return false;
        }

        _deployment.EndPayloadRetrieval();

        if (!_deployment.TryCutLooseDeployedPayload(
                out WorldItem releasedWorldItem) ||
            releasedWorldItem == null)
        {
            return false;
        }

        ApplyCutLineLossPlan(lossPlan);

        command =
            WinchCommand.Stop;

        deployedLength =
            0f;

        _payload =
            null;

        _constraint =
            _deployment.TetherConstraint;

        return true;
    }

    private bool TryBuildCutLineLossPlan(
        float cutLengthMeters,
        out List<CutLineLossPlanEntry> plan,
        out float coveredLengthMeters)
    {
        EnsureLineContainer();

        plan =
            new List<CutLineLossPlanEntry>();

        coveredLengthMeters =
            0f;

        if (lineCatalog == null ||
            lineContainer == null ||
            cutLengthMeters <= 0f)
        {
            return false;
        }

        float remaining =
            cutLengthMeters;

        for (int i = 0;
             i < lineContainer.SlotCount &&
             remaining > SegmentBoundaryEpsilonMeters;
             i++)
        {
            InventorySlot slot =
                lineContainer.GetSlot(i);

            if (slot == null ||
                slot.IsEmpty ||
                slot.Instance == null ||
                slot.Instance.Definition == null)
            {
                continue;
            }

            ItemInstance instance =
                slot.Instance;

            if (!lineCatalog.TryGet(
                    instance.Definition,
                    out TetherLineCatalog.Entry entry) ||
                entry == null)
            {
                continue;
            }

            float segmentLength =
                Mathf.Max(0f, entry.LengthMeters);

            if (segmentLength <= 0f)
                continue;

            int availableQuantity =
                Mathf.Max(0, instance.Quantity);

            int quantityToRemove = 0;

            while (quantityToRemove < availableQuantity &&
                   remaining > SegmentBoundaryEpsilonMeters)
            {
                quantityToRemove++;
                coveredLengthMeters += segmentLength;
                remaining -= segmentLength;
            }

            if (quantityToRemove <= 0)
                continue;

            plan.Add(
                new CutLineLossPlanEntry
                {
                    SlotIndex = i,
                    ExpectedInstance = instance,
                    QuantityToRemove = quantityToRemove,
                    SegmentLengthMeters = segmentLength
                });
        }

        return remaining <= SegmentBoundaryEpsilonMeters;
    }

    private void ApplyCutLineLossPlan(
        List<CutLineLossPlanEntry> plan)
    {
        if (plan == null ||
            plan.Count == 0 ||
            lineContainer == null)
        {
            return;
        }

        float lostLength = 0f;
        int lostSegments = 0;

        for (int i = 0; i < plan.Count; i++)
        {
            CutLineLossPlanEntry entry = plan[i];
            InventorySlot slot = lineContainer.GetSlot(entry.SlotIndex);

            if (slot == null ||
                slot.IsEmpty ||
                slot.Instance == null ||
                !ReferenceEquals(slot.Instance, entry.ExpectedInstance))
            {
                Debug.LogError(
                    $"[Winch:{name}] Cut-line loss plan changed unexpectedly at slot {entry.SlotIndex}. " +
                    "The payload is already released, so this slot was left untouched.",
                    this);
                continue;
            }

            ItemInstance instance = slot.Instance;
            int quantityToRemove =
                Mathf.Min(entry.QuantityToRemove, Mathf.Max(0, instance.Quantity));

            if (quantityToRemove <= 0)
                continue;

            instance.RemoveQuantity(quantityToRemove);
            lostSegments += quantityToRemove;
            lostLength += entry.SegmentLengthMeters * quantityToRemove;

            if (instance.IsDepleted())
                slot.Clear();
        }

        lineContainer.NotifyChanged();
        _installedModule?.RefreshMassFromDefinitionAndContents();

        Debug.Log(
            $"[Winch:{name}] Cut line released payload and sacrificed {lostSegments} loaded segment(s) " +
            $"covering {lostLength:F2} m in sequential slot order.",
            this);
    }

    public bool CanRemoveInstalledModule(out string reason)
    {
        RefreshDeploymentRuntimeRefs();

        if (_deployment != null &&
            _deployment.HasDeployedPayload)
        {
            reason = "Winch still has a deployed payload. Retrieve/stow it before removing the winch.";
            return false;
        }

        if (HasAnyLineLoaded())
        {
            reason = "Winch still contains tether line. Empty its line slots before removing the winch.";
            return false;
        }

        reason = null;
        return true;
    }

    public void EnsureLineContainer()
    {
        int slots = Mathf.Max(1, lineSlotCount);

        if (lineContainer == null)
            lineContainer = new ItemContainerState(slots, slots);
        else
            lineContainer.EnsureLayout(slots, slots);

        BindLineContainer();
    }

    public bool CanAcceptLine(ItemInstance item)
    {
        if (item == null || item.Definition == null || lineCatalog == null)
            return false;

        return lineCatalog.TryGet(item.Definition, out _);
    }

    public float AdditionalInstalledMass => LineContentsMass;

    public float LineContentsMass
    {
        get
        {
            EnsureLineContainer();
            float total = 0f;

            for (int i = 0; i < lineContainer.SlotCount; i++)
            {
                InventorySlot slot = lineContainer.GetSlot(i);
                if (slot == null || slot.IsEmpty || slot.Instance == null)
                    continue;

                total += slot.Instance.TotalMass;
            }

            return Mathf.Max(0f, total);
        }
    }

    public ItemContainerSnapshot CaptureLineContainerSnapshot()
    {
        EnsureLineContainer();
        return lineContainer != null ? lineContainer.ToSnapshot() : null;
    }

    public void RestoreLineContainerSnapshot(ItemContainerSnapshot snapshot, IItemDefinitionResolver resolver)
    {
        if (snapshot != null)
            lineContainer = ItemContainerState.FromSnapshot(snapshot, resolver);
        else
            lineContainer = new ItemContainerState(LineSlotCount, LineSlotCount);

        EnsureLineContainer();
        lineContainer?.NotifyChanged();
    }

    public void RestorePersistentTetherRuntime(
        float restoredDeployedLengthMeters)
    {
        command =
            WinchCommand.Stop;

        CacheRefs();
        RefreshDeploymentRuntimeRefs();

        if (_deployment == null ||
            !_deployment.HasDeployedPayload ||
            _constraint == null)
        {
            deployedLength =
                0f;

            return;
        }

        float restored =
            Mathf.Max(
                minimumDeployedLength,
                restoredDeployedLengthMeters);

        float available =
            AvailableLineMeters;

        if (available > 0f)
        {
            restored =
                Mathf.Min(
                    available,
                    restored);
        }

        deployedLength =
            restored;

        _deployment.EndPayloadRetrieval();

        _constraint.SetDeployedLength(
            deployedLength);

        _constraint.SetLineRatings(
            WorkingLoadNewtons,
            BreakingLoadNewtons);
    }

    private void CacheRefs()
    {
        if (_installedModule == null)
            _installedModule = GetComponent<InstalledModule>();

        if (_ownerHardpoint == null &&
            _installedModule != null)
        {
            _ownerHardpoint =
                _installedModule.OwnerHardpoint;
        }

        if (_ownerHardpoint != null)
        {
            if (_link == null)
            {
                _link =
                    _ownerHardpoint.GetComponent<TetherWinchLink>();
            }

        }

        RefreshDeploymentRuntimeRefs();
    }

    private bool EnsureDeploymentBound()
    {
        CacheRefs();

        if (_link == null)
        {
            Debug.LogWarning(
                $"[Winch:{name}] No TetherWinchLink exists on owner hardpoint.",
                this);

            return false;
        }

        if (!_link.TryGetDeploymentModule(
                out TetherDeploymentModule deployment) ||
            deployment == null)
        {
            Debug.LogWarning(
                $"[Winch:{name}] Linked hardpoint has no TetherDeploymentModule installed.",
                this);

            _deployment = null;
            _payload = null;
            _constraint = null;
            return false;
        }

        _deployment = deployment;
        _constraint = _deployment.TetherConstraint;
        _payload = _deployment.DeployedPayload;

        if (_constraint == null)
        {
            Debug.LogWarning(
                $"[Winch:{name}] Linked TetherDeploymentModule has no TetherConstraint2D.",
                this);

            return false;
        }

        return true;
    }

    private void RefreshDeploymentRuntimeRefs()
    {
        if (_link != null &&
            _link.TryGetDeploymentModule(
                out TetherDeploymentModule linkedDeployment) &&
            linkedDeployment != null)
        {
            _deployment = linkedDeployment;
        }

        if (_deployment == null)
        {
            _payload = null;
            _constraint = null;
            return;
        }

        _constraint =
            _deployment.TetherConstraint;

        _payload =
            _deployment.DeployedPayload;
    }

    private void TryAutoDock()
    {
        if (_deployment == null ||
            !_deployment.HasDeployedPayload ||
            _payload == null)
        {
            return;
        }

        WorldItem deployedWorldItem =
            _deployment.DeployedWorldItem;

        if (deployedWorldItem == null)
            return;

        float distanceToDock =
            Vector2.Distance(
                deployedWorldItem.transform.position,
                _deployment.PayloadHangPoint.position);

        if (distanceToDock > dockingDistance)
            return;

        if (!_deployment.TryRecallDeployedPayload())
            return;

        command = WinchCommand.Stop;
        deployedLength = 0f;

        _payload = null;
        _constraint = _deployment.TetherConstraint;
    }

    public void NotifyLineContainerChanged()
    {
        lineContainer?.NotifyChanged();
    }

    private void BindLineContainer()
    {
        if (ReferenceEquals(_subscribedLineContainer, lineContainer))
            return;

        UnbindLineContainer();
        _subscribedLineContainer = lineContainer;

        if (_subscribedLineContainer != null)
            _subscribedLineContainer.Changed += HandleLineContainerChanged;
    }

    private void UnbindLineContainer()
    {
        if (_subscribedLineContainer != null)
            _subscribedLineContainer.Changed -= HandleLineContainerChanged;

        _subscribedLineContainer = null;
    }

    private void HandleLineContainerChanged()
    {
        _installedModule?.RefreshMassFromDefinitionAndContents();
    }

    private bool HasAnyLineLoaded()
    {
        EnsureLineContainer();

        if (lineContainer == null)
            return false;

        for (int i = 0; i < lineContainer.SlotCount; i++)
        {
            InventorySlot slot = lineContainer.GetSlot(i);
            if (slot != null && !slot.IsEmpty && slot.Instance != null)
                return true;
        }

        return false;
    }

    private float CalculateAvailableLine(out float workingLoad, out float breakingLoad)
    {
        EnsureLineContainer();
        workingLoad = 0f;
        breakingLoad = 0f;

        if (lineCatalog == null || lineContainer == null)
            return 0f;

        float totalLength = 0f;
        float remainingDeployed = Mathf.Max(0f, deployedLength);
        bool foundDeployedSegment = false;
        float weakestWorking = float.PositiveInfinity;
        float weakestBreaking = float.PositiveInfinity;

        for (int i = 0; i < lineContainer.SlotCount; i++)
        {
            InventorySlot slot = lineContainer.GetSlot(i);

            if (slot == null ||
                slot.IsEmpty ||
                slot.Instance == null ||
                slot.Instance.Definition == null)
            {
                continue;
            }

            ItemInstance instance = slot.Instance;

            if (!lineCatalog.TryGet(
                    instance.Definition,
                    out TetherLineCatalog.Entry entry) ||
                entry == null)
            {
                continue;
            }

            float segmentLength = Mathf.Max(0f, entry.LengthMeters);
            int quantity = Mathf.Max(0, instance.Quantity);

            if (segmentLength <= 0f || quantity <= 0)
                continue;

            totalLength += segmentLength * quantity;

            for (int unit = 0;
                 unit < quantity &&
                 remainingDeployed > SegmentBoundaryEpsilonMeters;
                 unit++)
            {
                foundDeployedSegment = true;
                weakestWorking = Mathf.Min(weakestWorking, entry.WorkingLoadNewtons);
                weakestBreaking = Mathf.Min(weakestBreaking, entry.BreakingLoadNewtons);
                remainingDeployed -= segmentLength;
            }
        }

        if (foundDeployedSegment)
        {
            workingLoad =
                float.IsPositiveInfinity(weakestWorking)
                    ? 0f
                    : weakestWorking;

            breakingLoad =
                float.IsPositiveInfinity(weakestBreaking)
                    ? 0f
                    : weakestBreaking;
        }

        return Mathf.Max(0f, totalLength);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Load Configured Line Item Into First Empty Slot")]
    private void DebugLoadLine()
    {
        EnsureLineContainer();

        if (debugLineItem == null)
        {
            Debug.LogWarning($"[Winch:{name}] Assign Debug Line Item first.", this);
            return;
        }

        ItemInstance instance = ItemInstance.Create(debugLineItem, 1);
        if (!CanAcceptLine(instance))
        {
            Debug.LogWarning($"[Winch:{name}] Debug item '{debugLineItem.DisplayName}' is not present in the assigned TetherLineCatalog.", this);
            return;
        }

        for (int i = 0; i < lineContainer.SlotCount; i++)
        {
            InventorySlot slot = lineContainer.GetSlot(i);
            if (slot == null || !slot.IsEmpty)
                continue;

            slot.Set(instance);
            lineContainer.NotifyChanged();
            _installedModule?.RefreshMassFromDefinitionAndContents();
            Debug.Log($"[Winch:{name}] Loaded '{debugLineItem.DisplayName}' into line slot {i}.", this);
            return;
        }

        Debug.LogWarning($"[Winch:{name}] No empty line slot available.", this);
    }

    [ContextMenu("Debug/Lower")]
    private void DebugLower() => TryLower();

    [ContextMenu("Debug/Stop")]
    private void DebugStop() => Stop();

    [ContextMenu("Debug/Raise")]
    private void DebugRaise() => TryRaise();

    [ContextMenu("Debug/Quick Release")]
    private void DebugQuickRelease() => QuickRelease();

    [ContextMenu("Debug/Cut Line")]
    private void DebugCutLine() => CutLine();
#endif
}
