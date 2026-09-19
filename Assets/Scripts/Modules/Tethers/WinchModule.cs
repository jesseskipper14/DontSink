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

    [Tooltip(
        "How long the controlled winch takes to transition from its current line speed " +
        "to a new commanded speed. This models spool/brake inertia so Stop and Raise do " +
        "not instantaneously arrest a free-spooling payload.")]
    [SerializeField, Min(0f)] private float spoolTransitionSeconds = 0.5f;

    [Header("Runtime")]
    [SerializeField] private ItemContainerState lineContainer;
    [SerializeField] private WinchCommand command = WinchCommand.Stop;
    [SerializeField] private float deployedLength;

    [Tooltip(
        "Signed line speed in meters/second. Positive = paying out, negative = reeling in.")]
    [SerializeField] private float currentLineSpeedMetersPerSecond;

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

    private bool _spoolTransitionActive;
    private float _spoolTransitionStartSpeed;
    private float _spoolTransitionTargetSpeed;
    private float _spoolTransitionElapsed;

    private const float SegmentBoundaryEpsilonMeters = 0.001f;
    private const float SpoolSpeedEpsilon = 0.001f;

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
    public float CurrentLineSpeedMetersPerSecond => currentLineSpeedMetersPerSecond;
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

        if (_constraint != null &&
            _constraint.TryConsumeBreak(
                out float breakTension))
        {
            HandleTetherBreak(
                breakTension);

            return;
        }

        if (_deployment == null ||
            !_deployment.HasDeployedPayload ||
            _payload == null ||
            _constraint == null)
        {
            if (command != WinchCommand.Stop)
                command = WinchCommand.Stop;

            ResetSpoolMotion();
            return;
        }

        float available = AvailableLineMeters;
        if (available <= 0f)
        {
            _deployment.EndPayloadRetrieval();
            command = WinchCommand.Stop;
            ResetSpoolMotion();
            return;
        }

        float dt =
            Mathf.Max(
                0f,
                Time.fixedDeltaTime);

        switch (command)
        {
            case WinchCommand.Lower:
                {
                    EnsureSpoolTarget(
                        Mathf.Max(
                            0f,
                            reelSpeedMetersPerSecond));

                    TickSpoolTransition(dt);
                    RefreshRetrievalIntentForActualSpoolMotion();
                    ApplyControlledSpoolMotion(available, dt);

                    if (currentLineSpeedMetersPerSecond < -SpoolSpeedEpsilon)
                        TryAutoDock();

                    break;
                }

            case WinchCommand.Raise:
                {
                    // Rated pull limits active hauling, but the brake can still slow a
                    // free-spooling payload toward zero instead of freezing line length
                    // in one physics step.
                    bool canActivelyHaul =
                        ratedPullNewtons <= 0f ||
                        CurrentTension <= ratedPullNewtons;

                    float targetSpeed =
                        canActivelyHaul
                            ? -Mathf.Max(
                                0f,
                                reelSpeedMetersPerSecond)
                            : 0f;

                    EnsureSpoolTarget(targetSpeed);
                    TickSpoolTransition(dt);

                    // Raise is explicit retrieval intent even while the drum is still
                    // braking through a positive payout speed.
                    _deployment.BeginPayloadRetrieval();

                    ApplyControlledSpoolMotion(available, dt);

                    if (currentLineSpeedMetersPerSecond <= SpoolSpeedEpsilon)
                        TryAutoDock();

                    break;
                }

            case WinchCommand.QuickRelease:
                {
                    _deployment.EndPayloadRetrieval();
                    CancelSpoolTransition();

                    float previousLength =
                        deployedLength;

                    float target =
                        Mathf.Min(
                            available,
                            Mathf.Max(
                                deployedLength,
                                CurrentDistance + quickReleaseSlackMeters));

                    deployedLength =
                        target;

                    currentLineSpeedMetersPerSecond =
                        dt > 0f
                            ? Mathf.Max(
                                0f,
                                (deployedLength - previousLength) / dt)
                            : 0f;

                    break;
                }

            case WinchCommand.Stop:
            default:
                {
                    EnsureSpoolTarget(0f);
                    TickSpoolTransition(dt);

                    // If Stop was pressed while line was still physically reeling in,
                    // keep retrieval-aware payloads released until the drum actually
                    // reaches zero speed. This avoids re-latching an anchor while the
                    // spool is still coasting inward.
                    RefreshRetrievalIntentForActualSpoolMotion();
                    ApplyControlledSpoolMotion(available, dt);

                    if (currentLineSpeedMetersPerSecond < -SpoolSpeedEpsilon)
                        TryAutoDock();

                    break;
                }
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
        ResetSpoolMotion();
        _deployment = null;
        _payload = null;
        _constraint = null;
    }

    /// <summary>
    /// Authoritative entry point for external winch control requests.
    /// UI, local input, and future network/host routing should request an intent
    /// here rather than directly mutating command, deployed length, or spool state.
    /// </summary>
    public bool TryApplyControlIntent(
        WinchControlIntent intent,
        out string message)
    {
        switch (intent)
        {
            case WinchControlIntent.Lower:
                {
                    bool ok =
                        TryLower();

                    message =
                        ok
                            ? "LOWER COMMAND ACCEPTED"
                            : "LOWER COMMAND REJECTED";

                    return ok;
                }

            case WinchControlIntent.Stop:
                {
                    Stop();

                    message =
                        "WINCH STOPPED";

                    return true;
                }

            case WinchControlIntent.Raise:
                {
                    bool ok =
                        TryRaise();

                    message =
                        ok
                            ? "RAISE COMMAND ACCEPTED"
                            : "RAISE COMMAND REJECTED";

                    return ok;
                }

            case WinchControlIntent.QuickRelease:
                {
                    bool ok =
                        QuickRelease();

                    message =
                        ok
                            ? "BRAKE RELEASED"
                            : "QUICK RELEASE REJECTED";

                    return ok;
                }

            case WinchControlIntent.CutLine:
                {
                    bool ok =
                        CutLine();

                    message =
                        ok
                            ? "LINE CUT | PAYLOAD RELEASED"
                            : "CUT LINE REJECTED";

                    return ok;
                }

            default:
                {
                    message =
                        $"UNKNOWN WINCH CONTROL INTENT: {intent}";

                    return false;
                }
        }
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
        StartSpoolTransition(
            Mathf.Max(
                0f,
                reelSpeedMetersPerSecond));

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
            ResetSpoolMotion();
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
        StartSpoolTransition(
            -Mathf.Max(
                0f,
                reelSpeedMetersPerSecond));

        return true;
    }

    public void Stop()
    {
        command = WinchCommand.Stop;
        StartSpoolTransition(0f);
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
        CancelSpoolTransition();
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

        ResetSpoolMotion();

        deployedLength =
            0f;

        _payload =
            null;

        _constraint =
            _deployment.TetherConstraint;

        return true;
    }

    private void HandleTetherBreak(
        float breakTension)
    {
        float breakingLoadAtFailure =
            BreakingLoadNewtons;

        float constraintLength =
            _constraint != null
                ? _constraint.DeployedLength
                : 0f;

        float lostLineLength =
            Mathf.Max(
                minimumDeployedLength,
                Mathf.Max(
                    deployedLength,
                    constraintLength));

        bool hasLossPlan =
            TryBuildCutLineLossPlan(
                lostLineLength,
                out List<CutLineLossPlanEntry> lossPlan,
                out float coveredLength);

        _deployment?.EndPayloadRetrieval();
        command = WinchCommand.Stop;
        ResetSpoolMotion();

        bool released =
            _deployment != null &&
            _deployment.TryCutLooseDeployedPayload(
                out WorldItem releasedWorldItem) &&
            releasedWorldItem != null;

        if (released &&
            hasLossPlan)
        {
            // V1 overload semantics intentionally match manual Cut Line:
            // every whole loaded segment needed to cover the deployed length
            // is sacrificed in sequential slot order. We do not invent a
            // fractional rope remnant or a hidden break position.
            ApplyCutLineLossPlan(
                lossPlan);
        }
        else if (released &&
                 !hasLossPlan)
        {
            Debug.LogError(
                $"[Winch:{name}] Tether physically broke, but the loaded line no longer " +
                $"covers the deployed length ({lostLineLength:F2} m deployed vs " +
                $"{coveredLength:F2} m valid line found). Payload was released, but line " +
                "inventory was left untouched to avoid deleting unrelated items.",
                this);
        }

        float reportedTension =
            Mathf.Max(
                breakTension,
                breakingLoadAtFailure);

        if (released)
        {
            GameMessageService.PostWarning(
                breakingLoadAtFailure > 0f
                    ? $"Tether snapped under {reportedTension:n0} N of load " +
                      $"(breaking load {breakingLoadAtFailure:n0} N)."
                    : "Tether snapped.");

            Debug.LogWarning(
                $"[Winch:{name}] Tether snapped | tension={reportedTension:F1} N " +
                $"| breakingLoad={breakingLoadAtFailure:F1} N " +
                $"| deployedLength={lostLineLength:F2} m.",
                this);
        }
        else
        {
            GameMessageService.PostWarning(
                "Tether joint snapped, but payload release state could not be reconciled.");

            Debug.LogError(
                $"[Winch:{name}] Tether joint broke but TetherDeploymentModule could not " +
                "release the deployed payload cleanly.",
                this);
        }

        deployedLength = 0f;
        _payload = null;

        _constraint =
            _deployment != null
                ? _deployment.TetherConstraint
                : null;
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

        ResetSpoolMotion();

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
        ResetSpoolMotion();
        deployedLength = 0f;

        _payload = null;
        _constraint = _deployment.TetherConstraint;
    }

    private void StartSpoolTransition(
        float targetSpeed)
    {
        targetSpeed =
            SanitizeLineSpeed(
                targetSpeed);

        float duration =
            Mathf.Max(
                0f,
                spoolTransitionSeconds);

        if (duration <= 0.0001f ||
            Mathf.Abs(
                currentLineSpeedMetersPerSecond -
                targetSpeed) <= SpoolSpeedEpsilon)
        {
            currentLineSpeedMetersPerSecond =
                targetSpeed;

            _spoolTransitionStartSpeed =
                targetSpeed;

            _spoolTransitionTargetSpeed =
                targetSpeed;

            _spoolTransitionElapsed =
                0f;

            _spoolTransitionActive =
                false;

            return;
        }

        _spoolTransitionStartSpeed =
            currentLineSpeedMetersPerSecond;

        _spoolTransitionTargetSpeed =
            targetSpeed;

        _spoolTransitionElapsed =
            0f;

        _spoolTransitionActive =
            true;
    }

    private void EnsureSpoolTarget(
        float targetSpeed)
    {
        targetSpeed =
            SanitizeLineSpeed(
                targetSpeed);

        if (_spoolTransitionActive)
        {
            if (Mathf.Abs(
                    _spoolTransitionTargetSpeed -
                    targetSpeed) <= SpoolSpeedEpsilon)
            {
                return;
            }

            StartSpoolTransition(
                targetSpeed);

            return;
        }

        if (Mathf.Abs(
                currentLineSpeedMetersPerSecond -
                targetSpeed) <= SpoolSpeedEpsilon)
        {
            currentLineSpeedMetersPerSecond =
                targetSpeed;

            return;
        }

        StartSpoolTransition(
            targetSpeed);
    }

    private void TickSpoolTransition(
        float dt)
    {
        if (!_spoolTransitionActive)
            return;

        float duration =
            Mathf.Max(
                0f,
                spoolTransitionSeconds);

        if (duration <= 0.0001f)
        {
            currentLineSpeedMetersPerSecond =
                _spoolTransitionTargetSpeed;

            _spoolTransitionActive =
                false;

            return;
        }

        _spoolTransitionElapsed +=
            Mathf.Max(
                0f,
                dt);

        float t =
            Mathf.Clamp01(
                _spoolTransitionElapsed /
                duration);

        currentLineSpeedMetersPerSecond =
            Mathf.Lerp(
                _spoolTransitionStartSpeed,
                _spoolTransitionTargetSpeed,
                t);

        if (t >= 1f)
        {
            currentLineSpeedMetersPerSecond =
                _spoolTransitionTargetSpeed;

            _spoolTransitionActive =
                false;
        }
    }

    private void ApplyControlledSpoolMotion(
        float availableLineMeters,
        float dt)
    {
        if (dt <= 0f)
            return;

        float minLength =
            Mathf.Max(
                0.01f,
                minimumDeployedLength);

        float maxLength =
            Mathf.Max(
                minLength,
                availableLineMeters);

        float nextLength =
            deployedLength +
            currentLineSpeedMetersPerSecond *
            dt;

        deployedLength =
            Mathf.Clamp(
                nextLength,
                minLength,
                maxLength);

        bool blockedAtMinimum =
            currentLineSpeedMetersPerSecond < 0f &&
            deployedLength <= minLength + SegmentBoundaryEpsilonMeters;

        bool blockedAtMaximum =
            currentLineSpeedMetersPerSecond > 0f &&
            deployedLength >= maxLength - SegmentBoundaryEpsilonMeters;

        if (blockedAtMinimum ||
            blockedAtMaximum)
        {
            currentLineSpeedMetersPerSecond =
                0f;

            CancelSpoolTransition();
        }
    }

    private void RefreshRetrievalIntentForActualSpoolMotion()
    {
        if (_deployment == null)
            return;

        if (currentLineSpeedMetersPerSecond <
            -SpoolSpeedEpsilon)
        {
            _deployment.BeginPayloadRetrieval();
        }
        else
        {
            _deployment.EndPayloadRetrieval();
        }
    }

    private void CancelSpoolTransition()
    {
        _spoolTransitionActive =
            false;

        _spoolTransitionStartSpeed =
            currentLineSpeedMetersPerSecond;

        _spoolTransitionTargetSpeed =
            currentLineSpeedMetersPerSecond;

        _spoolTransitionElapsed =
            0f;
    }

    private void ResetSpoolMotion()
    {
        currentLineSpeedMetersPerSecond =
            0f;

        _spoolTransitionActive =
            false;

        _spoolTransitionStartSpeed =
            0f;

        _spoolTransitionTargetSpeed =
            0f;

        _spoolTransitionElapsed =
            0f;
    }

    private static float SanitizeLineSpeed(
        float speed)
    {
        if (float.IsNaN(speed) ||
            float.IsInfinity(speed))
        {
            return 0f;
        }

        return speed;
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
