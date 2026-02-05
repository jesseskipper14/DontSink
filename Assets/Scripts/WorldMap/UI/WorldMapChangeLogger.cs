using System.Collections.Generic;
using UnityEngine;

public class WorldMapChangeLogger : MonoBehaviour
{
    [Header("Milestone Logging")]
    [Tooltip("Logs when a value crosses multiples of this step (ex: 0.1 logs at 1.5, 1.6, etc).")]
    [Range(0.01f, 1f)] public float milestoneStep = 0.1f;

    [Tooltip("If true, only logs upward crossings. If false, logs both up and down.")]
    public bool upwardOnly = false;

    [Tooltip("Optional: only log these kinds. Leave both true for now.")]
    public bool logStats = true;
    public bool logBuildings = true;

    // key: nodeId|kind|key  -> last bucket index
    private readonly Dictionary<string, int> _lastBucket = new();

    private void OnEnable()
    {
        WorldMapMessageBus.OnChange += Handle;
        _lastBucket.Clear();
    }

    private void OnDisable()
    {
        WorldMapMessageBus.OnChange -= Handle;
        _lastBucket.Clear();
    }

    private void Handle(WorldMapChange change)
    {
        if (change.kind == WorldMapChangeKind.StatChanged && !logStats) return;
        if (change.kind == WorldMapChangeKind.BuildingChanged && !logBuildings) return;

        float step = Mathf.Max(0.0001f, milestoneStep);

        // Determine which buckets old/new are in
        int oldBucket = Bucket(change.oldValue, step);
        int newBucket = Bucket(change.newValue, step);

        if (oldBucket == newBucket) return;

        if (upwardOnly && newBucket < oldBucket) return;

        string id = MakeId(change);

        // Initialize with old bucket if first time seeing this signal
        if (!_lastBucket.TryGetValue(id, out int last))
        {
            last = oldBucket;
            _lastBucket[id] = last;
        }

        // Only log if we crossed into a different bucket than last logged.
        // This prevents multiple logs if something oscillates within the same bucket.
        if (newBucket == last) return;

        _lastBucket[id] = newBucket;

        // Build a nicer message that shows the milestone crossed
        float milestoneValue = newBucket * step;
        Debug.Log($"[Milestone] #{change.nodeId} {change.nodeName} | {change.key} crossed {milestoneValue:0.0} (now {change.newValue:0.00})");
    }

    private static int Bucket(float value, float step)
    {
        // 0..4 range, but keep it generic
        return Mathf.FloorToInt(value / step);
    }

    private static string MakeId(WorldMapChange c)
    {
        return $"{c.nodeId}|{(int)c.kind}|{c.key}";
    }
}
