#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{
    private const float DoorSplitMinPieceHeight = 0.1f;
    private const float DoorFloorSearchVerticalTolerance = 3.0f;
    private const float DoorFloorSearchXPadding = 0.35f;
    private const float DoorWallSearchHorizontalTolerance = 0.75f;

    private static GameObject TryPlaceDoorWithWallSplit(
        GameObject doorPrefab,
        Vector3 requestedWorldPos,
        Transform boatRoot,
        Transform fallbackParent)
    {
        if (doorPrefab == null)
            return null;

        DoorAuthoring doorAuthoring = doorPrefab.GetComponent<DoorAuthoring>();
        if (doorAuthoring == null)
            doorAuthoring = doorPrefab.GetComponentInChildren<DoorAuthoring>(true);

        if (doorAuthoring == null)
        {
            Debug.LogWarning("[BoatBuilder] Door prefab is missing DoorAuthoring. Cannot perform wall split placement.", doorPrefab);
            return null;
        }

        float blockerHeight = doorAuthoring.GetOpeningHeightWorld();
        float clearance = doorAuthoring.GetBuilderOpeningClearanceWorld();
        float wallOpeningHeight = blockerHeight + clearance * 2f;

        if (blockerHeight <= 0.01f || wallOpeningHeight <= 0.01f)
        {
            Debug.LogWarning("[BoatBuilder] Door opening height is invalid.", doorPrefab);
            return null;
        }

        Transform searchRoot = boatRoot != null ? boatRoot : fallbackParent;

        if (!TryFindBestFloorSnapForDoor(
                requestedWorldPos,
                searchRoot,
                out FloorSegmentAuthoring floor,
                out float floorTopY))
        {
            Debug.LogWarning("[BoatBuilder] No valid floor found for door snap. Door placement requires a nearby FloorSegmentAuthoring.", doorPrefab);
            return null;
        }

        if (!TryFindBestWallSplitForDoor(
                requestedWorldPos,
                floorTopY,
                wallOpeningHeight,
                searchRoot,
                out WallSegmentAuthoring wall,
                out WallSplitUtility.SplitResult split))
        {
            Debug.LogWarning("[BoatBuilder] No valid vertical wall found for door placement. Door placement requires a splittable WallSegmentAuthoring/vertical ResizableSegment2D.", doorPrefab);
            return null;
        }

        Transform actualParent = wall.transform.parent != null ? wall.transform.parent : fallbackParent;
        Quaternion baseRotation = wall.transform.rotation;

        WallSplitRecord wallRecord = GetOrCreateWallSplitRecord(wall);
        Transform spanRoot = wallRecord != null && wallRecord.TryGetRoot(out Transform resolvedRoot)
            ? resolvedRoot
            : actualParent;

        GameObject topPiece = null;

        if (split.HasBottomPiece && split.HasTopPiece)
        {
            topPiece = CreateResizedWallClone(
                wall,
                split.TopHeight,
                split.TopCenterY,
                actualParent,
                "_Top");

            if (topPiece == null)
            {
                Debug.LogWarning("[BoatBuilder] Failed to create top split wall piece. Original wall preserved.", wall);
                return null;
            }

            WallSegmentAuthoring topWall = topPiece.GetComponent<WallSegmentAuthoring>();
            if (topWall != null && wallRecord != null)
                InheritWallSplitRecordFromSource(topWall, wallRecord);
        }

        float blockerBottomY = floorTopY + clearance;

        Vector3 doorWorldPos = new Vector3(
            wall.WorldCenterX,
            blockerBottomY,
            requestedWorldPos.z);

        GameObject doorInstance = PlacePrefab(doorPrefab, doorWorldPos, actualParent);

        if (doorInstance == null)
        {
            if (topPiece != null)
                Undo.DestroyObjectImmediate(topPiece);

            Debug.LogWarning("[BoatBuilder] Door placement failed after wall split prep. Original wall preserved.", wall);
            return null;
        }

        doorInstance.transform.rotation = baseRotation;
        AlignDoorToWallOpening(doorInstance, wall.WorldCenterX, blockerBottomY);

        InitializePlacedDoor(doorInstance, boatRoot != null ? boatRoot : actualParent);

        if (wallRecord != null && spanRoot != null)
        {
            AttachWallRepairBlocker(
                doorInstance,
                wallRecord,
                spanRoot,
                split.OpeningBottomY,
                split.OpeningTopY,
                "Door");
        }

        RecordWallSegmentForUndo(wall, "Resize original wall into split piece");

        if (split.HasBottomPiece)
        {
            wall.ApplyHeight(split.BottomHeight);
            wall.SetWorldCenterYPreservingColliderOffset(split.BottomCenterY);

            Undo.RecordObject(wall.gameObject, "Rename bottom split wall piece");
            wall.gameObject.name = BuildWallSplitPieceName(wall.gameObject.name, "_Bottom");

            if (wallRecord != null)
                MarkWallAsSplitFragment(wall, wallRecord);
        }
        else if (split.HasTopPiece)
        {
            wall.ApplyHeight(split.TopHeight);
            wall.SetWorldCenterYPreservingColliderOffset(split.TopCenterY);

            Undo.RecordObject(wall.gameObject, "Rename top split wall piece");
            wall.gameObject.name = BuildWallSplitPieceName(wall.gameObject.name, "_Top");

            if (wallRecord != null)
                MarkWallAsSplitFragment(wall, wallRecord);
        }

        EditorUtility.SetDirty(wall);
        EditorUtility.SetDirty(wall.gameObject);

        if (topPiece != null)
            EditorUtility.SetDirty(topPiece);

        EditorUtility.SetDirty(doorInstance);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return doorInstance;
    }

    private static bool TryFindBestFloorSnapForDoor(
        Vector3 requestedWorldPos,
        Transform searchRoot,
        out FloorSegmentAuthoring bestFloor,
        out float bestFloorTopY)
    {
        bestFloor = null;
        bestFloorTopY = 0f;

        FloorSegmentAuthoring[] candidates = searchRoot != null
            ? searchRoot.GetComponentsInChildren<FloorSegmentAuthoring>(true)
            : UnityEngine.Object.FindObjectsByType<FloorSegmentAuthoring>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Length; i++)
        {
            FloorSegmentAuthoring floor = candidates[i];
            if (floor == null)
                continue;

            float leftX = floor.WorldLeftX - DoorFloorSearchXPadding;
            float rightX = floor.WorldRightX + DoorFloorSearchXPadding;

            if (requestedWorldPos.x < leftX || requestedWorldPos.x > rightX)
                continue;

            float floorTopY = GetFloorTopY(floor);
            float yDelta = Mathf.Abs(requestedWorldPos.y - floorTopY);

            if (yDelta > DoorFloorSearchVerticalTolerance)
                continue;

            float clampedX = Mathf.Clamp(requestedWorldPos.x, floor.WorldLeftX, floor.WorldRightX);
            float xDelta = Mathf.Abs(requestedWorldPos.x - clampedX);

            float score = yDelta + xDelta * 3f;

            if (score < bestScore)
            {
                bestScore = score;
                bestFloor = floor;
                bestFloorTopY = floorTopY;
            }
        }

        return bestFloor != null;
    }

    private static float GetFloorTopY(FloorSegmentAuthoring floor)
    {
        if (floor == null)
            return 0f;

        if (floor.FloorCollider != null)
            return floor.FloorCollider.bounds.max.y;

        return floor.WorldCenterY + floor.Height * 0.5f;
    }

    private static bool TryFindBestWallSplitForDoor(
        Vector3 requestedWorldPos,
        float openingBottomY,
        float openingHeight,
        Transform searchRoot,
        out WallSegmentAuthoring bestWall,
        out WallSplitUtility.SplitResult bestSplit)
    {
        bestWall = null;
        bestSplit = default;

        List<WallSegmentAuthoring> candidates = FindWallCandidatesForDoor(
            searchRoot,
            requestedWorldPos.x,
            openingBottomY,
            openingBottomY + openingHeight);

        float openingCenterY = openingBottomY + openingHeight * 0.5f;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            WallSegmentAuthoring wall = candidates[i];
            if (wall == null)
                continue;

            float xDelta = Mathf.Abs(wall.WorldCenterX - requestedWorldPos.x);
            if (xDelta > DoorWallSearchHorizontalTolerance)
                continue;

            if (!WallSplitUtility.TryComputeSplit(
                    wall,
                    openingBottomY,
                    openingHeight,
                    DoorSplitMinPieceHeight,
                    out WallSplitUtility.SplitResult split))
            {
                continue;
            }

            float yScore = Mathf.Abs(wall.WorldCenterY - openingCenterY);
            float score = xDelta * 10f + yScore;

            if (score < bestScore)
            {
                bestScore = score;
                bestWall = wall;
                bestSplit = split;
            }
        }

        return bestWall != null && bestSplit.IsValid;
    }

    private static List<WallSegmentAuthoring> FindWallCandidatesForDoor(
        Transform searchRoot,
        float requestedWorldX,
        float openingBottomY,
        float openingTopY)
    {
        List<WallSegmentAuthoring> result = new List<WallSegmentAuthoring>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        if (searchRoot != null)
        {
            WallSegmentAuthoring[] walls = searchRoot.GetComponentsInChildren<WallSegmentAuthoring>(true);
            for (int i = 0; i < walls.Length; i++)
                AddWallCandidate(result, seen, walls[i]);

            ResizableSegment2D[] resizables = searchRoot.GetComponentsInChildren<ResizableSegment2D>(true);
            for (int i = 0; i < resizables.Length; i++)
            {
                WallSegmentAuthoring wall = TryCreateWallAuthoringFromResizableCandidate(
                    resizables[i],
                    requestedWorldX,
                    openingBottomY,
                    openingTopY);

                AddWallCandidate(result, seen, wall);
            }
        }

        return result;
    }

    private static WallSegmentAuthoring TryCreateWallAuthoringFromResizableCandidate(
        ResizableSegment2D resizable,
        float requestedWorldX,
        float openingBottomY,
        float openingTopY)
    {
        if (resizable == null)
            return null;

        if (resizable.Axis != ResizableSegment2D.ResizeAxis.Vertical &&
            resizable.Axis != ResizableSegment2D.ResizeAxis.Both)
            return null;

        if (resizable.GetComponent<FloorSegmentAuthoring>() != null)
            return null;

        WallSegmentAuthoring existing = resizable.GetComponent<WallSegmentAuthoring>();
        if (existing != null)
            return existing;

        Bounds bounds;

        if (resizable.BoxCollider != null)
            bounds = resizable.BoxCollider.bounds;
        else if (resizable.SpriteRenderer != null)
            bounds = resizable.SpriteRenderer.bounds;
        else
            return null;

        if (Mathf.Abs(bounds.center.x - requestedWorldX) > DoorWallSearchHorizontalTolerance)
            return null;

        if (openingBottomY < bounds.min.y - 0.0001f ||
            openingTopY > bounds.max.y + 0.0001f)
            return null;

        WallSegmentAuthoring created = Undo.AddComponent<WallSegmentAuthoring>(resizable.gameObject);
        created.ResolveRefs();
        created.SyncFromResizable();

        EditorUtility.SetDirty(created);
        return created;
    }

    private static void AddWallCandidate(
        List<WallSegmentAuthoring> result,
        HashSet<GameObject> seen,
        WallSegmentAuthoring wall)
    {
        if (wall == null || wall.gameObject == null)
            return;

        if (!seen.Add(wall.gameObject))
            return;

        result.Add(wall);
    }

    private static GameObject CreateResizedWallClone(
        WallSegmentAuthoring sourceWall,
        float newHeight,
        float worldCenterY,
        Transform parent,
        string nameSuffix)
    {
        if (sourceWall == null)
            return null;

        if (newHeight <= 0.01f)
        {
            Debug.LogWarning($"[BoatBuilder] Refusing to create split wall clone with invalid height {newHeight:F3}.", sourceWall);
            return null;
        }

        Transform resolvedParent = parent != null ? parent : sourceWall.transform.parent;

        GameObject piece = UnityEngine.Object.Instantiate(
            sourceWall.gameObject,
            sourceWall.transform.position,
            sourceWall.transform.rotation,
            resolvedParent);

        if (piece == null)
            return null;

        Undo.RegisterCreatedObjectUndo(piece, $"Create split wall clone {nameSuffix}");

        piece.transform.localScale = sourceWall.transform.localScale;
        piece.name = BuildWallSplitPieceName(sourceWall.name, nameSuffix);

        WallSegmentAuthoring pieceAuthoring = piece.GetComponent<WallSegmentAuthoring>();
        if (pieceAuthoring == null)
        {
            Debug.LogWarning("[BoatBuilder] Split wall clone is missing WallSegmentAuthoring. Destroying failed clone.", piece);
            Undo.DestroyObjectImmediate(piece);
            return null;
        }

        RecordWallSegmentForUndo(pieceAuthoring, "Resize split wall clone");

        pieceAuthoring.ApplyHeight(newHeight);
        pieceAuthoring.SetWorldCenterYPreservingColliderOffset(worldCenterY);

        EditorUtility.SetDirty(piece);
        EditorUtility.SetDirty(pieceAuthoring);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return piece;
    }

    private static void AlignDoorToWallOpening(GameObject doorInstance, float targetCenterX, float targetBottomY)
    {
        if (doorInstance == null)
            return;

        DoorAuthoring authoring = doorInstance.GetComponent<DoorAuthoring>();
        if (authoring == null)
            authoring = doorInstance.GetComponentInChildren<DoorAuthoring>(true);

        if (authoring == null || authoring.BlockingCollider == null)
        {
            Vector3 fallback = doorInstance.transform.position;
            fallback.x = targetCenterX;
            fallback.y = targetBottomY;
            doorInstance.transform.position = fallback;
            return;
        }

        if (TryGetColliderCenterAndSize(authoring.BlockingCollider, out Vector2 center, out Vector2 size))
        {
            float currentBottomY = center.y - size.y * 0.5f;

            Vector3 delta = new Vector3(
                targetCenterX - center.x,
                targetBottomY - currentBottomY,
                0f);

            Undo.RecordObject(doorInstance.transform, "Align door to wall opening");
            doorInstance.transform.position += delta;
        }
    }

    private static bool TryGetColliderCenterAndSize(Collider2D collider, out Vector2 center, out Vector2 size)
    {
        center = default;
        size = default;

        if (collider == null)
            return false;

        if (collider is BoxCollider2D box)
        {
            center = box.transform.TransformPoint(box.offset);

            size = new Vector2(
                Mathf.Abs(box.size.x * box.transform.lossyScale.x),
                Mathf.Abs(box.size.y * box.transform.lossyScale.y));

            return true;
        }

        Bounds b = collider.bounds;
        center = b.center;
        size = b.size;
        return size.x > 0.0001f || size.y > 0.0001f;
    }

    private static void InitializePlacedWall(GameObject placed)
    {
        if (placed == null)
            return;

        WallSegmentAuthoring wall = placed.GetComponent<WallSegmentAuthoring>();
        if (wall == null)
            wall = placed.GetComponentInChildren<WallSegmentAuthoring>(true);

        if (wall == null)
        {
            ResizableSegment2D resizable = placed.GetComponent<ResizableSegment2D>();
            if (resizable == null)
                resizable = placed.GetComponentInChildren<ResizableSegment2D>(true);

            if (resizable != null &&
                (resizable.Axis == ResizableSegment2D.ResizeAxis.Vertical ||
                 resizable.Axis == ResizableSegment2D.ResizeAxis.Both))
            {
                wall = Undo.AddComponent<WallSegmentAuthoring>(resizable.gameObject);
            }
        }

        if (wall != null)
        {
            wall.ResolveRefs();
            wall.SyncFromResizable();
            EditorUtility.SetDirty(wall);
        }
    }

    private static void InitializePlacedDoor(GameObject placed, Transform boatRoot)
    {
        if (placed == null)
            return;

        DoorAuthoring authoring = placed.GetComponent<DoorAuthoring>();
        if (authoring == null)
            authoring = placed.GetComponentInChildren<DoorAuthoring>(true);

        if (authoring == null)
            return;

        if (!string.IsNullOrWhiteSpace(authoring.DoorId))
            return;

        Undo.RecordObject(authoring, "Configure Door");

        string id = GenerateNextDoorId(boatRoot, "door");
        authoring.DoorId = id;

        Undo.RecordObject(placed, "Rename Door");
        placed.name = id;

        EditorUtility.SetDirty(authoring);
        EditorUtility.SetDirty(placed);
    }

    private static string GenerateNextDoorId(Transform boatRoot, string prefix)
    {
        int maxFound = 0;

        if (boatRoot != null)
        {
            DoorAuthoring[] existing = boatRoot.GetComponentsInChildren<DoorAuthoring>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                DoorAuthoring door = existing[i];
                if (door == null)
                    continue;

                string id = door.DoorId;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!id.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
                    continue;

                string suffix = id.Substring(prefix.Length + 1);
                if (int.TryParse(suffix, out int n))
                    maxFound = Mathf.Max(maxFound, n);
            }
        }

        return $"{prefix}_{(maxFound + 1):00}";
    }

    private static WallSplitRecord GetOrCreateWallSplitRecord(WallSegmentAuthoring wall)
    {
        if (wall == null)
            return null;

        WallSplitRecord record = wall.GetComponent<WallSplitRecord>();
        if (record == null)
        {
            record = wall.gameObject.AddComponent<WallSplitRecord>();
            Undo.RegisterCreatedObjectUndo(record, "Add WallSplitRecord");

            Transform root = wall.transform.parent != null ? wall.transform.parent : wall.transform;

            Vector3 localBottom = root.InverseTransformPoint(new Vector3(wall.WorldCenterX, wall.WorldBottomY, 0f));
            Vector3 localTop = root.InverseTransformPoint(new Vector3(wall.WorldCenterX, wall.WorldTopY, 0f));
            Vector3 localCenter = root.InverseTransformPoint(new Vector3(wall.WorldCenterX, wall.WorldCenterY, 0f));

            record.InitializeNewRootRecord(
                Guid.NewGuid().ToString("N"),
                root,
                localBottom.y,
                localTop.y,
                localCenter.x,
                "Wall");

            EditorUtility.SetDirty(record);
        }

        return record;
    }

    private static void InheritWallSplitRecordFromSource(WallSegmentAuthoring targetWall, WallSplitRecord sourceRecord)
    {
        if (targetWall == null || sourceRecord == null)
            return;

        WallSplitRecord targetRecord = targetWall.GetComponent<WallSplitRecord>();
        if (targetRecord == null)
        {
            targetRecord = targetWall.gameObject.AddComponent<WallSplitRecord>();
            Undo.RegisterCreatedObjectUndo(targetRecord, "Add WallSplitRecord");
        }

        targetRecord.InitializeFromExistingRecord(
            sourceRecord,
            markAsSplitFragment: true,
            newSplitDepth: sourceRecord.SplitDepth + 1);

        EditorUtility.SetDirty(targetRecord);
    }

    private static void MarkWallAsSplitFragment(WallSegmentAuthoring wall, WallSplitRecord sourceRecord)
    {
        if (wall == null || sourceRecord == null)
            return;

        WallSplitRecord record = wall.GetComponent<WallSplitRecord>();
        if (record == null)
        {
            record = wall.gameObject.AddComponent<WallSplitRecord>();
            Undo.RegisterCreatedObjectUndo(record, "Add WallSplitRecord");
        }

        record.InitializeFromExistingRecord(
            sourceRecord,
            markAsSplitFragment: true,
            newSplitDepth: sourceRecord.SplitDepth + 1);

        EditorUtility.SetDirty(record);
    }

    private static void AttachWallRepairBlocker(
        GameObject openingObject,
        WallSplitRecord wallRecord,
        Transform spanRoot,
        float openingBottomWorldY,
        float openingTopWorldY,
        string blockerKind)
    {
        if (openingObject == null || wallRecord == null || spanRoot == null)
            return;

        WallRepairBlocker blocker = openingObject.GetComponent<WallRepairBlocker>();
        if (blocker == null)
        {
            blocker = openingObject.AddComponent<WallRepairBlocker>();
            Undo.RegisterCreatedObjectUndo(blocker, "Add WallRepairBlocker");
        }

        float localBottomY = spanRoot.InverseTransformPoint(new Vector3(0f, openingBottomWorldY, 0f)).y;
        float localTopY = spanRoot.InverseTransformPoint(new Vector3(0f, openingTopWorldY, 0f)).y;

        blocker.Initialize(
            wallRecord.SpanId,
            spanRoot,
            localBottomY,
            localTopY,
            blockerKind);

        EditorUtility.SetDirty(blocker);
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

    private static string GetCleanWallSplitBaseName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Wall";

        string name = rawName;

        bool changed;
        do
        {
            changed = false;

            if (name.EndsWith("_Bottom", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_Bottom".Length);
                changed = true;
            }

            if (name.EndsWith("_Top", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - "_Top".Length);
                changed = true;
            }

            int repairedIndex = name.LastIndexOf("_Repaired_", StringComparison.OrdinalIgnoreCase);
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

    private static string BuildWallSplitPieceName(string baseName, string sideSuffix)
    {
        return $"{GetCleanWallSplitBaseName(baseName)}{sideSuffix}";
    }
}
#endif