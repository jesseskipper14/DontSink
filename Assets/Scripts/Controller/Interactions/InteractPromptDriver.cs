using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class InteractPromptDriver : MonoBehaviour
{
    [SerializeField] private Interactor2D interactor;
    [SerializeField] private InteractPromptUI promptUI;

    [Header("Multiplayer (Optional)")]
    [Tooltip("If set, prompts will only show when this returns IsLocal=true. Leave null for singleplayer.")]
    [SerializeField] private MonoBehaviour localAuthoritySource; // should implement ILocalPlayerAuthority

    [Header("Fallback")]
    [SerializeField] private string defaultVerb = "Interact";

    private ILocalPlayerAuthority _localAuth;

    // Interactables we interacted with recently; prompt stays hidden until we leave range.
    private readonly HashSet<IInteractable> _suppressedInteract = new();

    // Pickup targets we picked up recently; highlight stays hidden until we leave range.
    private readonly HashSet<IPickupInteractable> _suppressedPickup = new();

    private WorldItem _highlightedWorldItem;

    private void Reset()
    {
        interactor = GetComponent<Interactor2D>();
        promptUI = FindAnyObjectByType<InteractPromptUI>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (interactor == null) interactor = GetComponent<Interactor2D>();
        if (promptUI == null) promptUI = FindAnyObjectByType<InteractPromptUI>(FindObjectsInactive.Include);

        _localAuth = localAuthoritySource as ILocalPlayerAuthority;

        if (localAuthoritySource != null && _localAuth == null)
            Debug.LogError($"{name}: localAuthoritySource must implement ILocalPlayerAuthority.", this);

        if (promptUI != null) promptUI.Hide();
    }

    private void OnEnable()
    {
        if (interactor != null)
        {
            interactor.OnInteracted += HandleInteracted;
            interactor.OnPickedUp += HandlePickedUp;
        }
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

    private void LateUpdate()
    {
        if (promptUI == null || interactor == null)
            return;

        if (_localAuth != null && !_localAuth.IsLocal)
        {
            ClearWorldItemHighlight();
            promptUI.Hide();
            return;
        }

        if (_suppressedInteract.Count > 0)
            _suppressedInteract.RemoveWhere(t => t == null || !interactor.IsCandidatePresent(t));

        if (_suppressedPickup.Count > 0)
            _suppressedPickup.RemoveWhere(t => t == null || !interactor.IsPickupCandidatePresent(t));

        bool hasInteract = interactor.TryGetBestTarget(out var interactTarget, out var interactCtx) && interactTarget != null;
        bool hasPickup = interactor.TryGetBestPickupTarget(out var pickupTarget, out var pickupCtx) && pickupTarget != null;

        // Pickup lane: highlight world items only.
        if (hasPickup && !_suppressedPickup.Contains(pickupTarget) && pickupTarget is WorldItem worldItem)
            SetWorldItemHighlight(worldItem);
        else
            ClearWorldItemHighlight();

        // Interact lane: show normal prompt UI.
        if (!hasInteract || _suppressedInteract.Contains(interactTarget))
        {
            promptUI.Hide();
            return;
        }

        string verb = defaultVerb;
        if (interactTarget is IInteractPromptProvider provider)
        {
            var v = provider.GetPromptVerb(interactCtx);
            if (!string.IsNullOrWhiteSpace(v))
                verb = v;
        }

        Vector3 pos = (interactTarget as MonoBehaviour) != null
            ? ((MonoBehaviour)interactTarget).transform.position
            : (Vector3)interactCtx.Origin;

        if (interactTarget is IInteractPromptProvider provider2)
        {
            var anchor = provider2.GetPromptAnchor();
            if (anchor != null)
                pos = anchor.position;
        }

        promptUI.Show(verb, pos);
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
        if (_highlightedWorldItem != null)
        {
            _highlightedWorldItem.SetHighlighted(false);
            _highlightedWorldItem = null;
        }
    }
}