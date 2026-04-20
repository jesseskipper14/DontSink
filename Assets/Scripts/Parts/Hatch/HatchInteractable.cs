using UnityEngine;

[DisallowMultipleComponent]
public sealed class HatchInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Refs")]
    [SerializeField] private HatchRuntime hatchRuntime;
    [SerializeField] private Transform promptAnchor;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private bool allowInteractWhenOpen = true;
    [SerializeField] private bool allowInteractWhenClosed = true;

    public int InteractionPriority => interactionPriority;

    private void Reset()
    {
        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Awake()
    {
        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (promptAnchor == null)
            promptAnchor = transform;

        if (hatchRuntime == null)
        {
            Debug.LogError("[HatchInteractable] Missing HatchRuntime.", this);
            enabled = false;
        }
    }

    public bool CanInteract(in InteractContext context)
    {
        if (hatchRuntime == null)
            return false;

        if (hatchRuntime.IsOpen && !allowInteractWhenOpen)
            return false;

        if (!hatchRuntime.IsOpen && !allowInteractWhenClosed)
            return false;

        return hatchRuntime.CanToggle(out _);
    }

    public void Interact(in InteractContext context)
    {
        if (hatchRuntime == null)
            return;

        if (!hatchRuntime.CanToggle(out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[HatchInteractable] Toggle denied: {reason}", this);
            return;
        }

        hatchRuntime.Toggle();
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (hatchRuntime == null)
            return "Use Hatch";

        return hatchRuntime.IsOpen ? "Close Hatch" : "Open Hatch";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }
}