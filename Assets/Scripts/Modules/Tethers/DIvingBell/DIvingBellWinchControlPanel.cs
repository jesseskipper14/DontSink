using UnityEngine;

/// <summary>
/// Bell-local resolver/authority bridge for internal winch controls.
///
/// The panel does not move the bell or mutate tether state directly. It resolves
/// the WinchModule whose TetherWinchLink currently owns THIS bell's deployment,
/// validates that the requesting player occupies THIS bell, then forwards a
/// normal WinchControlIntent to WinchModule.TryApplyControlIntent().
///
/// Current routing is local/synchronous, matching WinchOverlayRunner. In a future
/// multiplayer build, this method is the natural client -> host request seam while
/// WinchModule remains authoritative over actual spool/deployment state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DivingBellOccupancy))]
public sealed class DivingBellWinchControlPanel : MonoBehaviour
{
    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Header("Internal Safety")]
    [Tooltip(
        "Basic bell controls intentionally permit only Lower, Stop, and Raise. " +
        "Leave this disabled until an intentional emergency-control pass adds " +
        "Quick Release / Cut Line UI and confirmation behavior.")]
    [SerializeField] private bool allowDangerousIntents = false;

    [Header("Runtime Debug")]
    [SerializeField] private TetherPayload resolvedPayload;
    [SerializeField] private TetherWinchLink resolvedLink;
    [SerializeField] private TetherDeploymentModule resolvedDeployment;
    [SerializeField] private WinchModule resolvedWinch;
    [SerializeField] private string lastResolveMessage;
    [SerializeField] private string lastControlMessage;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public DivingBellOccupancy Occupancy => occupancy;
    public WinchModule ResolvedWinch => resolvedWinch;
    public TetherDeploymentModule ResolvedDeployment => resolvedDeployment;

    private void Awake()
    {
        ResolveRefs();
        ResolveWinchTarget(forceRefresh: true);
    }

    private void OnEnable()
    {
        ResolveRefs();
    }

    /// <summary>
    /// Returns true only when this exact player occupies this exact bell and the
    /// bell can be traced through its deployment hardpoint/link to one winch.
    /// </summary>
    public bool CanControl(
        GameObject interactorGO,
        out string reason)
    {
        reason = null;

        ResolveRefs();

        if (occupancy == null)
        {
            reason = "DIVING BELL OCCUPANCY IS MISSING";
            return false;
        }

        if (interactorGO == null)
        {
            reason = "NO CONTROLLING PLAYER";
            return false;
        }

        if (!occupancy.Contains(interactorGO))
        {
            reason = "CONTROL AVAILABLE ONLY TO THIS BELL'S OCCUPANTS";
            return false;
        }

        if (!ResolveWinchTarget(forceRefresh: false))
        {
            reason = !string.IsNullOrWhiteSpace(lastResolveMessage)
                ? lastResolveMessage
                : "NO LINKED WINCH FOUND FOR THIS BELL";

            return false;
        }

        if (resolvedWinch == null ||
            !resolvedWinch.isActiveAndEnabled)
        {
            reason = "LINKED WINCH IS UNAVAILABLE";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Bell-local request entry point. This validates occupant + bell + link
    /// identity, then forwards the existing WinchControlIntent to WinchModule.
    /// </summary>
    public bool TryApplyControlIntent(
        GameObject interactorGO,
        WinchControlIntent intent,
        out string message)
    {
        message = null;

        if (!IsIntentAllowedInternally(intent))
        {
            message =
                $"{intent.ToString().ToUpperInvariant()} IS NOT ENABLED ON INTERNAL BELL CONTROLS";

            lastControlMessage = message;
            return false;
        }

        if (!CanControl(interactorGO, out string reason))
        {
            message = reason;
            lastControlMessage = message;
            return false;
        }

        // WinchModule is the existing runtime authority. Do not duplicate its
        // deployment, spool, load, docking, or line validation here.
        bool ok = resolvedWinch.TryApplyControlIntent(
            intent,
            out message);

        lastControlMessage = message;

        if (verboseLogging)
        {
            Debug.Log(
                $"[DivingBellWinchControlPanel:{name}] " +
                $"player='{interactorGO.name}' bell='{occupancy.name}' " +
                $"intent={intent} success={ok} message='{message}'",
                this);
        }

        return ok;
    }

    public bool IsIntentAllowedInternally(
        WinchControlIntent intent)
    {
        if (intent == WinchControlIntent.Lower ||
            intent == WinchControlIntent.Stop ||
            intent == WinchControlIntent.Raise)
        {
            return true;
        }

        return allowDangerousIntents &&
               (intent == WinchControlIntent.QuickRelease ||
                intent == WinchControlIntent.CutLine);
    }

    /// <summary>
    /// Resolve the unique winch whose TetherWinchLink points at the deployment
    /// module currently representing this physical bell.
    ///
    /// This scan occurs only when the cache is missing/stale or explicitly
    /// refreshed. It is deliberately identity-based rather than "nearest winch"
    /// so multiple boats/bells remain independent in multiplayer.
    /// </summary>
    public bool ResolveWinchTarget(
        bool forceRefresh)
    {
        ResolveRefs();

        resolvedPayload =
            occupancy != null
                ? occupancy.Payload
                : null;

        if (resolvedPayload == null)
        {
            ClearResolvedTarget();
            lastResolveMessage = "BELL HAS NO TETHER PAYLOAD";
            return false;
        }

        if (!forceRefresh &&
            IsCachedTargetStillValid())
        {
            lastResolveMessage = "LINKED WINCH RESOLVED";
            return true;
        }

        ClearResolvedTarget();

        TetherWinchLink[] links =
            Object.FindObjectsByType<TetherWinchLink>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        TetherWinchLink matchedLink = null;
        TetherDeploymentModule matchedDeployment = null;
        WinchModule matchedWinch = null;
        int matchCount = 0;

        for (int i = 0; i < links.Length; i++)
        {
            TetherWinchLink link = links[i];

            if (link == null ||
                !link.TryGetDeploymentModule(
                    out TetherDeploymentModule deployment) ||
                deployment == null ||
                !DeploymentRepresentsThisPayload(
                    deployment,
                    resolvedPayload))
            {
                continue;
            }

            Hardpoint winchHardpoint =
                link.OwnerWinchHardpoint;

            if (winchHardpoint == null ||
                !winchHardpoint.HasInstalledModule ||
                winchHardpoint.InstalledModule == null)
            {
                continue;
            }

            WinchModule winch =
                winchHardpoint.InstalledModule
                    .GetComponent<WinchModule>();

            if (winch == null)
                continue;

            matchCount++;

            matchedLink = link;
            matchedDeployment = deployment;
            matchedWinch = winch;
        }

        if (matchCount == 1)
        {
            resolvedLink = matchedLink;
            resolvedDeployment = matchedDeployment;
            resolvedWinch = matchedWinch;

            lastResolveMessage = "LINKED WINCH RESOLVED";
            return true;
        }

        if (matchCount > 1)
        {
            lastResolveMessage =
                $"AMBIGUOUS WINCH LINK: {matchCount} WINCHES CLAIM THIS BELL";

            Debug.LogWarning(
                $"[DivingBellWinchControlPanel:{name}] {lastResolveMessage}. " +
                "Refusing to choose one implicitly.",
                this);

            return false;
        }

        lastResolveMessage =
            "NO WINCH LINK CURRENTLY CLAIMS THIS BELL";

        return false;
    }

    private bool IsCachedTargetStillValid()
    {
        if (resolvedPayload == null ||
            resolvedLink == null ||
            resolvedDeployment == null ||
            resolvedWinch == null)
        {
            return false;
        }

        if (!resolvedLink.TryGetDeploymentModule(
                out TetherDeploymentModule currentDeployment) ||
            !ReferenceEquals(
                currentDeployment,
                resolvedDeployment))
        {
            return false;
        }

        if (!DeploymentRepresentsThisPayload(
                resolvedDeployment,
                resolvedPayload))
        {
            return false;
        }

        Hardpoint winchHardpoint =
            resolvedLink.OwnerWinchHardpoint;

        if (winchHardpoint == null ||
            !winchHardpoint.HasInstalledModule ||
            winchHardpoint.InstalledModule == null)
        {
            return false;
        }

        WinchModule currentWinch =
            winchHardpoint.InstalledModule
                .GetComponent<WinchModule>();

        return
            currentWinch != null &&
            ReferenceEquals(
                currentWinch,
                resolvedWinch);
    }

    private static bool DeploymentRepresentsThisPayload(
        TetherDeploymentModule deployment,
        TetherPayload payload)
    {
        if (deployment == null ||
            payload == null)
        {
            return false;
        }

        // Deployed / retrieving / dock-capturing path.
        if (ReferenceEquals(
                deployment.DeployedPayload,
                payload))
        {
            return true;
        }

        // Live physical stored shell path. This is what lets an occupant press
        // LOWER while the bell is already docked/stowed and deploy without ever
        // opening the runtime Inspector.
        WorldItem dockedWorldItem =
            deployment.DockedPhysicalWorldItem;

        if (dockedWorldItem == null)
            return false;

        TetherPayload dockedPayload =
            dockedWorldItem.GetComponent<TetherPayload>();

        return
            dockedPayload != null &&
            ReferenceEquals(
                dockedPayload,
                payload);
    }

    private void ResolveRefs()
    {
        if (occupancy == null)
            occupancy = GetComponent<DivingBellOccupancy>();

        if (occupancy == null)
            occupancy = GetComponentInParent<DivingBellOccupancy>();
    }

    private void ClearResolvedTarget()
    {
        resolvedLink = null;
        resolvedDeployment = null;
        resolvedWinch = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Resolve Linked Winch")]
    private void DebugResolveLinkedWinch()
    {
        bool ok =
            ResolveWinchTarget(
                forceRefresh: true);

        Debug.Log(
            $"[DivingBellWinchControlPanel:{name}] resolve={ok} " +
            $"message='{lastResolveMessage}' " +
            $"winch='{(resolvedWinch != null ? resolvedWinch.name : "NONE")}' " +
            $"deployment='{(resolvedDeployment != null ? resolvedDeployment.name : "NONE")}'.",
            this);
    }
#endif
}
