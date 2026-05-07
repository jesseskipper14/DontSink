#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WallRepairUtility
{
    private const float MinRepairPieceHeight = 0.1f;
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

        public bool IsValid => End - Start > MinRepairPieceHeight;
    }

    public static bool RepairFromSelectedWall(WallSegmentAuthoring selectedWall)
    {
        if (selectedWall == null)
        {
            Debug.LogWarning("[WallRepair] No WallSegmentAuthoring selected.");
            return false;
        }

        WallSplitRecord record = selectedWall.GetComponent<WallSplitRecord>();
        if (record == null)
        {
            Debug.LogWarning("[WallRepair] Selected wall has no WallSplitRecord.", selectedWall);
            return false;
        }

        if (!record.TryGetRoot(out Transform spanRoot) || spanRoot == null)
        {
            Debug.LogWarning("[WallRepair] WallSplitRecord has no valid span root.", selectedWall);
            return false;
        }

        string spanId = record.SpanId;
        if (string.IsNullOrWhiteSpace(spanId))
        {
            Debug.LogWarning("[WallRepair] WallSplitRecord has no spanId.", selectedWall);
            return false;
        }

        float originalBottom = Mathf.Min(record.OriginalLocalBottomY, record.OriginalLocalTopY);
        float originalTop = Mathf.Max(record.OriginalLocalBottomY, record.OriginalLocalTopY);
        float originalCenterX = record.OriginalLocalCenterX;
        string sourceKind = record.SourceKind;

        if (originalTop - originalBottom <= MinRepairPieceHeight)
        {
            Debug.LogWarning("[WallRepair] Original wall span height is too small to repair.", selectedWall);
            return false;
        }

        Object logContext = spanRoot != null ? spanRoot.gameObject : selectedWall.gameObject;

        WallSegmentAuthoring[] allWalls = spanRoot.GetComponentsInChildren<WallSegmentAuthoring>(true);
        List<WallSegmentAuthoring> lineageWalls = new List<WallSegmentAuthoring>();

        for (int i = 0; i < allWalls.Length; i++)
        {
            WallSegmentAuthoring wall = allWalls[i];
            if (wall == null)
                continue;

            WallSplitRecord wallRecord = wall.GetComponent<WallSplitRecord>();
            if (wallRecord == null)
                continue;

            if (wallRecord.SpanId != spanId)
                continue;

            lineageWalls.Add(wall);
        }

        if (lineageWalls.Count == 0)
        {
            Debug.LogWarning($"[WallRepair] No lineage walls found for spanId '{spanId}'.", logContext);
            return false;
        }

        WallRepairBlocker[] allBlockers = spanRoot.GetComponentsInChildren<WallRepairBlocker>(true);
        List<Interval> blocked = new List<Interval>();

        for (int i = 0; i < allBlockers.Length; i++)
        {
            WallRepairBlocker blocker = allBlockers[i];
            if (blocker == null)
                continue;

            if (blocker.SpanId != spanId)
                continue;

            Interval interval = new Interval(blocker.LocalBottomY, blocker.LocalTopY);

            interval.Start = Mathf.Max(interval.Start, originalBottom);
            interval.End = Mathf.Min(interval.End, originalTop);

            if (interval.IsValid)
                blocked.Add(interval);
        }

        List<Interval> mergedBlocked = MergeIntervals(blocked);
        List<Interval> freeIntervals = ComputeFreeIntervals(originalBottom, originalTop, mergedBlocked);

        if (freeIntervals.Count == 0)
        {
            Debug.LogWarning(
                $"[WallRepair] No free intervals remain for spanId '{spanId}'. Repair would remove all wall segments.",
                logContext);
            return false;
        }

        WallSegmentAuthoring templateWall = lineageWalls[0];
        Transform parent = templateWall.transform.parent != null ? templateWall.transform.parent : spanRoot;
        Quaternion rotation = templateWall.transform.rotation;
        string templateWallName = templateWall.name;

        GameObject templateSource = null;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Wall Span");

        try
        {
            templateSource = UnityEngine.Object.Instantiate(templateWall.gameObject);
            templateSource.name = templateWall.gameObject.name + "_WallRepairTemplate";
            templateSource.hideFlags = HideFlags.HideAndDontSave;

            WallSegmentAuthoring templateSourceWall = templateSource.GetComponent<WallSegmentAuthoring>();
            if (templateSourceWall == null)
            {
                UnityEngine.Object.DestroyImmediate(templateSource);
                Debug.LogWarning("[WallRepair] Temporary repair template clone is missing WallSegmentAuthoring.", logContext);
                return false;
            }

            for (int i = 0; i < lineageWalls.Count; i++)
            {
                WallSegmentAuthoring wall = lineageWalls[i];
                if (wall == null)
                    continue;

                Undo.DestroyObjectImmediate(wall.gameObject);
            }

            int rebuiltCount = 0;

            for (int i = 0; i < freeIntervals.Count; i++)
            {
                Interval interval = freeIntervals[i];
                float height = interval.End - interval.Start;

                if (height <= MinRepairPieceHeight)
                    continue;

                float localCenterY = (interval.Start + interval.End) * 0.5f;
                Vector3 worldCenter = spanRoot.TransformPoint(new Vector3(originalCenterX, localCenterY, 0f));

                GameObject newPiece = UnityEngine.Object.Instantiate(
                    templateSource,
                    worldCenter,
                    rotation,
                    parent);

                Undo.RegisterCreatedObjectUndo(newPiece, "Create repaired wall segment");

                newPiece.name = BuildRepairedPieceName(templateWallName, rebuiltCount + 1);
                newPiece.hideFlags = HideFlags.None;

                WallSegmentAuthoring newWall = newPiece.GetComponent<WallSegmentAuthoring>();
                if (newWall == null)
                {
                    Debug.LogWarning("[WallRepair] Repaired clone missing WallSegmentAuthoring. Destroying invalid clone.", newPiece);
                    Undo.DestroyObjectImmediate(newPiece);
                    continue;
                }

                RecordWallSegmentForUndo(newWall, "Configure repaired wall segment");
                newWall.ApplyHeight(height);
                newWall.SetWorldCenterYPreservingColliderOffset(worldCenter.y);

                WallSplitRecord newRecord = newPiece.GetComponent<WallSplitRecord>();
                if (newRecord == null)
                {
                    newRecord = newPiece.AddComponent<WallSplitRecord>();
                    Undo.RegisterCreatedObjectUndo(newRecord, "Add WallSplitRecord");
                }

                newRecord.InitializeNewRootRecord(
                    spanId,
                    spanRoot,
                    originalBottom,
                    originalTop,
                    originalCenterX,
                    sourceKind);

                EditorUtility.SetDirty(newWall);
                EditorUtility.SetDirty(newPiece);
                EditorUtility.SetDirty(newRecord);

                rebuiltCount++;
            }

            if (templateSource != null)
                UnityEngine.Object.DestroyImmediate(templateSource);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                $"[WallRepair] Repaired wall span '{spanId}'. Blocked={mergedBlocked.Count}, rebuiltPieces={rebuiltCount}.",
                logContext);

            return true;
        }
        catch (System.Exception ex)
        {
            if (templateSource != null)
                UnityEngine.Object.DestroyImmediate(templateSource);

            Debug.LogError($"[WallRepair] Exception while repairing wall span '{spanId}': {ex}", logContext);
            return false;
        }
    }

    public static int RepairAllWallSpansUnderRoot(Transform root)
    {
        if (root == null)
        {
            Debug.LogWarning("[WallRepair] RepairAllWallSpansUnderRoot called with null root.");
            return 0;
        }

        WallSplitRecord[] records = root.GetComponentsInChildren<WallSplitRecord>(true);
        HashSet<string> spanIds = new HashSet<string>();

        for (int i = 0; i < records.Length; i++)
        {
            WallSplitRecord record = records[i];
            if (record == null)
                continue;

            if (string.IsNullOrWhiteSpace(record.SpanId))
                continue;

            spanIds.Add(record.SpanId);
        }

        int repairedCount = 0;

        foreach (string spanId in spanIds)
        {
            if (RepairWallSpanById(root, spanId))
                repairedCount++;
        }

        Debug.Log($"[WallRepair] Repair All complete under '{root.name}'. Repaired {repairedCount}/{spanIds.Count} wall spans.", root);
        return repairedCount;
    }

    public static bool RepairWallSpanById(Transform root, string spanId)
    {
        if (root == null || string.IsNullOrWhiteSpace(spanId))
            return false;

        WallSegmentAuthoring[] walls = root.GetComponentsInChildren<WallSegmentAuthoring>(true);

        for (int i = 0; i < walls.Length; i++)
        {
            WallSegmentAuthoring wall = walls[i];
            if (wall == null)
                continue;

            WallSplitRecord record = wall.GetComponent<WallSplitRecord>();
            if (record == null)
                continue;

            if (record.SpanId != spanId)
                continue;

            return RepairFromSelectedWall(wall);
        }

        return false;
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

    private static List<Interval> ComputeFreeIntervals(float originalBottom, float originalTop, List<Interval> blocked)
    {
        List<Interval> free = new List<Interval>();

        float cursor = originalBottom;

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

        if (originalTop > cursor + IntervalMergeEpsilon)
        {
            Interval tail = new Interval(cursor, originalTop);
            if (tail.IsValid)
                free.Add(tail);
        }

        return free;
    }

    private static void RecordWallSegmentForUndo(WallSegmentAuthoring wall, string label)
    {
        if (wall == null)
            return;

        Undo.RecordObject(wall, label);
        Undo.RecordObject(wall.transform, label);

        if (wall.WallCollider != null)
            Undo.RecordObject(wall.WallCollider, label);

        if (wall.SpriteRenderer != null)
        {
            Undo.RecordObject(wall.SpriteRenderer, label);
            Undo.RecordObject(wall.SpriteRenderer.transform, label);
        }

        if (wall.ResizableSegment != null)
        {
            Undo.RecordObject(wall.ResizableSegment, label);

            if (wall.ResizableSegment.BoxCollider != null)
                Undo.RecordObject(wall.ResizableSegment.BoxCollider, label);

            if (wall.ResizableSegment.SpriteRenderer != null)
            {
                Undo.RecordObject(wall.ResizableSegment.SpriteRenderer, label);
                Undo.RecordObject(wall.ResizableSegment.SpriteRenderer.transform, label);
            }
        }
    }

    private static string GetCleanRepairBaseName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Wall";

        string name = rawName;

        bool changed;
        do
        {
            changed = false;

            if (name.EndsWith("_Bottom", System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_Bottom".Length);
                changed = true;
            }

            if (name.EndsWith("_Top", System.StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_Top".Length);
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

        return string.IsNullOrWhiteSpace(name) ? "Wall" : name;
    }

    private static string BuildRepairedPieceName(string baseName, int index)
    {
        return $"{GetCleanRepairBaseName(baseName)}_Repaired_{index:00}";
    }
}
#endif