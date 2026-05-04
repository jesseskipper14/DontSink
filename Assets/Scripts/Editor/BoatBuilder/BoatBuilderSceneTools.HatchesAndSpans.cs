#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{

    private static GameObject TryPlaceHatchWithFloorSplit(GameObject hatchPrefab, Vector3 requestedWorldPos, Transform preferredParent)
        {
            if (hatchPrefab == null)
                return null;

            var hatchAuthoring = hatchPrefab.GetComponent<HatchAuthoring>();
            if (hatchAuthoring == null)
            {
                Debug.LogWarning("[BoatBuilder] Hatch prefab is missing HatchAuthoring. Cannot perform floor split placement.", hatchPrefab);
                return null;
            }

            float openingWidth = hatchAuthoring.OpeningWidth;
            float minPieceWidth = HatchSplitMinPieceWidth;

            if (!TryFindBestFloorSplit(
                    requestedWorldPos,
                    openingWidth,
                    minPieceWidth,
                    preferredParent,
                    out var floor,
                    out var split))
            {
                Debug.LogWarning("[BoatBuilder] No valid floor segment found under hatch placement. Hatch placement requires a splittable FloorSegmentAuthoring beneath it.");
                return null;
            }

            Transform actualParent = floor.transform.parent != null ? floor.transform.parent : preferredParent;
            Quaternion baseRot = floor.transform.rotation;

            SplitSpanRecord spanRecord = GetOrCreateSpanRecord(floor);
            Transform spanRoot = spanRecord != null && spanRecord.TryGetRoot(out var resolvedRoot)
                ? resolvedRoot
                : actualParent;

            Debug.Log(
                $"[BoatBuilder:Hatch] Splitting '{floor.name}' " +
                $"leftWidth={split.LeftWidth:F3}, rightWidth={split.RightWidth:F3}, " +
                $"leftCenter={split.LeftCenterX:F3}, rightCenter={split.RightCenterX:F3}",
                floor);

            // Create the new right-side piece first.
            // The original floor remains untouched until hatch + right piece are confirmed.
            GameObject rightPiece = CreateResizedFloorClone(
                floor,
                split.RightWidth,
                split.RightCenterX,
                actualParent,
                "_Right");

            if (rightPiece == null)
            {
                Debug.LogWarning("[BoatBuilder] Failed to create right split floor piece. Original floor preserved.", floor);
                return null;
            }

            FloorSegmentAuthoring rightPieceFloor = rightPiece != null
                ? rightPiece.GetComponent<FloorSegmentAuthoring>()
                : null;

            if (rightPieceFloor != null && spanRecord != null)
                InheritSpanRecordFromSource(rightPieceFloor, spanRecord);

            Vector3 hatchWorldPos = new Vector3(
                requestedWorldPos.x,
                floor.WorldCenterY,
                requestedWorldPos.z);

            GameObject hatchInstance = PlacePrefab(hatchPrefab, hatchWorldPos, actualParent);

            if (hatchInstance == null)
            {
                Debug.LogWarning("[BoatBuilder] Hatch placement failed. Destroying right split clone. Original floor preserved.", floor);

                Undo.DestroyObjectImmediate(rightPiece);
                return null;
            }

            hatchInstance.transform.rotation = baseRot;

            // Now mutate the original floor into the left piece.
            // No deletion. No vanishing boat floor. Society heals, briefly.
            RecordFloorSegmentForUndo(floor, "Resize original floor into left split piece");

            floor.ApplyWidth(split.LeftWidth);
            floor.SetWorldCenterXPreservingColliderOffset(split.LeftCenterX);

            Undo.RecordObject(floor.gameObject, "Rename left split floor piece");
            floor.gameObject.name = BuildSplitPieceName(floor.gameObject.name, "_Left");

            InitializePlacedHatch(hatchInstance, actualParent);

            if (spanRecord != null && spanRoot != null)
            {
                AttachRepairBlocker(
                    hatchInstance,
                    spanRecord,
                    spanRoot,
                    split.OpeningLeftX,
                    split.OpeningRightX,
                    "Hatch");
            }

            EditorUtility.SetDirty(floor);
            EditorUtility.SetDirty(floor.gameObject);
            EditorUtility.SetDirty(rightPiece);
            EditorUtility.SetDirty(hatchInstance);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return hatchInstance;
        }

    private static GameObject TryPlaceLedgeWithOptionalFloorSplit(
        GameObject ledgePrefab,
        Vector3 requestedWorldPos,
        Transform boatRoot,
        Transform fallbackParent)
        {
            if (ledgePrefab == null)
                return null;

            if (!TryGetOpeningWidthFromLedgePrefab(ledgePrefab, out float openingWidth))
            {
                Debug.LogWarning(
                    "[BoatBuilder] Ledge prefab is missing a usable BoxCollider2D/HatchLedge width. " +
                    "Falling back to normal ledge placement.",
                    ledgePrefab);

                return PlacePrefab(ledgePrefab, requestedWorldPos, fallbackParent);
            }

            float minPieceWidth = HatchSplitMinPieceWidth;

            // Important:
            // Search across the whole boat root, not the resolved ledge parent category.
            Transform splitSearchRoot = boatRoot != null ? boatRoot : null;

            if (!TryFindBestFloorSplit(
                    requestedWorldPos,
                    openingWidth,
                    minPieceWidth,
                    splitSearchRoot,
                    out var floor,
                    out var split))
            {
                // Ledges are allowed to exist standalone.
                return PlacePrefab(ledgePrefab, requestedWorldPos, fallbackParent);
            }

            Transform actualParent = floor.transform.parent != null ? floor.transform.parent : fallbackParent;
            Quaternion baseRot = floor.transform.rotation;

            SplitSpanRecord spanRecord = GetOrCreateSpanRecord(floor);
            Transform spanRoot = spanRecord != null && spanRecord.TryGetRoot(out var resolvedRoot)
                ? resolvedRoot
                : actualParent;

            Debug.Log(
                $"[BoatBuilder:Ledge] Splitting '{floor.name}' " +
                $"leftWidth={split.LeftWidth:F3}, rightWidth={split.RightWidth:F3}, " +
                $"leftCenter={split.LeftCenterX:F3}, rightCenter={split.RightCenterX:F3}",
                floor);

            GameObject rightPiece = CreateResizedFloorClone(
                floor,
                split.RightWidth,
                split.RightCenterX,
                actualParent,
                "_Right");

            if (rightPiece == null)
            {
                Debug.LogWarning(
                    "[BoatBuilder] Failed to create right split floor piece for ledge placement. " +
                    "Falling back to normal ledge placement.",
                    floor);

                return PlacePrefab(ledgePrefab, requestedWorldPos, fallbackParent);
            }

            FloorSegmentAuthoring rightPieceFloor = rightPiece != null
                ? rightPiece.GetComponent<FloorSegmentAuthoring>()
                : null;

            if (rightPieceFloor != null && spanRecord != null)
                InheritSpanRecordFromSource(rightPieceFloor, spanRecord);

            Vector3 ledgeWorldPos = new Vector3(
                requestedWorldPos.x,
                floor.WorldCenterY,
                requestedWorldPos.z);

            if (spanRecord != null)
                MarkFloorAsSplitFragment(floor, spanRecord);

            GameObject ledgeInstance = PlacePrefab(ledgePrefab, ledgeWorldPos, actualParent);

            if (ledgeInstance == null)
            {
                Debug.LogWarning(
                    "[BoatBuilder] Ledge placement failed after split prep. Destroying right split clone and preserving original floor.",
                    floor);

                Undo.DestroyObjectImmediate(rightPiece);
                return null;
            }

            if (spanRecord != null && spanRoot != null)
            {
                AttachRepairBlocker(
                    ledgeInstance,
                    spanRecord,
                    spanRoot,
                    split.OpeningLeftX,
                    split.OpeningRightX,
                    "Ledge");
            }

            ledgeInstance.transform.rotation = baseRot;

            RecordFloorSegmentForUndo(floor, "Resize original floor into left split piece");

            floor.ApplyWidth(split.LeftWidth);
            floor.SetWorldCenterXPreservingColliderOffset(split.LeftCenterX);

            Undo.RecordObject(floor.gameObject, "Rename left split floor piece");
            floor.gameObject.name = BuildSplitPieceName(floor.gameObject.name, "_Left");

            EditorUtility.SetDirty(floor);
            EditorUtility.SetDirty(floor.gameObject);
            EditorUtility.SetDirty(rightPiece);
            EditorUtility.SetDirty(ledgeInstance);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return ledgeInstance;
        }

    private static bool TryFindBestFloorSplit(
        Vector3 requestedWorldPos,
        float openingWidth,
        float minPieceWidth,
        Transform preferredParent,
        out FloorSegmentAuthoring bestFloor,
        out FloorSplitUtility.SplitResult bestSplit)
        {
            bestFloor = null;
            bestSplit = default;

            var candidates = preferredParent != null
                ? preferredParent.GetComponentsInChildren<FloorSegmentAuthoring>(true)
                : UnityEngine.Object.FindObjectsByType<FloorSegmentAuthoring>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            float bestScore = float.PositiveInfinity;

            //Debug.Log($"[BoatBuilder:Hatch] Searching split. requested=({requestedWorldPos.x:F2},{requestedWorldPos.y:F2}) openingWidth={openingWidth:F2} minPieceWidth={minPieceWidth:F2} candidates={candidates.Length}");

            foreach (var floor in candidates)
            {
                if (floor == null)
                    continue;

                float yDelta = Mathf.Abs(floor.transform.position.y - requestedWorldPos.y);
                if (yDelta > HatchSearchVerticalTolerance)
                {
                    Debug.Log($"[BoatBuilder:Hatch] Reject floor '{floor.name}' due to Y delta. floorY={floor.transform.position.y:F2} requestedY={requestedWorldPos.y:F2} yDelta={yDelta:F2} tol={HatchSearchVerticalTolerance:F2}", floor);
                    continue;
                }

                float segLeft = floor.WorldLeftX;
                float segRight = floor.WorldRightX;
                float openLeft = requestedWorldPos.x - openingWidth * 0.5f;
                float openRight = requestedWorldPos.x + openingWidth * 0.5f;

                //Debug.Log(
                //    $"[BoatBuilder:Hatch] Candidate '{floor.name}' floorWidth={floor.Width:F2} floorX={floor.transform.position.x:F2} seg=[{segLeft:F2},{segRight:F2}] opening=[{openLeft:F2},{openRight:F2}]",
                //    floor);

                if (!FloorSplitUtility.TryComputeSplit(
                        floor,
                        requestedWorldPos.x,
                        openingWidth,
                        minPieceWidth,
                        out var split))
                {
                    Debug.LogWarning(
                        $"[BoatBuilder:Hatch] Reject floor '{floor.name}' because split is invalid. " +
                        $"Likely causes: opening wider than allowed span, click too close to edge, or minPieceWidth too large. " +
                        $"floorWidth={floor.Width:F2} openingWidth={openingWidth:F2} minPieceWidth={minPieceWidth:F2}",
                        floor);
                    continue;
                }

                float centerDelta = Mathf.Abs(floor.WorldCenterX - requestedWorldPos.x);
                float score = yDelta * 10f + centerDelta;

                Debug.Log(
                    $"[BoatBuilder:Hatch] VALID split on '{floor.name}'. leftWidth={split.LeftWidth:F2} rightWidth={split.RightWidth:F2} score={score:F2}",
                    floor);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestFloor = floor;
                    bestSplit = split;
                }
            }

            return bestFloor != null && bestSplit.IsValid;
        }

    private static GameObject CreateResizedFloorClone(
        FloorSegmentAuthoring sourceFloor,
        float newWidth,
        float worldCenterX,
        Transform parent,
        string nameSuffix)
        {
            if (sourceFloor == null)
                return null;

            if (newWidth <= 0.01f)
            {
                Debug.LogWarning($"[BoatBuilder] Refusing to create split floor clone with invalid width {newWidth:F3}.", sourceFloor);
                return null;
            }

            Transform resolvedParent = parent != null ? parent : sourceFloor.transform.parent;

            // Instantiate directly under the intended parent while preserving world pose.
            // This avoids the "instantiate, then reparent, then pray" ritual.
            GameObject piece = UnityEngine.Object.Instantiate(
                sourceFloor.gameObject,
                sourceFloor.transform.position,
                sourceFloor.transform.rotation,
                resolvedParent);

            if (piece == null)
                return null;

            Undo.RegisterCreatedObjectUndo(piece, $"Create split floor clone {nameSuffix}");

            piece.transform.localScale = sourceFloor.transform.localScale;
            piece.name = BuildSplitPieceName(sourceFloor.name, nameSuffix);

            var pieceAuthoring = piece.GetComponent<FloorSegmentAuthoring>();
            if (pieceAuthoring == null)
            {
                Debug.LogWarning("[BoatBuilder] Split floor clone is missing FloorSegmentAuthoring. Destroying failed clone.", piece);
                Undo.DestroyObjectImmediate(piece);
                return null;
            }

            RecordFloorSegmentForUndo(pieceAuthoring, "Resize split floor clone");

            pieceAuthoring.ApplyWidth(newWidth);

            // Critical: move AFTER resizing, because the collider center/bounds may change.
            pieceAuthoring.SetWorldCenterXPreservingColliderOffset(worldCenterX);

            Debug.Log(
                $"[BoatBuilder:Hatch] Created split clone '{piece.name}' " +
                $"requestedCenterX={worldCenterX:F3}, actualCenterX={pieceAuthoring.WorldCenterX:F3}, " +
                $"width={newWidth:F3}, actualBounds=[{pieceAuthoring.WorldLeftX:F3}, {pieceAuthoring.WorldRightX:F3}]",
                piece);

            EditorUtility.SetDirty(piece);
            EditorUtility.SetDirty(pieceAuthoring);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return piece;
        }

    private static void InitializePlacedHatch(GameObject placed, Transform boatRoot)
        {
            if (placed == null)
                return;

            var authoring = placed.GetComponent<HatchAuthoring>();
            if (authoring == null)
                return;

            if (!string.IsNullOrWhiteSpace(authoring.HatchId))
                return;

            Undo.RecordObject(authoring, "Configure Hatch");

            string id = GenerateNextHatchId(boatRoot, "hatch");
            authoring.HatchId = id;

            Undo.RecordObject(placed, "Rename Hatch");
            placed.name = id;

            EditorUtility.SetDirty(authoring);
            EditorUtility.SetDirty(placed);
        }

    private static string GenerateNextHatchId(Transform boatRoot, string prefix)
        {
            int maxFound = 0;

            if (boatRoot != null)
            {
                var existing = boatRoot.GetComponentsInChildren<HatchAuthoring>(true);
                foreach (var h in existing)
                {
                    if (h == null)
                        continue;

                    string id = h.HatchId;
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (!id.StartsWith(prefix + "_", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    string suffix = id.Substring(prefix.Length + 1);
                    if (int.TryParse(suffix, out int n))
                        maxFound = Mathf.Max(maxFound, n);
                }
            }

            return $"{prefix}_{(maxFound + 1):00}";
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

    private static bool TryGetOpeningWidthFromLedgePrefab(GameObject ledgePrefab, out float openingWidth)
        {
            openingWidth = 0f;

            if (ledgePrefab == null)
                return false;

            HatchLedge hatchLedge = ledgePrefab.GetComponent<HatchLedge>();
            if (hatchLedge == null)
                hatchLedge = ledgePrefab.GetComponentInChildren<HatchLedge>(true);

            BoxCollider2D box = null;

            if (hatchLedge != null)
                box = hatchLedge.Collider as BoxCollider2D;

            if (box == null)
                box = ledgePrefab.GetComponent<BoxCollider2D>();

            if (box == null)
                box = ledgePrefab.GetComponentInChildren<BoxCollider2D>(true);

            if (box == null)
                return false;

            float localWidth = Mathf.Abs(box.size.x);
            float scaleX = Mathf.Abs(box.transform.lossyScale.x);

            openingWidth = localWidth * scaleX;
            return openingWidth > 0.01f;
        }

    private static string GenerateNewSpanId()
        {
            return System.Guid.NewGuid().ToString("N");
        }

    private static SplitSpanRecord GetOrCreateSpanRecord(FloorSegmentAuthoring floor)
        {
            if (floor == null)
                return null;

            SplitSpanRecord record = floor.GetComponent<SplitSpanRecord>();
            if (record == null)
            {
                record = floor.gameObject.AddComponent<SplitSpanRecord>();
                Undo.RegisterCreatedObjectUndo(record, "Add SplitSpanRecord");

                Transform root = floor.transform.parent != null ? floor.transform.parent : floor.transform;
                Vector3 localPos = root.InverseTransformPoint(floor.transform.position);

                float localStartX = localPos.x - floor.Width * 0.5f;
                float localEndX = localPos.x + floor.Width * 0.5f;
                float localCenterY = localPos.y;

                record.InitializeNewRootRecord(
                    GenerateNewSpanId(),
                    root,
                    localStartX,
                    localEndX,
                    localCenterY,
                    "Floor");

                EditorUtility.SetDirty(record);
            }

            return record;
        }

    private static void InheritSpanRecordFromSource(FloorSegmentAuthoring targetFloor, SplitSpanRecord sourceRecord)
        {
            if (targetFloor == null || sourceRecord == null)
                return;

            SplitSpanRecord targetRecord = targetFloor.GetComponent<SplitSpanRecord>();
            if (targetRecord == null)
            {
                targetRecord = targetFloor.gameObject.AddComponent<SplitSpanRecord>();
                Undo.RegisterCreatedObjectUndo(targetRecord, "Add SplitSpanRecord");
            }

            targetRecord.InitializeFromExistingRecord(
                sourceRecord,
                markAsSplitFragment: true,
                newSplitDepth: sourceRecord.SplitDepth + 1);

            EditorUtility.SetDirty(targetRecord);
        }

    private static void MarkFloorAsSplitFragment(FloorSegmentAuthoring floor, SplitSpanRecord sourceRecord)
        {
            if (floor == null || sourceRecord == null)
                return;

            SplitSpanRecord record = floor.GetComponent<SplitSpanRecord>();
            if (record == null)
            {
                record = floor.gameObject.AddComponent<SplitSpanRecord>();
                Undo.RegisterCreatedObjectUndo(record, "Add SplitSpanRecord");
            }

            record.InitializeFromExistingRecord(
                sourceRecord,
                markAsSplitFragment: true,
                newSplitDepth: sourceRecord.SplitDepth + 1);

            EditorUtility.SetDirty(record);
        }

    private static void AttachRepairBlocker(
            GameObject openingObject,
            SplitSpanRecord spanRecord,
            Transform spanRoot,
            float openingLeftWorldX,
            float openingRightWorldX,
            string blockerKind)
        {
            if (openingObject == null || spanRecord == null || spanRoot == null)
                return;

            SpanRepairBlocker blocker = openingObject.GetComponent<SpanRepairBlocker>();
            if (blocker == null)
            {
                blocker = openingObject.AddComponent<SpanRepairBlocker>();
                Undo.RegisterCreatedObjectUndo(blocker, "Add SpanRepairBlocker");
            }

            float localLeftX = spanRoot.InverseTransformPoint(new Vector3(openingLeftWorldX, 0f, 0f)).x;
            float localRightX = spanRoot.InverseTransformPoint(new Vector3(openingRightWorldX, 0f, 0f)).x;

            blocker.Initialize(
                spanRecord.SpanId,
                spanRoot,
                localLeftX,
                localRightX,
                blockerKind);

            EditorUtility.SetDirty(blocker);
        }

    private static string GetCleanSplitBaseName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Segment";

            string name = rawName;

            bool changed;
            do
            {
                changed = false;

                if (name.EndsWith("_Left", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - "_Left".Length);
                    changed = true;
                }

                if (name.EndsWith("_Right", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - "_Right".Length);
                    changed = true;
                }

                int repairedIndex = name.LastIndexOf("_Repaired_", StringComparison.OrdinalIgnoreCase);
                if (repairedIndex >= 0)
                {
                    string suffix = name.Substring(repairedIndex);
                    if (System.Text.RegularExpressions.Regex.IsMatch(suffix, @"^_Repaired_\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        name = name.Substring(0, repairedIndex);
                        changed = true;
                    }
                }
            }
            while (changed);

            return string.IsNullOrWhiteSpace(name) ? "Segment" : name;
        }

    private static string BuildSplitPieceName(string baseName, string sideSuffix)
        {
            return $"{GetCleanSplitBaseName(baseName)}{sideSuffix}";
        }
}
#endif
