using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayInputBlocker : MonoBehaviour
{
    public static GameplayInputBlocker I { get; private set; }

    private readonly HashSet<object> _blockers = new();

    public static bool IsBlocked => I != null && I._blockers.Count > 0;

    private void Awake()
    {
        I = this;
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;
    }

    public static void Push(object owner)
    {
        if (owner == null)
            return;

        GameplayInputBlocker blocker = GetOrFind();
        if (blocker == null)
            return;

        blocker._blockers.Add(owner);
    }

    public static void Pop(object owner)
    {
        if (owner == null || I == null)
            return;

        I._blockers.Remove(owner);
    }

    public static GameplayInputBlocker GetOrFind()
    {
        if (I != null)
            return I;

        I = FindAnyObjectByType<GameplayInputBlocker>(FindObjectsInactive.Include);
        return I;
    }

    public static GameplayInputBlocker GetOrCreate()
    {
        if (I != null)
            return I;

        GameObject go = new GameObject(nameof(GameplayInputBlocker));
        return go.AddComponent<GameplayInputBlocker>();
    }
}