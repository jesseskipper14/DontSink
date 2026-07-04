using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Services/Boat Vendor Placeholder Service")]
public sealed class BoatVendorPlaceholderServiceDefinition : AgentServiceDefinition
{
    [Header("Boats Tab")]
    [TextArea(2, 6)]
    [SerializeField]
    private string boatsPlaceholderMessage =
        "Boat sales are pending development. Catalog entries are shown for planning only.";

    [Header("Modules Tab")]
    [Tooltip("Multiplies module ItemDefinition.BasePrice when the boat vendor sells modules to the player.")]
    [SerializeField, Min(0f)]
    private float moduleSellPriceMultiplier = 1f;

    [Tooltip("Multiplies module sell-back value when the boat vendor buys modules from the player.")]
    [SerializeField, Min(0f)]
    private float moduleBuyFromPlayerMultiplier = 1f;

    [Tooltip("-1 means infinite stock for v0.")]
    [SerializeField]
    private int defaultModuleStock = -1;

    [Header("Builder Tab")]
    [TextArea(2, 8)]
    [SerializeField]
    private string builderPlaceholderMessage =
        "Runtime boat builder is pending development. Later this will open the boat customization/build/edit flow.";

    public override AgentServiceKind Kind => AgentServiceKind.BoatVendorPlaceholder;

    public string BoatsPlaceholderMessage => boatsPlaceholderMessage;
    public float ModuleSellPriceMultiplier => Mathf.Max(0f, moduleSellPriceMultiplier);
    public float ModuleBuyFromPlayerMultiplier => Mathf.Max(0f, moduleBuyFromPlayerMultiplier);
    public int DefaultModuleStock => defaultModuleStock;
    public string BuilderPlaceholderMessage => builderPlaceholderMessage;

    public override AgentServiceResult Run(AgentServiceContext context)
    {
        if (AgentServiceDispatcher.TryDispatch(context, out AgentServiceResult result))
            return result;

        string agentName = context.Agent != null ? context.Agent.DisplayName : "Boat Vendor";
        Debug.Log($"{agentName}: {boatsPlaceholderMessage}", context.Agent);

        return AgentServiceResult.Handled(boatsPlaceholderMessage);
    }
}