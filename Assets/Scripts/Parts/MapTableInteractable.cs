using UnityEngine;

[DisallowMultipleComponent]
public sealed class MapTableInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private int priority = 40;
    [SerializeField] private float maxUseDistance = 1.8f;

    [Header("Map")]
    [SerializeField] private MapOverlayToggleService mapService;
    [SerializeField] private bool toggle = true; // if false, always Open()

    public int InteractionPriority => priority;

    private void Awake()
    {
        if (mapService == null)
            mapService = FindAnyObjectByType<MapOverlayToggleService>();

        if (mapService == null)
            Debug.LogError($"{name}: Missing MapOverlayToggleService in scene.", this);
    }

    public bool CanInteract(in InteractContext context)
    {
        if (mapService == null) return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxUseDistance;
    }

    public void Interact(in InteractContext context)
    {
        if (toggle) mapService.Toggle(transform);
        else mapService.Open(transform);
    }
}
