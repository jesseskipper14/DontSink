using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractPromptDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Interactor2D interactor;
    [SerializeField] private InteractPromptUI promptUI;

    [Header("Multiplayer (Optional)")]
    [Tooltip("If set, prompts will only show when this returns IsLocal=true. Leave null for singleplayer.")]
    [SerializeField] private MonoBehaviour localAuthoritySource; // should implement ILocalPlayerAuthority

    [Header("Fallback")]
    [SerializeField] private string defaultVerb = "Interact";

    [Header("Debug")]
    [SerializeField] private bool debugPromptTargets = false;
    [SerializeField, Min(1)] private int debugPromptEveryNFrames = 15;

    private readonly List<PromptAction> _promptActions = new();
    private readonly HashSet<IInteractable> _suppressedInteract = new();
    private readonly HashSet<IPickupInteractable> _suppressedPickup = new();

    private ILocalPlayerAuthority _localAuth;
    private WorldItem _highlightedWorldItem;

    private void Reset()
    {
        interactor = GetComponent<Interactor2D>();
        promptUI = FindAnyObjectByType<InteractPromptUI>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (interactor == null)
            interactor = GetComponent<Interactor2D>();

        if (promptUI == null)
            promptUI = FindAnyObjectByType<InteractPromptUI>(FindObjectsInactive.Include);

        _localAuth = localAuthoritySource as ILocalPlayerAuthority;

        if (localAuthoritySource != null && _localAuth == null)
            Debug.LogError($"{name}: localAuthoritySource must implement ILocalPlayerAuthority.", this);

        if (promptUI != null)
            promptUI.Hide();
    }

    private void OnEnable()
    {
        if (interactor == null)
            return;

        interactor.OnInteracted += HandleInteracted;
        interactor.OnPickedUp += HandlePickedUp;
    }

    private void OnDisable()
    {
        if (interactor != null)
        {
            interactor.OnInteracted -= HandleInteracted;
            interactor.OnPickedUp -= HandlePickedUp;
        }

        ClearWorldItemHighlight();

        if (promptUI != null)
            promptUI.Hide();
    }

    private void LateUpdate()
    {
        if (promptUI == null || interactor == null)
            return;

        if (!HasLocalPromptAuthority())
        {
            HidePromptAndHighlight();
            return;
        }

        PruneSuppressedTargets();

        if (!TryResolveVisibleHoverTarget(out InteractionHoverTarget target, out InteractContext ctx))
        {
            HidePromptAndHighlight();
            return;
        }

        BuildPromptActions(target, ctx);
        UpdateWorldItemHighlight(target);

        if (_promptActions.Count == 0)
        {
            promptUI.Hide();
            return;
        }

        _promptActions.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        promptUI.Show(ResolvePromptPosition(target), _promptActions);
    }

    private void HandleInteracted(IInteractable target)
    {
        if (target != null)
            _suppressedInteract.Add(target);

        if (promptUI != null)
            promptUI.Hide();
    }

    private void HandlePickedUp(IPickupInteractable target)
    {
        if (target != null)
            _suppressedPickup.Add(target);

        ClearWorldItemHighlight();
    }

    private bool HasLocalPromptAuthority()
    {
        return _localAuth == null || _localAuth.IsLocal;
    }

    private void PruneSuppressedTargets()
    {
        if (_suppressedInteract.Count > 0)
            _suppressedInteract.RemoveWhere(t => t == null || !interactor.IsCandidatePresent(t));

        if (_suppressedPickup.Count > 0)
            _suppressedPickup.RemoveWhere(t => t == null || !interactor.IsPickupCandidatePresent(t));
    }

    private bool TryResolveVisibleHoverTarget(out InteractionHoverTarget target, out InteractContext ctx)
    {
        target = default;
        ctx = default;

        if (!interactor.TryGetMouseHoverTarget(out target, out ctx))
            return false;

        if (!target.IsValid)
            return false;

        if (!interactor.IsWithinHoverNameRange(target, ctx))
            return false;

        DebugPrompt(
            $"hover owner={DescribePromptTarget(target.Owner)} " +
            $"interact={target.Interact != null} pickup={target.Pickup != null} " +
            $"actionRange={interactor.IsWithinActionRange(target, ctx)}");

        return true;
    }

    private void BuildPromptActions(in InteractionHoverTarget target, in InteractContext ctx)
    {
        _promptActions.Clear();

        string label = ResolveInteractionLabel(target, ctx);
        if (!string.IsNullOrWhiteSpace(label))
            _promptActions.Add(new PromptAction(label, priority: 200));

        if (!interactor.IsWithinActionRange(target, ctx))
            return;

        AddInteractActions(target, ctx);
        AddPickupActions(target, ctx);
        AddToggleActions(target, ctx);
    }

    private Vector3 ResolvePromptPosition(in InteractionHoverTarget target)
    {
        if (target.PromptProvider != null)
        {
            Transform anchor = target.PromptProvider.GetPromptAnchor();
            if (anchor != null)
                return anchor.position;
        }

        if (target.PickupPromptProvider is IInteractPromptProvider pickupAsInteractPrompt)
        {
            Transform anchor = pickupAsInteractPrompt.GetPromptAnchor();
            if (anchor != null)
                return anchor.position;
        }

        if (target.SourceCollider != null)
            return target.SourceCollider.bounds.center;

        if (target.Owner != null)
            return target.Owner.transform.position;

        return transform.position;
    }

    private string ResolveInteractionLabel(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (target.Owner is IInteractionPromptDisplayPolicyProvider displayPolicy &&
            !displayPolicy.ShouldShowHoverLabel(ctx))
        {
            return string.Empty;
        }

        if (target.LabelProvider != null)
        {
            string label = target.LabelProvider.GetInteractionLabel(ctx);
            if (!string.IsNullOrWhiteSpace(label))
                return label;
        }

        if (target.Owner != null)
            return CleanObjectName(target.Owner.name);

        if (target.SourceCollider != null)
            return CleanObjectName(target.SourceCollider.name);

        return string.Empty;
    }

    private void AddInteractActions(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (target.Interact == null)
            return;

        if (_suppressedInteract.Contains(target.Interact))
            return;

        if (!target.Interact.CanInteract(ctx))
            return;

        if (target.ActionProvider != null)
        {
            target.ActionProvider.GetPromptActions(ctx, _promptActions);
            return;
        }

        string interactVerb = defaultVerb;

        if (string.IsNullOrWhiteSpace(interactVerb))
            interactVerb = "Interact";

        if (target.PromptProvider != null)
        {
            string providedVerb = target.PromptProvider.GetPromptVerb(ctx);
            if (!string.IsNullOrWhiteSpace(providedVerb))
                interactVerb = providedVerb;
        }

        _promptActions.Add(new PromptAction($"Press E to {interactVerb}", priority: 100));
    }

    private void AddPickupActions(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (target.Pickup == null)
            return;

        if (_suppressedPickup.Contains(target.Pickup))
            return;

        if (!target.Pickup.CanPickup(ctx))
            return;

        string pickupVerb = ResolvePickupVerb(target, ctx);
        bool isHoldPickup = target.Pickup.PickupMode == PickupInteractionMode.Hold;
        float progress = 0f;

        if (isHoldPickup && ReferenceEquals(interactor.ActiveHoldPickupTarget, target.Pickup))
            progress = interactor.ActiveHoldPickupProgress;

        _promptActions.Add(new PromptAction(
            isHoldPickup ? $"Hold F to {pickupVerb}" : $"Press F to {pickupVerb}",
            priority: 90,
            showProgress: isHoldPickup,
            progress01: progress));
    }

    private string ResolvePickupVerb(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (target.PickupPromptProvider != null)
        {
            string verb = target.PickupPromptProvider.GetPickupPromptVerb(ctx);
            if (!string.IsNullOrWhiteSpace(verb))
                return verb;
        }

        if (target.PromptProvider != null)
        {
            string verb = target.PromptProvider.GetPromptVerb(ctx);
            if (!string.IsNullOrWhiteSpace(verb))
                return verb;
        }

        return "Pick up";
    }

    private void AddToggleActions(in InteractionHoverTarget target, in InteractContext ctx)
    {
        if (target.Interact is not HardpointInteractable hardpointInteractable)
            return;

        if (!hardpointInteractable.CanInteract(ctx))
            return;

        if (!hardpointInteractable.TryGetInstalledToggleState(out bool isOn, out string label))
            return;

        string stateLabel = string.IsNullOrWhiteSpace(label) ? "Module" : label;

        _promptActions.Add(new PromptAction(
            isOn ? $"{stateLabel}: ON" : $"{stateLabel}: OFF",
            priority: 80,
            textColor: isOn ? Color.green : Color.red,
            pulse: isOn));

        _promptActions.Add(new PromptAction(
            isOn ? "Press T to Turn Off" : "Press T to Turn On",
            priority: 85));
    }

    private void UpdateWorldItemHighlight(in InteractionHoverTarget target)
    {
        if (target.Pickup is WorldItem worldItem)
        {
            SetWorldItemHighlight(worldItem);
            return;
        }

        ClearWorldItemHighlight();
    }

    private void SetWorldItemHighlight(WorldItem item)
    {
        if (_highlightedWorldItem == item)
            return;

        ClearWorldItemHighlight();

        _highlightedWorldItem = item;

        if (_highlightedWorldItem != null)
            _highlightedWorldItem.SetHighlighted(true);
    }

    private void ClearWorldItemHighlight()
    {
        if (_highlightedWorldItem == null)
            return;

        _highlightedWorldItem.SetHighlighted(false);
        _highlightedWorldItem = null;
    }

    private void HidePromptAndHighlight()
    {
        ClearWorldItemHighlight();
        promptUI.Hide();
    }

    private static string CleanObjectName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string s = raw.Trim();
        s = s.Replace("(Clone)", "");
        s = s.Replace("_", " ");

        if (s.StartsWith("RackVisual Item ", System.StringComparison.OrdinalIgnoreCase))
        {
            int lastSpace = s.LastIndexOf(' ');
            if (lastSpace >= 0 && lastSpace < s.Length - 1)
                s = s.Substring(lastSpace + 1);
        }

        return s.Trim();
    }

    private void DebugPrompt(string message)
    {
        if (!debugPromptTargets)
            return;

        if (debugPromptEveryNFrames > 1 &&
            Time.frameCount % debugPromptEveryNFrames != 0)
            return;

        Debug.Log($"[InteractPromptDriver:{name}] {message}", this);
    }

    private static string DescribePromptTarget(object target)
    {
        if (target == null)
            return "null";

        if (target is MonoBehaviour mb)
            return $"{target.GetType().Name}('{mb.name}')";

        return target.GetType().Name;
    }
}
