#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Boat))]
public sealed class BoatGeometryAutoFitEditor : Editor
{
    private const string UndoLabel = "Auto-fit Boat Geometry";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Geometry Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-fit Geometry From Structure", GUILayout.Height(28)))
            {
                AutoFitGeometry((Boat)target);
            }

            EditorGUILayout.HelpBox(
                "Scans child structural renderers/colliders, computes boat-local bounds, and updates Boat width/height/volume. " +
                "This is a rectangle approximation for buoyancy.",
                MessageType.Info);
        }
    }

    private static void AutoFitGeometry(Boat boat)
    {
        if (boat == null)
            return;

        if (!TryComputeLocalBounds(boat.transform, out Bounds localBounds))
        {
            Debug.LogWarning("[BoatGeometryAutoFit] Could not compute bounds. No usable child renderers/colliders found.", boat);
            return;
        }

        float width = Mathf.Max(0.01f, localBounds.size.x);
        float height = Mathf.Max(0.01f, localBounds.size.y);

        // 2D project: use rectangle area as volume proxy.
        float volume = Mathf.Max(0.01f, width * height);

        Undo.RecordObject(boat, UndoLabel);

        boat.SetAuthoritativeGeometry(width, height, volume);

        EditorUtility.SetDirty(boat);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boat.gameObject.scene);

        Debug.Log(
            $"[BoatGeometryAutoFit] Updated '{boat.name}' geometry: width={width:F2}, height={height:F2}, volume={volume:F2}. " +
            $"Local bounds center={localBounds.center}, size={localBounds.size}",
            boat);
    }

    private static bool TryComputeLocalBounds(Transform boatRoot, out Bounds localBounds)
    {
        localBounds = default;

        bool hasAny = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

        // Prefer colliders for physics-ish geometry.
        Collider2D[] colliders = boatRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in colliders)
        {
            if (col == null)
                continue;

            // Ignore triggers. Compartments, boarded volumes, interaction zones, etc.
            // Physics buoyancy should come from solid boat structure, not invisible gameplay volumes.
            if (col.isTrigger)
                continue;

            EncapsulateWorldBoundsAsLocalAabb(boatRoot, col.bounds, ref min, ref max, ref hasAny);
        }

        // Fallback to renderers if no solid colliders found.
        if (!hasAny)
        {
            Renderer[] renderers = boatRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;

                // Skip water renderers if present, because using flood water to size boat buoyancy
                // would be impressively stupid even by game-dev standards.
                if (r.GetComponent<CompartmentWaterRenderer>() != null)
                    continue;

                EncapsulateWorldBoundsAsLocalAabb(boatRoot, r.bounds, ref min, ref max, ref hasAny);
            }
        }

        if (!hasAny)
            return false;

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        localBounds = new Bounds(center, size);
        return true;
    }

    private static void EncapsulateWorldBoundsAsLocalAabb(
        Transform root,
        Bounds worldBounds,
        ref Vector3 min,
        ref Vector3 max,
        ref bool hasAny)
    {
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        Vector3[] corners =
        {
            new(c.x - e.x, c.y - e.y, c.z),
            new(c.x - e.x, c.y + e.y, c.z),
            new(c.x + e.x, c.y - e.y, c.z),
            new(c.x + e.x, c.y + e.y, c.z),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = root.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
            hasAny = true;
        }
    }
}
#endif