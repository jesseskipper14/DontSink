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
    }

    private void HandleInteracted(IInteractable target)
    {
        if (target != null)
            _suppressed.Add(target);

        if (promptUI != null)
            promptUI.Hide();
    }

    private void LateUpdate()
    {
        if (promptUI == null || interactor == null)
            return;

        // Multiplayer gate: only show prompts for the local player.
        if (_localAuth != null && !_localAuth.IsLocal)
        {
            promptUI.Hide();
            return;
        }

        // Clear suppressions once the player leaves range of those interactables.
        if (_suppressed.Count > 0)
        {
            _suppressed.RemoveWhere(t => t == null || !interactor.IsCandidatePresent(t));
        }

        if (!interactor.TryGetBestTarget(out var target, out var ctx) || target == null)
        {
            promptUI.Hide();
            return;
        }

        // If we just interacted with this thing and haven't left range yet, keep prompt hidden.
        if (_suppressed.Contains(target))
        {
            promptUI.Hide();
            return;
        }

        // Prompt verb
        string verb = defaultVerb;
        if (target is IInteractPromptProvider provider)
        {
            var v = provider.GetPromptVerb(ctx);
            if (!string.IsNullOrWhiteSpace(v)) verb = v;
        }

        // Prompt anchor/position
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
}