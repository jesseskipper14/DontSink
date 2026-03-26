using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItemContainerInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [SerializeField] private WorldItem worldItem;
    [SerializeField] private float maxDistance = 1.5f;
    [SerializeField] private int interactionPriority = 5;
    [SerializeField] private Transform promptAnchor;

    [Header("Container UI")]
    [SerializeField] private float autoCloseDistance = 2.25f;

    public int InteractionPriority => interactionPriority;

    private void Awake()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();
    }

    public bool CanInteract(in InteractContext context)
    {
        if (worldItem == null || worldItem.Instance == null)
            return false;

        if (!worldItem.Instance.IsContainer || worldItem.Instance.ContainerState == null)
            return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxDistance;
    }

    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context))
            return;

        ExternalContainerOverlayUI overlay = Object.FindFirstObjectByType<ExternalContainerOverlayUI>();
        if (overlay == null)
        {
            Debug.LogWarning("[WorldItemContainerInteractable] No ExternalContainerOverlayUI found.");
            return;
        }

        ItemContainerState state = worldItem.Instance.ContainerState;
        if (state == null)
            return;

        if (overlay.IsOpen && overlay.CurrentState == state)
        {
            overlay.Close();
            return;
        }

        string title = worldItem.Item != null ? worldItem.Item.DisplayName : "Container";
        overlay.Open(title, state, transform, autoCloseDistance);
    }

    public string GetPromptVerb(in InteractContext context) => "Open";
    public Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
}