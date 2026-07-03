using System.Linq;
using UnityEngine;

public abstract class AgentServiceHandler : MonoBehaviour
{
    [Header("Dispatch")]
    [SerializeField] private int handlerPriority = 0;

    public int HandlerPriority => handlerPriority;

    public abstract bool CanHandle(AgentServiceDefinition service);
    public abstract bool TryHandle(AgentServiceContext context, out AgentServiceResult result);
}

public static class AgentServiceDispatcher
{
    public static bool TryDispatch(AgentServiceContext context, out AgentServiceResult result)
    {
        AgentServiceHandler[] handlers = Object.FindObjectsByType<AgentServiceHandler>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        foreach (AgentServiceHandler handler in handlers.OrderByDescending(h => h.HandlerPriority))
        {
            if (handler == null || !handler.CanHandle(context.Service))
                continue;

            if (handler.TryHandle(context, out result))
                return true;
        }

        result = AgentServiceResult.Unhandled();
        return false;
    }
}