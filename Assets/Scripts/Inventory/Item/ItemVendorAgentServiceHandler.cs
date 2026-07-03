using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemVendorAgentServiceHandler : AgentServiceHandler
{
    [Header("Refs")]
    [SerializeField] private ItemVendorOverlayRunner runner;

    private void Awake()
    {
        if (runner == null)
            runner = FindFirstObjectByType<ItemVendorOverlayRunner>();

        if (runner == null)
            Debug.LogWarning("[ItemVendorAgentServiceHandler] Missing ItemVendorOverlayRunner.", this);
    }

    public override bool CanHandle(AgentServiceDefinition service)
    {
        return service != null && service.Kind == AgentServiceKind.ItemVendor;
    }

    public override bool TryHandle(AgentServiceContext context, out AgentServiceResult result)
    {
        ItemVendorServiceDefinition itemVendor = context.Service as ItemVendorServiceDefinition;

        if (itemVendor == null)
        {
            result = AgentServiceResult.Unhandled("Service was not an ItemVendorServiceDefinition.");
            return false;
        }

        if (runner == null)
        {
            result = AgentServiceResult.Unhandled("Missing ItemVendorOverlayRunner.");
            return false;
        }

        if (!runner.Open(itemVendor, context))
        {
            result = AgentServiceResult.Unhandled("Item vendor overlay failed to open.");
            return false;
        }

        result = AgentServiceResult.Handled($"Opened item vendor for {context.Agent.DisplayName}.");
        return true;
    }
}