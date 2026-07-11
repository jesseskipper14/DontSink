using System.Collections.Generic;
using UnityEngine;

public enum CreatureThreatKind
{
    Generic = 0,
    Player = 1,
    Boat = 2,
    Predator = 3,
    Noise = 4,
    Light = 5,
    Weird = 99
}

[DisallowMultipleComponent]
public class CreatureThreatSource : MonoBehaviour
{
    [Header("Threat")]
    [SerializeField] private CreatureThreatKind kind = CreatureThreatKind.Generic;
    [SerializeField] private bool activeThreat = true;

    [SerializeField, Min(0f)] private float radius = 5f;
    [SerializeField, Min(0f)] private float strength = 1f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmosAlways = false;

    public CreatureThreatKind Kind => kind;
    public bool IsActiveThreat => activeThreat && isActiveAndEnabled;
    public float Radius => Mathf.Max(0f, radius);
    public float Strength => Mathf.Max(0f, strength);
    public Vector2 Position => transform.position;

    protected virtual void OnEnable()
    {
        CreatureThreatRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        CreatureThreatRegistry.Unregister(this);
    }

    [ContextMenu("Log Threat Registry")]
    private void LogThreatRegistryContext()
    {
        CreatureThreatRegistry.LogSources();
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmosAlways)
            return;

        DrawThreatGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawThreatGizmo();
    }

    private void DrawThreatGizmo()
    {
        Gizmos.color = IsActiveThreat
            ? new Color(1f, 0.35f, 0.2f, 0.35f)
            : new Color(0.4f, 0.4f, 0.4f, 0.25f);

        Gizmos.DrawWireSphere(transform.position, radius);
    }
}

public static class CreatureThreatRegistry
{
    private static readonly List<CreatureThreatSource> Sources = new(32);

    public static IReadOnlyList<CreatureThreatSource> AllSources => Sources;

    public static int ActiveSourceCount
    {
        get
        {
            int count = 0;

            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                CreatureThreatSource source = Sources[i];

                if (source == null)
                {
                    Sources.RemoveAt(i);
                    continue;
                }

                if (source.IsActiveThreat)
                    count++;
            }

            return count;
        }
    }

    public static void Register(CreatureThreatSource source)
    {
        if (source == null)
            return;

        if (!Sources.Contains(source))
            Sources.Add(source);
    }

    public static void Unregister(CreatureThreatSource source)
    {
        if (source == null)
            return;

        Sources.Remove(source);
    }

    public static bool TryGetStrongestThreat(
        Vector2 position,
        float observerNoticeRadius,
        out CreatureThreatSource strongest,
        out Vector2 fleeDirection,
        out float threat01)
    {
        strongest = null;
        fleeDirection = Vector2.zero;
        threat01 = 0f;

        float noticeRadius = Mathf.Max(0f, observerNoticeRadius);
        if (noticeRadius <= 0f)
            return false;

        float bestScore = 0f;
        Vector2 bestAway = Vector2.zero;

        for (int i = Sources.Count - 1; i >= 0; i--)
        {
            CreatureThreatSource source = Sources[i];

            if (source == null)
            {
                Sources.RemoveAt(i);
                continue;
            }

            if (!source.IsActiveThreat)
                continue;

            float effectiveRadius = Mathf.Min(noticeRadius, source.Radius);
            if (effectiveRadius <= 0f)
                continue;

            Vector2 awayFromThreat = position - source.Position;
            float dist = awayFromThreat.magnitude;

            if (dist > effectiveRadius)
                continue;

            float closeness01 = 1f - Mathf.Clamp01(dist / effectiveRadius);
            float score = closeness01 * Mathf.Max(0.01f, source.Strength);

            if (score <= bestScore)
                continue;

            bestScore = score;
            strongest = source;

            if (dist > 0.001f)
                bestAway = awayFromThreat / dist;
            else
                bestAway = Random.insideUnitCircle.normalized;
        }

        if (strongest == null)
            return false;

        fleeDirection = bestAway.sqrMagnitude > 0.001f
            ? bestAway.normalized
            : Vector2.right;

        threat01 = Mathf.Clamp01(bestScore);
        return true;
    }

    public static void LogSources()
    {
        Debug.Log($"[CreatureThreatRegistry] sources={Sources.Count}, active={ActiveSourceCount}");

        for (int i = Sources.Count - 1; i >= 0; i--)
        {
            CreatureThreatSource source = Sources[i];

            if (source == null)
            {
                Sources.RemoveAt(i);
                continue;
            }

            Debug.Log(
                $"[CreatureThreatRegistry] {i}: " +
                $"name='{source.name}', kind={source.Kind}, active={source.IsActiveThreat}, " +
                $"radius={source.Radius:F2}, strength={source.Strength:F2}, pos={source.Position}",
                source);
        }
    }
}