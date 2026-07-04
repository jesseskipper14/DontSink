using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatVendorAgentServiceHandler : AgentServiceHandler
{
    [Header("Refs")]
    [SerializeField] private BoatVendorOverlayRunner runner;

    private void Awake()
    {
        if (runner == null)
            runner = FindFirstObjectByType<BoatVendorOverlayRunner>();

        if (runner == null)
            Debug.LogWarning("[BoatVendorAgentServiceHandler] Missing BoatVendorOverlayRunner.", this);
    }

    public override bool CanHandle(AgentServiceDefinition service)
    {
        return service != null && service.Kind == AgentServiceKind.BoatVendorPlaceholder;
    }

    public override bool TryHandle(AgentServiceContext context, out AgentServiceResult result)
    {
        BoatVendorPlaceholderServiceDefinition boatVendor =
            context.Service as BoatVendorPlaceholderServiceDefinition;

        if (boatVendor == null)
        {
            result = AgentServiceResult.Unhandled("Service was not a BoatVendorPlaceholderServiceDefinition.");
            return false;
        }

        if (runner == null)
        {
            result = AgentServiceResult.Unhandled("Missing BoatVendorOverlayRunner.");
            return false;
        }

        if (!runner.Open(boatVendor, context))
        {
            result = AgentServiceResult.Unhandled("Boat vendor overlay failed to open.");
            return false;
        }

        result = AgentServiceResult.Handled($"Opened boat vendor for {context.Agent.DisplayName}.");
        return true;
    }
}