#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SpanRepairUtility
{
    private const float MinRepairPieceWidth = 0.1f;
    private const float IntervalMergeEpsilon = 0.0001f;

    private struct Interval
    {
        public float Start;
        public float End;

        public Interval(float start, float end)
        {
            Start = Mathf.Min(start, end);
            End = Mathf.Max(start, end);
        }

        public bool IsValid => End - Start > MinRepairPieceWidth;
    }

    public static bool RepairFromSelectedFloor(FloorSegmentAuthoring selectedFloor)
    {
        if (selectedFloor == null)
        {
            Debug.LogWarning("[SpanRepair] No FloorSegmentAuthoring selected.");
            return false;
        }

        SplitSpanRecord record = selectedFloor.GetComponent<SplitSpanRecord>();
        if (record == null)
        {
            Debug.LogWarning("[SpanRepair] Selected floor has no SplitSpanRecord.", selectedFloor);
            return false;
        }

        if (!record.TryGetRoot(out Transform spanRoot) || spanRoot == null)
        {
            Debug.LogWarning("[SpanRepair] SplitSpanRecord has no valid span root.", selectedFloor);
            return false;
        }

        string spanId = record.SpanId;
        if (string.IsNullOrWhiteSpace(spanId))
        {
            Debug.LogWarning("[SpanRepair] SplitSpanRecord has no spanId.", selectedFloor);
            return false;
        }

        float originalStart = Mathf.Min(record.OriginalLocalStartX, record.OriginalLocalEndX);
        float originalEnd = Mathf.Max(record.OriginalLocalStartX, record.OriginalLocalEndX);
        float originalCenterY = record.OriginalLocalCenterY;
        string sourceKind = record.SourceKind;

        if (originalEnd - originalStart <= MinRepairPieceWidth)
        {
            Debug.LogWarning("[SpanRepair] Original span width is too small to repair.", selectedFloor);
            return false;
        }

        Object logContext = spanRoot != null ? spanRoot.gameObject : selectedFloor.gameObject;

        FloorSegmentAuthoring[] allFloors = spanRoot.GetComponentsInChildren<FloorSegmentAuthoring>(true);
        List<FloorSegmentAuthoring> lineageFloors = new List<FloorSegmentAuthoring>();

        for (int i = 0; i < allFloors.Length; i++)
        {
            FloorSegmentAuthoring floor = allFloors[i];
            if (floor == null)
                continue;

            SplitSpanRecord floorRecord = floor.GetComponent<SplitSpanRecord>();
            if (floorRecord == null)
                continue;

            if (floorRecord.SpanId != spanId)
                continue;

            lineageFloors.Add(floor);
        }

        if (lineageFloors.Count == 0)
        {
            Debug.LogWarning($"[SpanRepair] No lineage floors found for spanId '{spanId}'.", logContext);
            return false;
        }

        SpanRepairBlocker[] allBlockers = spanRoot.GetComponentsInChildren<SpanRepairBlocker>(true);
        List<Interval> blocked = new List<Interval>();

        for (int i = 0; i < allBlockers.Length; i++)
        {
            SpanRepairBlocker blocker = allBlockers[i];
            if (blocker == null)
                continue;

            if (blocker.SpanId != spanId)
                continue;

            Interval blockerInterval = new Interval(blocker.LocalStartX, blocker.LocalEndX);

            blockerInterval.Start = Mathf.Max(blockerInterval.Start, originalStart);
            blockerInterval.End = Mathf.Min(blockerInterval.End, originalEnd);

            if (blockerInterval.IsValid)
                blocked.Add(blockerInterval);
        }

        List<Interval> mergedBlocked = MergeIntervals(blocked);
        List<Interval> freeIntervals = ComputeFreeIntervals(originalStart, originalEnd, mergedBlocked);

        if (freeIntervals.Count == 0)
        {
            Debug.LogWarning(
                $"[SpanRepair] No free intervals remain for spanId '{spanId}'. Repair would remove all segments.",
                logContext);
            return false;
        }

        FloorSegmentAuthoring templateFloor = lineageFloors[0];
        Transform parent = templateFloor.transform.parent != null ? templateFloor.transform.parent : spanRoot;
        Quaternion rotation = templateFloor.transform.rotation;
        string templateFloorName = templateFloor.name;

        GameObject templateSource = null;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Span");

        try
        {
            templateSource = Object.Instantiate(templateFloor.gameObject);
            templateSource.name = templateFloor.gameObject.name + "_RepairTemplate";
            templateSource.hideFlags = HideFlags.HideAndDontSave;

            FloorSegmentAuthoring templateSourceFloor = templateSource.GetComponent<FloorSegmentAuthoring>();
            if (templateSourceFloor == null)
            {
                Object.DestroyImmediate(templateSource);
                Debug.LogWarning("[SpanRepair] Temporary repair template clone is missing FloorSegmentAuthoring.", logContext);
                return false;
            }

            for (int i = 0; i < lineageFloors.Count; i++)
            {
                FloorSegmentAuthoring floor = lineageFloors[i];
                if (floor == null)
                    continue;

                Undo.DestroyObjectImmediate(floor.gameObject);
            }

            int rebuiltCount = 0;

            for (int i = 0; i < freeIntervals.Count; i++)
            {
                Interval interval = freeIntervals[i];
                float width = interval.End - interval.Start;

                if (width <= MinRepairPieceWidth)
                    continue;

                float localCenterX = (interval.Start + interval.End) * 0.5f;
                Vector3 worldCenter = spanRoot.TransformPoint(new Vector3(localCenterX, originalCenterY, 0f));

                GameObject newPiece = Object.Instantiate(
                    templateSource,
                    worldCenter,
                    rotation,
                    parent);

                Undo.RegisterCreatedObjectUndo(newPiece, "Create repaired floor segment");

                newPiece.name = BuildRepairedPieceName(templateFloorName, rebuiltCount + 1);
                newPiece.hideFlags = HideFlags.None;

                FloorSegmentAuthoring newFloor = newPiece.GetComponent<FloorSegmentAuthoring>();
                if (newFloor == null)
                {
                    Debug.LogWarning("[SpanRepair] Repaired clone missing FloorSegmentAuthoring. Destroying invalid clone.", newPiece);
                    Undo.DestroyObjectImmediate(newPiece);
                    continue;
                }

                RecordFloorSegmentForUndo(newFloor, "Configure repaired floor segment");
                newFloor.ApplyWidth(width);
                newFloor.SetWorldCenterXPreservingColliderOffset(worldCenter.x);

                SplitSpanRecord newRecord = newPiece.GetComponent<SplitSpanRecord>();
                if (newRecord == null)
                {
                    newRecord = newPiece.AddComponent<SplitSpanRecord>();
                    Undo.RegisterCreatedObjectUndo(newRecord, "Add SplitSpanRecord");
                }

                newRecord.InitializeNewRootRecord(
                    spanId,
                    spanRoot,
                    originalStart,
                    originalEnd,
                    originalCenterY,
                    sourceKind);

                EditorUtility.SetDirty(newFloor);
                EditorUtility.SetDirty(newPiece);
                EditorUtility.SetDirty(newRecord);

                rebuiltCount++;
            }

            if (templateSource != null)
                Object.DestroyImmediate(templateSource);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[SpanRepair] Repaired span '{spanId}'. Blocked={mergedBlocked.Count}, rebuiltPieces={rebuiltCount}.",
                logContext);

            return true;
        }
        catch (System.Exception ex)
        {
            if (templateSource != null)
                Object.DestroyImmediate(templateSource);

            Debug.LogError($"[SpanRepair] Exception while repairing span '{spanId}': {ex}", logContext);
            return false;
        }
    }

    private static List<Interval> MergeIntervals(List<Interval> intervals)
    {
        List<Interval> result = new List<Interval>();
        if (intervals == null || intervals.Count == 0)
            return result;

        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));

        Interval current = intervals[0];

        for (int i = 1; i < intervals.Count; i++)
        {
            Interval next = intervals[i];

            if (next.Start <= current.End + IntervalMergeEpsilon)
            {
                current.End = Mathf.Max(current.End, next.End);
            }
            else
            {
                if (current.IsValid)
                    result.Add(current);

                current = next;
            }
        }

        if (current.IsValid)
            result.Add(current);

        return result;
    }

    private static List<Interval> ComputeFreeIntervals(float originalStart, float originalEnd, List<Interval> blocked)
    {
        List<Interval> free = new List<Interval>();

        float cursor = originalStart;

        for (int i = 0; i < blocked.Count; i++)
        {
            Interval b = blocked[i];

            if (b.Start > cursor + IntervalMergeEpsilon)
            {
                Interval freeInterval = new Interval(cursor, b.Start);
                if (freeInterval.IsValid)
                    free.Add(freeInterval);
            }

            cursor = Mathf.Max(cursor, b.End);
        }

        if (originalEnd > cursor + IntervalMergeEpsilon)
        {
            Interval tail = new Interval(cursor, originalEnd);
            if (tail.IsValid)
                free.Add(tail);
        }

        return free;
    }

    private static void RecordFloorSegmentForUndo(FloorSegmentAuthoring floor, string label)
    {
        if (floor == null)
            return;

        Undo.RecordObject(floor, label);
        Undo.RecordObject(floor.transform, label);

        if (floor.FloorCollider != null)
            Undo.RecordObject(floor.FloorCollider, label);

        if (floor.SpriteRenderer != null)
        {
            Undo.RecordObject(floor.SpriteRenderer, label);
            Undo.RecordObject(floor.SpriteRenderer.transform, label);
        }

        if (floor.ResizableSegment != null)
        {
            Undo.RecordObject(floor.ResizableSegment, label);

            if (floor.ResizableSegment.BoxCollider != null)
                Undo.RecordObject(floor.ResizableSegment.BoxCollider, label);

            if (floor.ResizableSegment.SpriteRenderer != null)
            {
                Undo.RecordObject(floor.ResizableSegment.SpriteRenderer, label);
                Undo.RecordObject(floor.ResizableSegment.SpriteRenderer.transform, label);
            }
        }
    }

    public static int RepairAllSpansUnderRoot(Transform root)
    {
        if (root == null)
        {
            Debug.LogWarning("[SpanRepair] RepairAllSpansUnderRoot called with null root.");
            return 0;
        }

        SplitSpanRecord[] records = root.GetComponentsInChildren<SplitSpanRecord>(true);
        HashSet<string> spanIds = new HashSet<string>();

        for (int i = 0; i < records.Length; i++)
        {
            SplitSpanRecord record = records[i];
            if (record == null)
                continue;

            if (string.IsNullOrWhiteSpace(record.SpanId))
                continue;

            spanIds.Add(record.SpanId);
        }

        int repairedCount = 0;

        foreach (string spanId in spanIds)
        {
            if (RepairSpanById(root, spanId))
                repairedCount++;
        }

        Debug.Log($"[SpanRepair] Repair All complete under '{root.name}'. Repaired {repairedCount}/{spanIds.Count} spans.", root);
        return repairedCount;
    }

    public static bool RepairSpanById(Transform root, string spanId)
    {
        if (root == null || string.IsNullOrWhiteSpace(spanId))
            return false;

        FloorSegmentAuthoring[] floors = root.GetComponentsInChildren<FloorSegmentAuthoring>(true);

        for (int i = 0; i < floors.Length; i++)
        {
            FloorSegmentAuthoring floor = floors[i];
            if (floor == null)
                continue;

            SplitSpanRecord record = floor.GetComponent<SplitSpanRecord>();
            if (record == null)
                continue;

            if (record.SpanId != spanId)
                continue;

            return RepairFromSelectedFloor(floor);
        }

        return false;
    }

    private static string GetCleanRepairBaseName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Segment";

        string name = rawName;

        bool changed;
        do
        {
            changed = false;

            if (name.EndsWith("_Left", System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_Left".Length);
                changed = true;
            }

            if (name.EndsWith("_Right", System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_Right".Length);
                changed = true;
            }

            int repairedIndex = name.LastIndexOf("_Repaired_", System.StringComparison.OrdinalIgnoreCase);
            if (repairedIndex >= 0)
            {
                string suffix = name.Substring(repairedIndex);
                if (System.Text.RegularExpressions.Regex.IsMatch(
                    suffix,
                    @"^_Repaired_\d+$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    name = name.Substring(0, repairedIndex);
                    changed = true;
                }
            }
        }
        while (changed);

        return string.IsNullOrWhiteSpace(name) ? "Segment" : name;
    }

    private static string BuildRepairedPieceName(string baseName, int index)
    {
        return $"{GetCleanRepairBaseName(baseName)}_Repaired_{index:00}";
    }
}
#endif