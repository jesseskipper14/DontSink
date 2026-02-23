using UnityEngine;

[DisallowMultipleComponent]
public sealed class MarketStallInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Interaction")]
    [SerializeField] private int priority = 50;          // below chairs (100), above random junk
    [SerializeField] private float maxUseDistance = 1.8f;

    [Header("Trade")]
    [SerializeField] private TradeWorldMapRunner tradeRunner;

    public int InteractionPriority => priority;
    public string GetPromptVerb(in InteractContext context) => "Trade";
    public Transform GetPromptAnchor() => transform;

    private void Awake()
    {
        if (tradeRunner == null)
            tradeRunner = FindAnyObjectByType<TradeWorldMapRunner>();

        if (tradeRunner == null)
            Debug.LogError($"{name}: Missing TradeWorldMapRunner in scene.", this);
    }

    public bool CanInteract(in InteractContext context)
    {
        if (tradeRunner == null) return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxUseDistance;
    }

    public void Interact(in InteractContext context)
    {
        tradeRunner?.OpenTrade();
    }
}
