using System.Collections.Generic;

/// <summary>
/// Narrow gameplay-interaction gate.
///
/// Unlike GameplayInputBlocker, this only suppresses ordinary interaction
/// intents. Movement and other gameplay controls remain available. Systems
/// such as placement modes can temporarily claim mouse/interaction input
/// without opening a full UI modal.
/// </summary>
public static class InteractionInputBlocker
{
    private static readonly HashSet<object> Blockers = new();

    public static bool IsBlocked => Blockers.Count > 0;

    public static void Push(object owner)
    {
        if (owner != null)
            Blockers.Add(owner);
    }

    public static void Pop(object owner)
    {
        if (owner != null)
            Blockers.Remove(owner);
    }
}
