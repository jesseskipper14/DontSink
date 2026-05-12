using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EscapeCloseRegistry : MonoBehaviour
{
    public static EscapeCloseRegistry I { get; private set; }

    private readonly List<IEscapeClosable> _closables = new();

    private void Awake()
    {
        I = this;
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;
    }

    public static EscapeCloseRegistry GetOrFind()
    {
        if (I != null)
            return I;

        I = FindAnyObjectByType<EscapeCloseRegistry>(FindObjectsInactive.Include);

        if (I != null)
            return I;

        Debug.LogError(
            "[EscapeCloseRegistry] No EscapeCloseRegistry exists in the scene. " +
            "Add _GameplayUIBootstrap with EscapeCloseRegistry.");

        return null;
    }

    public static EscapeCloseRegistry TryGetOrFind()
    {
        if (I != null)
            return I;

        I = FindAnyObjectByType<EscapeCloseRegistry>(FindObjectsInactive.Include);
        return I;
    }

    public void Register(IEscapeClosable closable)
    {
        if (closable == null)
            return;

        if (!_closables.Contains(closable))
            _closables.Add(closable);
    }

    public void Unregister(IEscapeClosable closable)
    {
        if (closable == null)
            return;

        _closables.Remove(closable);
    }

    public bool TryCloseTopmost()
    {
        IEscapeClosable best = null;
        int bestPriority = int.MinValue;
        int bestIndex = -1;

        for (int i = _closables.Count - 1; i >= 0; i--)
        {
            IEscapeClosable c = _closables[i];

            if (c == null)
            {
                _closables.RemoveAt(i);
                continue;
            }

            if (!c.IsEscapeOpen)
                continue;

            int priority = c.EscapePriority;

            if (best == null || priority > bestPriority || (priority == bestPriority && i > bestIndex))
            {
                best = c;
                bestPriority = priority;
                bestIndex = i;
            }
        }

        if (best == null)
            return false;

        return best.CloseFromEscape();
    }
}