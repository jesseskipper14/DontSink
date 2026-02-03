#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BoatCompartmentMerger
{
    private const float EPS = 0.0005f; // edge-touch tolerance in boat-local units

    private struct RectL
    {
        public Vector2 Min;
        public Vector2 Max;

        public float MinX => Min.x;
        public float MaxX => Max.x;
        public float MinY => Min.y;
        public float MaxY => Max.y;

        public RectL(Vector2 min, Vector2 max) { Min = min; Max = max; }
    }

    public static void MergeOnBoat(Boat boat)
    {
        if (boat == null) return;

        // Must have compartments list
        var fragments = boat.Compartments?.Where(c => c != null).Distinct().ToList();
        if (fragments == null || fragments.Count == 0)
        {
            Debug.LogWarning("[Merge Compartments] Boat has no compartments assigned.", boat);
            return;
        }

        Undo.RegisterCompleteObjectUndo(boat, "Merge Compartments");

        // Cache rects in BOAT LOCAL space
        var rects = new Dictionary<Compartment, RectL>(fragments.Count);
        foreach (var c in fragments)
        {
            if (!TryGetCompartmentRectBoatLocal(boat, c, out var r))
            {
                Debug.LogWarning($"[Merge Compartments] Could not determine rect for {c.name}. Needs BoxCollider2D or valid corners.", c);
                continue;
            }
            rects[c] = r;
        }

        // Build adjacency graph (touching, and NOT separated by an explicit connection)
        var graph = new Dictionary<Compartment, List<Compartment>>();
        foreach (var a in rects.Keys) graph[a] = new List<Compartment>();

        bool HasExplicitConnection(Compartment x, Compartment y)
        {
            if (x == null || y == null) return false;
            foreach (var conn in boat.Connections)
            {
                if (conn == null) continue;
                if ((conn.A == x && conn.B == y) || (conn.A == y && conn.B == x))
                    return true;
            }
            return false;
        }

        var keys = rects.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = i + 1; j < keys.Count; j++)
            {
                var A = keys[i];
                var B = keys[j];

                if (HasExplicitConnection(A, B))
                    continue; // touching is allowed if explicitly connected

                if (AreTouching(rects[A], rects[B]))
                {
                    graph[A].Add(B);
                    graph[B].Add(A);
                }
            }
        }

        // Find connected components
        var visited = new HashSet<Compartment>();
        var components = new List<List<Compartment>>();

        foreach (var start in keys)
        {
            if (visited.Contains(start)) continue;

            var comp = new List<Compartment>();
            var q = new Queue<Compartment>();
            q.Enqueue(start);
            visited.Add(start);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                comp.Add(cur);

                foreach (var nxt in graph[cur])
                {
                    if (visited.Add(nxt))
                        q.Enqueue(nxt);
                }
            }

            components.Add(comp);
        }

        // If nothing merges, done
        if (!components.Any(c => c.Count > 1))
        {
            Debug.Log("[Merge Compartments] No touching fragments found (or all are explicitly connected).", boat);
            return;
        }

        // Create a disabled container to stash old fragments (prevents mass contribution double-counting)
        var stash = boat.transform.Find("CompartmentFragments_DISABLED");
        if (stash == null)
        {
            var stashGO = new GameObject("CompartmentFragments_DISABLED");
            Undo.RegisterCreatedObjectUndo(stashGO, "Create Compartment Stash");
            stashGO.transform.SetParent(boat.transform, false);
            stashGO.SetActive(false);
            stash = stashGO.transform;
        }

        // Map old -> new merged compartment
        var remap = new Dictionary<Compartment, Compartment>();

        // Build new compartments list
        var newCompartments = new List<Compartment>();

        int mergeIndex = 0;
        foreach (var group in components)
        {
            if (group.Count == 1)
            {
                // Keep single fragments as-is
                newCompartments.Add(group[0]);
                continue;
            }

            // Union bounds in boat-local space
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            float totalWater = 0f;
            float minAirIntegrity = 1f;
            float maxMinAirFraction = 0.2f;

            var combinedExternalSources = new List<ExternalWaterSource>();

            foreach (var frag in group)
            {
                var r = rects[frag];
                min = Vector2.Min(min, r.Min);
                max = Vector2.Max(max, r.Max);

                totalWater += frag.WaterArea;
                minAirIntegrity = Mathf.Min(minAirIntegrity, frag.airIntegrity);
                maxMinAirFraction = Mathf.Max(maxMinAirFraction, frag.minAirFraction);

                foreach (var src in frag.externalWaterSources)
                    if (src != null && !combinedExternalSources.Contains(src))
                        combinedExternalSources.Add(src);
            }

            // Create merged compartment object at BOAT root so localCorners are in boat-local coordinates
            var template = group[0]; // visual template for this merged compartment
            var mergedGO = new GameObject($"CompartmentMerged_{mergeIndex++}");
            Undo.RegisterCreatedObjectUndo(mergedGO, "Create Merged Compartment");
            mergedGO.transform.SetParent(boat.transform, false);
            mergedGO.transform.localPosition = Vector3.zero;
            mergedGO.transform.localRotation = Quaternion.identity;
            mergedGO.transform.localScale = Vector3.one;

            var merged = mergedGO.AddComponent<Compartment>();

            // Set geometry to a rectangle in merged's local space (which is boat-local)
            SetCompartmentRectGeometry(merged, min, max);
            CloneAndResizeVisuals(template, merged, min, max);

            // Carry over some state
            merged.compartmentName = mergedGO.name;
            merged.minAirFraction = maxMinAirFraction;
            merged.airIntegrity = minAirIntegrity;
            merged.externalWaterSources = combinedExternalSources;

            // Fill with water AFTER geometry is set
            merged.AcceptWater(totalWater);

            newCompartments.Add(merged);

            // Remap all fragments -> merged
            foreach (var frag in group)
                remap[frag] = merged;

            // Stash old fragment gameobjects so they stop contributing anything
            foreach (var frag in group)
            {
                if (frag == null) continue;
                Undo.SetTransformParent(frag.transform, stash, "Stash Old Compartment Fragment");
            }
        }

        // Remap connections A/B to new compartments, removing “internal” connections
        if (boat.Connections != null)
        {
            for (int i = boat.Connections.Count - 1; i >= 0; i--)
            {
                var conn = boat.Connections[i];
                if (conn == null) continue;

                if (conn.A != null && remap.TryGetValue(conn.A, out var newA)) conn.A = newA;
                if (conn.B != null && remap.TryGetValue(conn.B, out var newB)) conn.B = newB;

                // If it now connects the same compartment, it’s meaningless: remove
                if (conn.A != null && conn.A == conn.B)
                    boat.Connections.RemoveAt(i);
            }
        }

        // Replace boat compartments list
        boat.Compartments = newCompartments;

        // Rebuild each compartment’s connection list (like your Boat.Awake does)
        foreach (var c in boat.Compartments)
            c.connections.Clear();

        foreach (var conn in boat.Connections)
        {
            if (conn == null) continue;
            if (conn.A != null && !conn.A.connections.Contains(conn)) conn.A.connections.Add(conn);
            if (conn.B != null && !conn.B.connections.Contains(conn)) conn.B.connections.Add(conn);
        }

        EditorUtility.SetDirty(boat);
        Debug.Log($"[Merge Compartments] Done. Now {boat.Compartments.Count} compartments, {boat.Connections.Count} connections.", boat);
    }

    private static bool AreTouching(RectL a, RectL b)
    {
        // Overlap extents
        float overlapX = Mathf.Min(a.MaxX, b.MaxX) - Mathf.Max(a.MinX, b.MinX);
        float overlapY = Mathf.Min(a.MaxY, b.MaxY) - Mathf.Max(a.MinY, b.MinY);

        bool touchVerticalEdge =
            overlapY > EPS &&
            (Mathf.Abs(a.MaxX - b.MinX) <= EPS || Mathf.Abs(b.MaxX - a.MinX) <= EPS);

        bool touchHorizontalEdge =
            overlapX > EPS &&
            (Mathf.Abs(a.MaxY - b.MinY) <= EPS || Mathf.Abs(b.MaxY - a.MinY) <= EPS);

        // Also treat overlaps as touching (authoring error but we can merge anyway)
        bool overlaps = overlapX > EPS && overlapY > EPS;

        return touchVerticalEdge || touchHorizontalEdge || overlaps;
    }

    private static bool TryGetCompartmentRectBoatLocal(Boat boat, Compartment c, out RectL rect)
    {
        // Prefer BoxCollider2D since your authoring creates it
        var box = c.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            // Compute the 4 corners of the box in world, then convert to BOAT local and AABB them.
            var t = box.transform;
            Vector2 off = box.offset;
            Vector2 half = box.size * 0.5f;

            var w0 = (Vector2)t.TransformPoint(off + new Vector2(-half.x, -half.y));
            var w1 = (Vector2)t.TransformPoint(off + new Vector2(half.x, -half.y));
            var w2 = (Vector2)t.TransformPoint(off + new Vector2(half.x, half.y));
            var w3 = (Vector2)t.TransformPoint(off + new Vector2(-half.x, half.y));

            Vector2 b0 = boat.transform.InverseTransformPoint(w0);
            Vector2 b1 = boat.transform.InverseTransformPoint(w1);
            Vector2 b2 = boat.transform.InverseTransformPoint(w2);
            Vector2 b3 = boat.transform.InverseTransformPoint(w3);

            var min = Vector2.Min(Vector2.Min(b0, b1), Vector2.Min(b2, b3));
            var max = Vector2.Max(Vector2.Max(b0, b1), Vector2.Max(b2, b3));
            rect = new RectL(min, max);
            return true;
        }

        // Fallback: use Compartment.GetWorldCorners() if you have it meaningful
        var poly = c.GetWorldCorners();
        if (poly != null && poly.Length == 4)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var w in poly)
            {
                var bl = (Vector2)boat.transform.InverseTransformPoint(w);
                min = Vector2.Min(min, bl);
                max = Vector2.Max(max, bl);
            }
            rect = new RectL(min, max);
            return true;
        }

        rect = default;
        return false;
    }

    private static void SetCompartmentRectGeometry(Compartment c, Vector2 minBoatLocal, Vector2 maxBoatLocal)
    {
        // We set BOTH:
        // - public p0..p3 (used by FloorY/CeilingY/Width/Height)
        // - private localCorners[] (used by GetWorldCorners and polygon clipping)
        //
        // Coordinates are in the Compartment's local space.
        // We created merged compartments at boat root, so this equals boat-local.

        Vector2 p0 = new Vector2(minBoatLocal.x, maxBoatLocal.y); // top-left
        Vector2 p1 = new Vector2(maxBoatLocal.x, maxBoatLocal.y); // top-right
        Vector2 p2 = new Vector2(maxBoatLocal.x, minBoatLocal.y); // bottom-right
        Vector2 p3 = new Vector2(minBoatLocal.x, minBoatLocal.y); // bottom-left

        c.p0 = p0; c.p1 = p1; c.p2 = p2; c.p3 = p3;

        // Now write the private serialized field "localCorners"
        var so = new SerializedObject(c);
        var prop = so.FindProperty("localCorners");
        if (prop != null && prop.isArray && prop.arraySize == 4)
        {
            prop.GetArrayElementAtIndex(0).vector2Value = p0;
            prop.GetArrayElementAtIndex(1).vector2Value = p1;
            prop.GetArrayElementAtIndex(2).vector2Value = p2;
            prop.GetArrayElementAtIndex(3).vector2Value = p3;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Make sure water surface recomputes
        c.RecomputeWaterSurface();
        EditorUtility.SetDirty(c);
    }

    private static void CloneAndResizeVisuals(Compartment template, Compartment merged, Vector2 minBoatLocal, Vector2 maxBoatLocal)
    {
        if (template == null || merged == null) return;

        // 1) Add CompartmentWaterRenderer directly to merged compartment root
        var templateWR = template.GetComponentInChildren<CompartmentWaterRenderer>(true);
        Debug.Log($"[MergeCompartments] templateWR={(templateWR != null ? templateWR.name : "NULL")}");

        if (templateWR != null)
        {
            // Ensure mesh components exist
            if (merged.GetComponent<MeshFilter>() == null)
                Undo.AddComponent<MeshFilter>(merged.gameObject);

            if (merged.GetComponent<MeshRenderer>() == null)
                Undo.AddComponent<MeshRenderer>(merged.gameObject);

            // Add water renderer (no cloning, no children)
            var mergedWR = merged.GetComponent<CompartmentWaterRenderer>();
            if (mergedWR == null)
                mergedWR = Undo.AddComponent<CompartmentWaterRenderer>(merged.gameObject);

            // Copy serialized config from template
            UnityEditor.EditorUtility.CopySerialized(templateWR, mergedWR);

            // Let Awake() / OnEnable() do their thing naturally
            mergedWR.enabled = false;
            mergedWR.enabled = true;

            Debug.Log("[MergeCompartments] CompartmentWaterRenderer added to merged compartment root.", merged);
        }

        // 2) Clone background sprite child (visual-only is fine as a child)
        Transform bgT = null;
        var bgNamed = template.transform.Find("Background");
        if (bgNamed != null) bgT = bgNamed;

        if (bgT == null)
        {
            var sr = template.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) bgT = sr.transform;
        }

        if (bgT != null)
        {
            var bgGO = UnityEngine.Object.Instantiate(bgT.gameObject);
            Undo.RegisterCreatedObjectUndo(bgGO, "Clone Background");
            bgGO.name = bgT.gameObject.name;
            bgGO.transform.SetParent(merged.transform, false);

            ResizeBackground(bgGO, minBoatLocal, maxBoatLocal);
        }
    }


    private static void ResizeBackground(GameObject bgGO, Vector2 minBoatLocal, Vector2 maxBoatLocal)
    {
        var sr = bgGO.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        float w = Mathf.Max(0.01f, maxBoatLocal.x - minBoatLocal.x);
        float h = Mathf.Max(0.01f, maxBoatLocal.y - minBoatLocal.y);

        // Center the background in the merged rect
        var center = (minBoatLocal + maxBoatLocal) * 0.5f;
        bgGO.transform.localPosition = new Vector3(center.x, center.y, bgGO.transform.localPosition.z);

        // Best-case: Tiled or Sliced draw mode supports sizing directly
        if (sr.drawMode != SpriteDrawMode.Simple)
        {
            sr.size = new Vector2(w, h);
            bgGO.transform.localScale = Vector3.one;
            return;
        }

        // Fallback: scale based on sprite bounds (works even for Simple)
        var spriteSize = sr.sprite.bounds.size; // local units
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f) return;

        bgGO.transform.localScale = new Vector3(w / spriteSize.x, h / spriteSize.y, 1f);
    }

    // --- Water renderer binding/resizing using reflection so we don’t hard-depend on your API ---
    private static void TryBindWaterRenderer(GameObject waterGO, Compartment merged)
    {
        if (waterGO == null || merged == null) return;

        // If your class is WaterRenderer, this will work.
        // If not, either change the type above or rely on reflection.
        var comps = waterGO.GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == null) continue;

            var t = c.GetType();

            // Field: public Compartment compartment;
            var f = t.GetField("compartment");
            if (f != null && f.FieldType == typeof(Compartment))
            {
                f.SetValue(c, merged);
                continue;
            }

            // Property: public Compartment Compartment {get;set;}
            var p = t.GetProperty("Compartment");
            if (p != null && p.PropertyType == typeof(Compartment) && p.CanWrite)
            {
                p.SetValue(c, merged);
                continue;
            }

            // Method: SetCompartment(Compartment c)
            var m = t.GetMethod("SetCompartment", new[] { typeof(Compartment) });
            if (m != null)
            {
                m.Invoke(c, new object[] { merged });
                continue;
            }
        }
    }

    private static void TryResizeWaterRenderer(GameObject waterGO, Vector2 minBoatLocal, Vector2 maxBoatLocal)
    {
        if (waterGO == null) return;

        float w = Mathf.Max(0.01f, maxBoatLocal.x - minBoatLocal.x);
        float h = Mathf.Max(0.01f, maxBoatLocal.y - minBoatLocal.y);
        var center = (minBoatLocal + maxBoatLocal) * 0.5f;

        // Many water renderers just read Compartment polygon each frame, so resizing may be unnecessary.
        // But if yours needs explicit size, we try a couple common method names.
        var comps = waterGO.GetComponents<Component>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            var t = c.GetType();

            var m1 = t.GetMethod("SetSize", new[] { typeof(Vector2) });
            if (m1 != null)
            {
                m1.Invoke(c, new object[] { new Vector2(w, h) });
                continue;
            }

            var m2 = t.GetMethod("SetRect", new[] { typeof(Vector2), typeof(Vector2) });
            if (m2 != null)
            {
                m2.Invoke(c, new object[] { minBoatLocal, maxBoatLocal });
                continue;
            }
        }

        // Also place it at the compartment center if it’s supposed to live there
        var tr = waterGO.transform;
        tr.localPosition = new Vector3(center.x, center.y, tr.localPosition.z);
    }

    private static void InvokeIfExists(Component c, string methodName)
    {
        if (c == null) return;

        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var m = c.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
        if (m != null) m.Invoke(c, null);
    }
}
#endif
