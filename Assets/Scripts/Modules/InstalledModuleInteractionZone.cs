using UnityEngine;

/// <summary>
/// Prefab-local interaction geometry / forwarding adapter for an installed module.
///
/// IMPORTANT:
/// This component owns NO module gameplay behavior. HardpointInteractable remains
/// authoritative for install/open/toggle/remove/link/access rules.
///
/// The adapter exists because an installed module's MountPoint is not guaranteed to
/// place the module underneath HardpointInteractable in Transform ancestry. Interactor2D
/// can therefore hit this authored collider directly and this component forwards the
/// request to the exact owning HardpointInteractable resolved through
/// InstalledModule.OwnerHardpoint.
/// </summary>
[DisallowMultipleComponent]
public sealed class InstalledModuleInteractionZone :
    MonoBehaviour,
    IInteractable,
    IPickupInteractable,
    IInteractPromptProvider,
    IPickupPromptProvider,
    IToggleInteractable,
    ILinkInteractable,
    IInteractionLabelProvider,
    IInteractionPromptDisplayPolicyProvider,
    IInteractionColliderScope
{
    [Header("Authored Interaction Geometry")]
    [Tooltip(
        "Collider(s) that represent this installed module for interaction. " +
        "If left empty, colliders on this object and its children are discovered automatically.")]
    [SerializeField] private Collider2D[] interactionColliders;

    [Tooltip(
        "Optional prompt anchor. If left empty, this component searches the owning " +
        "InstalledModule for a descendant named 'PromptAnchor', then falls back to this transform.")]
    [SerializeField] private Transform promptAnchor;

    [Header("Runtime Debug")]
    [SerializeField] private InstalledModule resolvedInstalledModule;
    [SerializeField] private Hardpoint resolvedHardpoint;
    [SerializeField] private HardpointInteractable resolvedOwner;
    [SerializeField] private Rigidbody2D interactionQueryBody;

    private Collider2D[] _runtimeColliders;

    public Transform PromptAnchor
    {
        get
        {
            ResolvePromptAnchor();
            return promptAnchor != null ? promptAnchor : transform;
        }
    }

    public bool HasInteractionColliders
    {
        get
        {
            Collider2D[] colliders = GetInteractionColliders();
            if (colliders == null)
                return false;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    return true;
            }

            return false;
        }
    }

    public int InteractionPriority
    {
        get
        {
            HardpointInteractable owner = ResolveOwner();
            return owner != null ? owner.InteractionPriority : 0;
        }
    }

    public int PickupPriority
    {
        get
        {
            HardpointInteractable owner = ResolveOwner();
            return owner != null ? owner.PickupPriority : 0;
        }
    }

    public PickupInteractionMode PickupMode
    {
        get
        {
            HardpointInteractable owner = ResolveOwner();
            return owner != null ? owner.PickupMode : PickupInteractionMode.Instant;
        }
    }

    public float PickupHoldDuration
    {
        get
        {
            HardpointInteractable owner = ResolveOwner();
            return owner != null ? owner.PickupHoldDuration : 0f;
        }
    }

    private void Reset()
    {
        AutoConfigure();
        EnsureInteractionQueryBody();
        ResolveOwner();
    }

    private void Awake()
    {
        EnsureRuntimeColliderCache();

        // Mounted modules may live under a Rigidbody2D whose simulated flag is
        // intentionally false. Child colliders then disappear from Physics2D
        // overlap/raycast queries unless they have their own simulated body.
        EnsureInteractionQueryBody();

        ResolveOwner();
        ResolvePromptAnchor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _runtimeColliders = null;

        if (interactionQueryBody == null)
            interactionQueryBody = GetComponent<Rigidbody2D>();

        ResolveOwner();

        if (promptAnchor == null)
            ResolvePromptAnchor();
    }

    [ContextMenu("Auto Configure Interaction Zone")]
    private void DebugAutoConfigure()
    {
        AutoConfigure();
        ResolveOwner();
    }
#endif

    // ---------------------------------------------------------------------
    // Thin forwarding surface. HardpointInteractable remains authoritative.
    // ---------------------------------------------------------------------

    public bool CanInteract(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null && owner.CanInteract(context);
    }

    public void Interact(in InteractContext context)
    {
        ResolveOwner()?.Interact(context);
    }

    public bool CanPickup(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null && owner.CanPickup(context);
    }

    public void Pickup(in InteractContext context)
    {
        ResolveOwner()?.Pickup(context);
    }

    public bool CanToggle(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null && owner.CanToggle(context);
    }

    public void Toggle(in InteractContext context)
    {
        ResolveOwner()?.Toggle(context);
    }

    public bool CanLink(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null && owner.CanLink(context);
    }

    public string GetLinkPromptVerb(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null ? owner.GetLinkPromptVerb(context) : "Link Module";
    }

    public void Link(in InteractContext context)
    {
        ResolveOwner()?.Link(context);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null ? owner.GetPromptVerb(context) : "Use Module";
    }

    public Transform GetPromptAnchor()
    {
        return PromptAnchor;
    }

    public string GetPickupPromptVerb(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null ? owner.GetPickupPromptVerb(context) : "Remove Module";
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null ? owner.GetInteractionLabel(context) : "Installed Module";
    }

    public bool ShouldShowHoverLabel(in InteractContext context)
    {
        HardpointInteractable owner = ResolveOwner();
        return owner != null && owner.ShouldShowHoverLabel(context);
    }

    public bool AllowsInteractionCollider(Collider2D sourceCollider)
    {
        return Contains(sourceCollider);
    }

    // ---------------------------------------------------------------------
    // Physics query body
    // ---------------------------------------------------------------------

    private void EnsureInteractionQueryBody()
    {
        if (interactionQueryBody == null)
            interactionQueryBody = GetComponent<Rigidbody2D>();

        if (interactionQueryBody == null)
            interactionQueryBody = gameObject.AddComponent<Rigidbody2D>();

        interactionQueryBody.bodyType = RigidbodyType2D.Kinematic;
        interactionQueryBody.simulated = true;
        interactionQueryBody.gravityScale = 0f;
        interactionQueryBody.linearVelocity = Vector2.zero;
        interactionQueryBody.angularVelocity = 0f;
    }

    // ---------------------------------------------------------------------
    // Geometry
    // ---------------------------------------------------------------------

    public bool Contains(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
            return false;

        Collider2D[] colliders = GetInteractionColliders();
        if (colliders == null)
            return false;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (ReferenceEquals(colliders[i], sourceCollider))
                return true;
        }

        return false;
    }

    public float GetDistanceFrom(Vector2 worldOrigin)
    {
        Collider2D[] colliders = GetInteractionColliders();
        float best = float.PositiveInfinity;

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 closest = collider.ClosestPoint(worldOrigin);
                float distance = Vector2.Distance(worldOrigin, closest);

                if (distance < best)
                    best = distance;
            }
        }

        if (!float.IsPositiveInfinity(best))
            return best;

        return Vector2.Distance(worldOrigin, transform.position);
    }

    private Collider2D[] GetInteractionColliders()
    {
        if (interactionColliders != null && interactionColliders.Length > 0)
            return interactionColliders;

        EnsureRuntimeColliderCache();
        return _runtimeColliders;
    }

    private void EnsureRuntimeColliderCache()
    {
        if (_runtimeColliders != null && _runtimeColliders.Length > 0)
            return;

        _runtimeColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void AutoConfigure()
    {
        interactionColliders = GetComponentsInChildren<Collider2D>(true);
        _runtimeColliders = null;
        ResolvePromptAnchor();
    }

    // ---------------------------------------------------------------------
    // Exact owner resolution through installed-module ownership, not hierarchy.
    // ---------------------------------------------------------------------

    private HardpointInteractable ResolveOwner()
    {
        if (resolvedOwner != null &&
            resolvedHardpoint != null &&
            resolvedInstalledModule != null)
        {
            return resolvedOwner;
        }

        resolvedInstalledModule =
            GetComponentInParent<InstalledModule>();

        resolvedHardpoint =
            resolvedInstalledModule != null
                ? resolvedInstalledModule.OwnerHardpoint
                : null;

        // Robust fallback:
        // InstalledModule.OwnerHardpoint may not yet be populated when this child
        // adapter wakes, and some existing runtime/module restoration paths may
        // leave that back-reference unset. The installed prefab is still parented
        // beneath the hardpoint's MountPoint, so hierarchy is a valid secondary
        // ownership source.
        if (resolvedHardpoint == null &&
            resolvedInstalledModule != null)
        {
            resolvedHardpoint =
                resolvedInstalledModule.GetComponentInParent<Hardpoint>(
                    true);
        }

        if (resolvedHardpoint == null)
        {
            resolvedHardpoint =
                GetComponentInParent<Hardpoint>(
                    true);
        }

        resolvedOwner =
            null;

        if (resolvedHardpoint != null)
        {
            resolvedOwner =
                resolvedHardpoint.GetComponent<HardpointInteractable>();

            if (resolvedOwner == null)
            {
                resolvedOwner =
                    resolvedHardpoint.GetComponentInChildren<HardpointInteractable>(
                        true);
            }

            if (resolvedOwner == null)
            {
                resolvedOwner =
                    resolvedHardpoint.GetComponentInParent<HardpointInteractable>(
                        true);
            }
        }

        // Final compatibility fallback for unusual authored hierarchies where the
        // interaction owner is above the module but the Hardpoint component itself
        // is not in that same direct chain.
        if (resolvedOwner == null)
        {
            resolvedOwner =
                GetComponentInParent<HardpointInteractable>(
                    true);

            if (resolvedOwner != null &&
                resolvedHardpoint == null)
            {
                resolvedHardpoint =
                    resolvedOwner.TargetHardpoint;
            }
        }

        return resolvedOwner;
    }

    private void ResolvePromptAnchor()
    {
        if (promptAnchor != null)
            return;

        if (resolvedInstalledModule == null)
            resolvedInstalledModule = GetComponentInParent<InstalledModule>();

        Transform searchRoot = resolvedInstalledModule != null
            ? resolvedInstalledModule.transform
            : transform.parent;

        if (searchRoot == null)
            return;

        promptAnchor = FindDescendantByName(searchRoot, "PromptAnchor");
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
