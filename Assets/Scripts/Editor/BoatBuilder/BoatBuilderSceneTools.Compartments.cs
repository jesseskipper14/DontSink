#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{

    private static void InitializePlacedCompartment(GameObject placed, Transform boatRoot)
        {
            if (placed == null)
                return;

            if (boatRoot == null)
            {
                Debug.LogWarning("[BoatBuilder] Cannot register compartment: no BoatRoot resolved.", placed);
                return;
            }

            Boat boat = boatRoot.GetComponent<Boat>();
            if (boat == null)
            {
                boat = boatRoot.GetComponentInParent<Boat>();
            }

            if (boat == null)
            {
                Debug.LogWarning(
                    $"[BoatBuilder] Cannot register compartment '{placed.name}': no Boat component found on BoatRoot '{boatRoot.name}' or its parents.",
                    placed);
                return;
            }

            Compartment[] compartments = placed.GetComponentsInChildren<Compartment>(true);
            if (compartments == null || compartments.Length == 0)
            {
                Debug.LogWarning(
                    $"[BoatBuilder] Placed CompartmentRect '{placed.name}' has no Compartment component.",
                    placed);
                return;
            }

            Undo.RecordObject(boat, "Register placed compartment");

            int added = 0;

            foreach (Compartment compartment in compartments)
            {
                if (compartment == null)
                    continue;

                if (boat.Compartments == null)
                    boat.Compartments = new System.Collections.Generic.List<Compartment>();

                if (boat.Compartments.Contains(compartment))
                    continue;

                boat.Compartments.Add(compartment);
                added++;

                EditorUtility.SetDirty(compartment);
            }

            EditorUtility.SetDirty(boat);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

    public static void RebuildCompartmentsFromBoatRoot(Transform boatRoot)
        {
            if (boatRoot == null)
            {
                Debug.LogWarning("[BoatBuilder] Cannot rebuild compartments: BoatRoot is null.");
                return;
            }

            Boat boat = boatRoot.GetComponent<Boat>();
            if (boat == null)
                boat = boatRoot.GetComponentInParent<Boat>();

            if (boat == null)
            {
                Debug.LogWarning($"[BoatBuilder] Cannot rebuild compartments: no Boat component found for '{boatRoot.name}'.", boatRoot);
                return;
            }

            Compartment[] found = boatRoot.GetComponentsInChildren<Compartment>(true);

            Undo.RecordObject(boat, "Rebuild Boat Compartments");

            boat.Compartments = new System.Collections.Generic.List<Compartment>();

            foreach (Compartment c in found)
            {
                if (c == null)
                    continue;

                if (!boat.Compartments.Contains(c))
                    boat.Compartments.Add(c);
            }

            if (boat.Connections == null)
                boat.Connections = new System.Collections.Generic.List<CompartmentConnection>();
            else
                boat.Connections = boat.Connections
                    .Where(conn => conn != null)
                    .Distinct()
                    .ToList();

            foreach (Compartment c in boat.Compartments)
            {
                if (c != null)
                    c.connections.Clear();
            }

            foreach (CompartmentConnection conn in boat.Connections)
            {
                if (conn == null)
                    continue;

                if (conn.A != null && !conn.A.connections.Contains(conn))
                    conn.A.connections.Add(conn);

                if (conn.B != null && !conn.B.connections.Contains(conn))
                    conn.B.connections.Add(conn);
            }

            EditorUtility.SetDirty(boat);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[BoatBuilder] Rebuilt Boat.Compartments for '{boat.name}'. Found {boat.Compartments.Count} compartment(s).", boat);
        }

    private static GameObject TryPlaceDetectedCompartment(
        GameObject compartmentPrefab,
        Vector3 requestedWorldPos,
        Transform boatRoot,
        Transform fallbackParent)
        {
            if (compartmentPrefab == null)
                return null;

            if (boatRoot == null)
            {
                Debug.LogWarning("[BoatBuilder] Cannot place detected compartment: no BoatRoot resolved.");
                return null;
            }

            CompartmentBoundaryAuthoring[] boundaries =
                boatRoot.GetComponentsInChildren<CompartmentBoundaryAuthoring>(true);

            if (boundaries == null || boundaries.Length == 0)
            {
                Debug.LogWarning("[BoatBuilder] Cannot place detected compartment: no CompartmentBoundaryAuthoring objects found under BoatRoot.", boatRoot);
                return null;
            }

            Vector2 click = new Vector2(requestedWorldPos.x, requestedWorldPos.y);

            if (!CompartmentBoundedSpaceDetector.TryDetectBoundedSpaceAtPoint(
                    click,
                    boundaries,
                    out var result,
                    joinEpsilon: 0.12f,
                    minWidth: 0.1f,
                    minHeight: 0.1f))
            {
                Debug.LogWarning(
                    $"[BoatBuilder] Compartment placement failed at {click}: {result.Failure}",
                    boatRoot);
                return null;
            }

            // Directional fit padding.
            // Positive values expand the compartment slightly into structural boundaries.
            // Keep these small so we hide seams without obviously penetrating the boat.
            const float sideOverlap = 0.08f;
            const float floorOverlap = 0.05f;
            const float roofOverlap = 0.08f;

            float paddedMinX = result.MinX - sideOverlap;
            float paddedMaxX = result.MaxX + sideOverlap;
            float paddedMinY = result.MinY - floorOverlap;
            float paddedMaxY = result.MaxY + roofOverlap;

            float width = Mathf.Max(0.01f, paddedMaxX - paddedMinX);
            float height = Mathf.Max(0.01f, paddedMaxY - paddedMinY);

            float centerX = (paddedMinX + paddedMaxX) * 0.5f;
            float centerY = (paddedMinY + paddedMaxY) * 0.5f;

            Vector3 worldCenter = new Vector3(centerX, centerY, requestedWorldPos.z);

            Transform actualParent = fallbackParent != null ? fallbackParent : boatRoot;

            GameObject placed = PlacePrefab(compartmentPrefab, worldCenter, actualParent);
            if (placed == null)
                return null;

            if (!ConfigurePlacedCompartmentToBounds(
                    placed,
                    actualParent,
                    width,
                    height,
                    paddedMinY,
                    paddedMaxY,
                    result.IsOpenTop))
            {
                Debug.LogWarning("[BoatBuilder] Failed to configure placed compartment from detected bounds. Destroying placed object.", placed);
                Undo.DestroyObjectImmediate(placed);
                return null;
            }

            InitializePlacedCompartment(placed, boatRoot);

            return placed;
        }

    private static bool ConfigurePlacedCompartmentToBounds(
        GameObject placed,
        Transform parentSpace,
        float worldWidth,
        float worldHeight,
        float targetBottomWorldY,
        float targetTopWorldY,
        bool isOpenTop)
        {
            if (placed == null)
                return false;

            CompartmentRectAuthoring rect = placed.GetComponent<CompartmentRectAuthoring>();
            if (rect == null)
                rect = placed.GetComponentInChildren<CompartmentRectAuthoring>(true);

            if (rect == null)
            {
                Debug.LogWarning("[BoatBuilder] Placed compartment prefab has no CompartmentRectAuthoring.", placed);
                return false;
            }

            Undo.RecordObject(rect, "Configure detected compartment");
            Undo.RecordObject(placed.transform, "Configure detected compartment transform");

            float cellSize = Mathf.Max(0.1f, rect.cellSize);

            int cellsWide = Mathf.Max(1, Mathf.CeilToInt(worldWidth / cellSize));

            // Keep the current sizing policy:
            // open-top can bias a little large, closed-top stays conservative.
            int cellsHigh = Mathf.Max(1, Mathf.CeilToInt(worldHeight / cellSize));

            if (cellsHigh <= 0)
                cellsHigh = 1;

            rect.width = cellsWide;
            rect.height = cellsHigh;
            rect.centerOffsetCells = Vector2Int.zero;

            rect.Apply();

            float appliedHeightWorld = rect.height * cellSize;

            Vector3 pos = placed.transform.position;

            // Always top-anchor vertically.
            // For closed-top this respects the roof bottom.
            // For open-top this respects the lower wall top.
            float correctedCenterY = targetTopWorldY - appliedHeightWorld * 0.5f;
            pos.y = correctedCenterY;

            placed.transform.position = pos;

            EditorUtility.SetDirty(rect);
            EditorUtility.SetDirty(placed);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            return true;
        }
}
#endif
