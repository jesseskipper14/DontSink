using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class InteractPromptDriver : MonoBehaviour
{
    [SerializeField] private Interactor2D interactor;
    [SerializeField] private InteractPromptUI promptUI;
    private readonly List<PromptAction> _promptActions = new();

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

        bool pickupVisible = hasPickup && !_suppressedPickup.Contains(pickupTarget);
        bool interactVisible = hasInteract;

        if (pickupVisible && pickupTarget is WorldItem worldItem)
            SetWorldItemHighlight(worldItem);
        else
            ClearWorldItemHighlight();

        if (!pickupVisible && !interactVisible)
        {
            promptUI.Hide();
            return;
        }

        Vector3 promptPos = transform.position;

        if (interactVisible && interactTarget is IInteractPromptProvider interactProvider)
        {
            Transform anchor = interactProvider.GetPromptAnchor();
            if (anchor != null)
                promptPos = anchor.position;
        }
        else if (pickupVisible && pickupTarget is IInteractPromptProvider pickupProvider)
        {
            Transform anchor = pickupProvider.GetPromptAnchor();
            if (anchor != null)
                promptPos = anchor.position;
        }
        else if (interactVisible && interactTarget is MonoBehaviour interactMb)
        {
            promptPos = interactMb.transform.position;
        }
        else if (pickupVisible && pickupTarget is MonoBehaviour pickupMb)
        {
            promptPos = pickupMb.transform.position;
        }

        _promptActions.Clear();

        if (interactVisible)
        {
            string interactVerb = "Interact";

            if (interactTarget is IInteractPromptProvider interactPromptProvider)
            {
                string v = interactPromptProvider.GetPromptVerb(interactCtx);
                if (!string.IsNullOrWhiteSpace(v))
                    interactVerb = v;
            }

            _promptActions.Add(new PromptAction($"Press E to {interactVerb}", priority: 100));
        }

        if (pickupVisible)
        {
            string pickupVerb = "Pick up";

            if (pickupTarget is IInteractPromptProvider pickupPromptProvider)
            {
                string v = pickupPromptProvider.GetPromptVerb(pickupCtx);
                if (!string.IsNullOrWhiteSpace(v))
                    pickupVerb = v;
            }

            bool isHoldPickup = pickupTarget.PickupMode == PickupInteractionMode.Hold;
            float progress = 0f;

            if (isHoldPickup && ReferenceEquals(interactor.ActiveHoldPickupTarget, pickupTarget))
                progress = interactor.ActiveHoldPickupProgress;

            _promptActions.Add(new PromptAction(
                isHoldPickup ? $"Hold F to {pickupVerb}" : $"Press F to {pickupVerb}",
                priority: 90,
                showProgress: isHoldPickup,
                progress01: progress));
        }

        promptUI.Show(promptPos, _promptActions);
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