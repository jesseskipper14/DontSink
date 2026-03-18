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
    private readonly HashSet<IInteractable> _suppressed = new();

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
            interactor.OnInteracted += HandleInteracted;
    }

    private void OnDisable()
    {
        if (interactor != null)
            interactor.OnInteracted -= HandleInteracted;

        ClearWorldItemHighlight();

        if (promptUI != null)
            promptUI.Hide();
    }

    private void HandleInteracted(IInteractable target)
    {
        if (target != null)
            _suppressed.Add(target);

        if (promptUI != null)
            promptUI.Hide();

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

        if (_suppressed.Count > 0)
        {
            _suppressed.RemoveWhere(t => t == null || !interactor.IsCandidatePresent(t));
        }

        if (!interactor.TryGetBestTarget(out var target, out var ctx) || target == null)
        {
            ClearWorldItemHighlight();
            promptUI.Hide();
            return;
        }

        if (_suppressed.Contains(target))
        {
            ClearWorldItemHighlight();
            promptUI.Hide();
            return;
        }

        // Pickup items: highlight only, no giant prompt box.
        if (target is WorldItem worldItem)
        {
            SetWorldItemHighlight(worldItem);
            promptUI.Hide();
            return;
        }

        // Non-world-item interactables: normal prompt flow.
        ClearWorldItemHighlight();

        string verb = defaultVerb;
        if (target is IInteractPromptProvider provider)
        {
            var v = provider.GetPromptVerb(ctx);
            if (!string.IsNullOrWhiteSpace(v)) verb = v;
        }

        Vector3 pos = (target as MonoBehaviour) != null
            ? ((MonoBehaviour)target).transform.position
            : (Vector3)ctx.Origin;

        if (target is IInteractPromptProvider provider2)
        {
            var anchor = provider2.GetPromptAnchor();
            if (anchor != null) pos = anchor.position;
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