using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(InstalledModule))]
public sealed class WinchModule : MonoBehaviour, IInstalledModuleLifecycle, IInstalledModuleRemovalGuard, IInstalledModuleAdditionalMass
{
    [Header("Rigging")]
    [SerializeField] private Transform lineAnchor;
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
    private Rigidbody2D _boatBody;
    private TetherPayloadModule _payload;
    private ItemContainerState _subscribedLineContainer;

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
    public bool HasDeployedPayload => _payload != null && !_payload.IsStowed;

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
        if (_payload == null || _payload.IsStowed || _payload.IsCutLoose)
            return;

        float available = AvailableLineMeters;
        if (available <= 0f)
        {
            command = WinchCommand.Stop;
            return;
        }

        switch (command)
        {
            case WinchCommand.Lower:
                deployedLength = Mathf.Min(
                    available,
                    deployedLength + reelSpeedMetersPerSecond * Time.fixedDeltaTime);
                break;

            case WinchCommand.Raise:
                float speedScale = ratedPullNewtons <= 0f || CurrentTension <= ratedPullNewtons
                    ? 1f
                    : 0f;

                deployedLength = Mathf.Max(
                    minimumDeployedLength,
                    deployedLength - reelSpeedMetersPerSecond * speedScale * Time.fixedDeltaTime);

                _payload.BeginRetrieving();
                TryAutoDock();
                break;

            case WinchCommand.QuickRelease:
                float target = Mathf.Min(
                    available,
                    Mathf.Max(deployedLength, CurrentDistance + quickReleaseSlackMeters));
                deployedLength = target;
                break;
        }

        if (_constraint != null)
        {
            _constraint.SetDeployedLength(deployedLength);
            _constraint.SetLineRatings(WorkingLoadNewtons, BreakingLoadNewtons);
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
        if (_constraint != null)
            _constraint.Detach();
    }

    public bool TryLower()
    {
        if (!EnsurePayloadBoundForDeployment())
            return false;

        if (AvailableLineMeters <= 0f)
        {
            Debug.LogWarning($"[Winch:{name}] Cannot lower: no valid tether line is loaded.", this);
            return false;
        }

        if (_payload.IsStowed)
        {
            if (!_payload.BeginDeploy())
                return false;

            deployedLength = Mathf.Clamp(
                Mathf.Max(minimumDeployedLength, CurrentPayloadDistance()),
                minimumDeployedLength,
                AvailableLineMeters);

            BindConstraint();
            _payload.MarkSuspended();
        }

        command = WinchCommand.Lower;
        return true;
    }

    public bool TryRaise()
    {
        if (!EnsurePayloadBoundForDeployment())
            return false;

        if (_payload.IsStowed)
        {
            command = WinchCommand.Stop;
            return true;
        }

        if (_payload.IsCutLoose)
            return false;

        BindConstraint();
        command = WinchCommand.Raise;
        _payload.BeginRetrieving();
        return true;
    }

    public void Stop()
    {
        command = WinchCommand.Stop;
    }

    public bool QuickRelease()
    {
        if (!EnsurePayloadBoundForDeployment())
            return false;

        if (_payload.IsStowed && !TryLower())
            return false;

        command = WinchCommand.QuickRelease;
        return true;
    }

    public bool CutLine()
    {
        if (_payload == null || _payload.IsStowed || _payload.IsCutLoose)
            return false;

        command = WinchCommand.Stop;

        if (_constraint != null)
            _constraint.Detach();

        _payload.CutLoose();
        return true;
    }

    public bool CanRemoveInstalledModule(out string reason)
    {
        if (_payload != null && !_payload.IsStowed)
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

    private void CacheRefs()
    {
        if (_installedModule == null)
            _installedModule = GetComponent<InstalledModule>();

        if (_ownerHardpoint == null && _installedModule != null)
            _ownerHardpoint = _installedModule.OwnerHardpoint;

        if (_ownerHardpoint != null)
        {
            if (_link == null)
                _link = _ownerHardpoint.GetComponent<TetherWinchLink>();

            if (_boatBody == null)
                _boatBody = _ownerHardpoint.GetComponentInParent<Rigidbody2D>();
        }

        if (_constraint == null)
            _constraint = GetComponent<TetherConstraint2D>();

        if (_constraint == null)
            _constraint = gameObject.AddComponent<TetherConstraint2D>();
    }

    private bool EnsurePayloadBoundForDeployment()
    {
        CacheRefs();

        if (_link == null)
        {
            Debug.LogWarning($"[Winch:{name}] No TetherWinchLink exists on owner hardpoint.", this);
            return false;
        }

        if (!_link.TryGetPayload(out TetherPayloadModule payload) || payload == null)
        {
            Debug.LogWarning($"[Winch:{name}] Linked payload hardpoint has no TetherPayloadModule installed.", this);
            return false;
        }

        _payload = payload;
        return true;
    }

    private void BindConstraint()
    {
        if (_constraint == null || _payload == null)
            return;

        _constraint.Bind(
            lineAnchor != null ? lineAnchor : transform,
            _boatBody,
            _payload,
            Mathf.Max(minimumDeployedLength, deployedLength),
            WorkingLoadNewtons,
            BreakingLoadNewtons);
    }

    private float CurrentPayloadDistance()
    {
        if (_payload == null)
            return minimumDeployedLength;

        Vector2 start = lineAnchor != null ? lineAnchor.position : transform.position;
        Vector2 end = _payload.TetherAnchor.position;
        return Vector2.Distance(start, end);
    }

    private void TryAutoDock()
    {
        if (_payload == null || _payload.IsStowed || _payload.IsCutLoose)
            return;

        ResolveOwnerHardpoint();
        if (_payload.OwnerHardpoint == null)
            return;

        float distanceToDock = _payload.DistanceToStowPose;

        if (distanceToDock > dockingDistance)
            return;

        _payload.BeginDocking();

        if (_payload.TryStow())
        {
            command = WinchCommand.Stop;
            deployedLength = 0f;
            _constraint?.Detach();
        }
    }

    private void ResolveOwnerHardpoint()
    {
        if (_ownerHardpoint == null && _installedModule != null)
            _ownerHardpoint = _installedModule.OwnerHardpoint;
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

        float length = 0f;
        bool foundAny = false;
        float weakestWorking = float.PositiveInfinity;
        float weakestBreaking = float.PositiveInfinity;

        for (int i = 0; i < lineContainer.SlotCount; i++)
        {
            InventorySlot slot = lineContainer.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Instance == null || slot.Instance.Definition == null)
                continue;

            if (!lineCatalog.TryGet(slot.Instance.Definition, out TetherLineCatalog.Entry entry) || entry == null)
                continue;

            foundAny = true;
            length += entry.LengthMeters * slot.Instance.Quantity;
            weakestWorking = Mathf.Min(weakestWorking, entry.WorkingLoadNewtons);
            weakestBreaking = Mathf.Min(weakestBreaking, entry.BreakingLoadNewtons);
        }

        if (!foundAny)
            return 0f;

        workingLoad = float.IsPositiveInfinity(weakestWorking) ? 0f : weakestWorking;
        breakingLoad = float.IsPositiveInfinity(weakestBreaking) ? 0f : weakestBreaking;
        return Mathf.Max(0f, length);
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
