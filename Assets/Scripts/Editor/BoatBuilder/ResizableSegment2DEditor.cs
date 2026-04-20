#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ResizableSegment2D))]
public sealed class ResizableSegment2DEditor : Editor
{
    private const float HandleSize = 0.12f;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var segment = (ResizableSegment2D)target;

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Resize Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Sync Size From Collider"))
            {
                Undo.RecordObject(segment, "Sync Segment Size From Collider");
                segment.SyncSizeFromCollider();
                EditorUtility.SetDirty(segment);
            }

            if (GUILayout.Button("Normalize Scales And Apply"))
            {
                Undo.RecordObject(segment.transform, "Normalize Segment Scale");
                Undo.RecordObject(segment, "Normalize Segment Scale");

                if (segment.SpriteRenderer != null)
                    Undo.RecordObject(segment.SpriteRenderer.transform, "Normalize Segment Visual Scale");

                segment.NormalizeScalesAndApply();

                EditorUtility.SetDirty(segment);
                EditorUtility.SetDirty(segment.transform);

                if (segment.SpriteRenderer != null)
                    EditorUtility.SetDirty(segment.SpriteRenderer);
            }
        }
    }

    private void OnSceneGUI()
    {
        var segment = (ResizableSegment2D)target;
        if (segment == null)
            return;

        Transform t = segment.transform;

        Vector3 center = t.position;
        float halfW = segment.Width * 0.5f;
        float halfH = segment.Height * 0.5f;

        bool horizontal =
            segment.Axis == ResizableSegment2D.ResizeAxis.Horizontal ||
            segment.Axis == ResizableSegment2D.ResizeAxis.Both;

        bool vertical =
            segment.Axis == ResizableSegment2D.ResizeAxis.Vertical ||
            segment.Axis == ResizableSegment2D.ResizeAxis.Both;

        Handles.color = Color.yellow;

        if (horizontal)
        {
            Vector3 left = center - t.right * halfW;
            Vector3 right = center + t.right * halfW;

            DrawWidthHandle(segment, left, positiveSide: false);
            DrawWidthHandle(segment, right, positiveSide: true);

            Handles.DrawLine(left, right);
        }

        if (vertical)
        {
            Vector3 bottom = center - t.up * halfH;
            Vector3 top = center + t.up * halfH;

            DrawHeightHandle(segment, bottom, positiveSide: false);
            DrawHeightHandle(segment, top, positiveSide: true);

            Handles.DrawLine(bottom, top);
        }
    }

    private static void DrawWidthHandle(
        ResizableSegment2D segment,
        Vector3 handleWorldPos,
        bool positiveSide)
    {
        Transform t = segment.transform;

        EditorGUI.BeginChangeCheck();

        float size = HandleUtility.GetHandleSize(handleWorldPos) * HandleSize;
        Vector3 draggedWorldPos = Handles.Slider(
            handleWorldPos,
            t.right,
            size,
            Handles.CubeHandleCap,
            0f);

        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(segment, "Resize Segment Width");
        Undo.RecordObject(t, "Move Segment Center While Resizing");

        float oldWidth = segment.Width;
        Vector3 fixedEdgeWorld;
        float rawWidth;

        if (positiveSide)
        {
            // Dragging right edge. Left edge stays fixed.
            fixedEdgeWorld = t.position - t.right * (oldWidth * 0.5f);
            rawWidth = Vector3.Dot(draggedWorldPos - fixedEdgeWorld, t.right);
            rawWidth = Mathf.Max(0.01f, rawWidth);

            float newWidth = segment.SnapSize(rawWidth);
            Vector3 newCenter = fixedEdgeWorld + t.right * (newWidth * 0.5f);

            t.position = newCenter;
            segment.ApplyWidth(newWidth);
        }
        else
        {
            // Dragging left edge. Right edge stays fixed.
            fixedEdgeWorld = t.position + t.right * (oldWidth * 0.5f);
            rawWidth = Vector3.Dot(fixedEdgeWorld - draggedWorldPos, t.right);
            rawWidth = Mathf.Max(0.01f, rawWidth);

            float newWidth = segment.SnapSize(rawWidth);
            Vector3 newCenter = fixedEdgeWorld - t.right * (newWidth * 0.5f);

            t.position = newCenter;
            segment.ApplyWidth(newWidth);
        }

        EditorUtility.SetDirty(segment);
        EditorUtility.SetDirty(t);
    }

    private static void DrawHeightHandle(
        ResizableSegment2D segment,
        Vector3 handleWorldPos,
        bool positiveSide)
    {
        Transform t = segment.transform;

        EditorGUI.BeginChangeCheck();

        float size = HandleUtility.GetHandleSize(handleWorldPos) * HandleSize;
        Vector3 draggedWorldPos = Handles.Slider(
            handleWorldPos,
            t.up,
            size,
            Handles.CubeHandleCap,
            0f);

        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(segment, "Resize Segment Height");
        Undo.RecordObject(t, "Move Segment Center While Resizing");

        float oldHeight = segment.Height;
        Vector3 fixedEdgeWorld;
        float rawHeight;

        if (positiveSide)
        {
            // Dragging top edge. Bottom edge stays fixed.
            fixedEdgeWorld = t.position - t.up * (oldHeight * 0.5f);
            rawHeight = Vector3.Dot(draggedWorldPos - fixedEdgeWorld, t.up);
            rawHeight = Mathf.Max(0.01f, rawHeight);

            float newHeight = segment.SnapSize(rawHeight);
            Vector3 newCenter = fixedEdgeWorld + t.up * (newHeight * 0.5f);

            t.position = newCenter;
            segment.ApplyHeight(newHeight);
        }
        else
        {
            // Dragging bottom edge. Top edge stays fixed.
            fixedEdgeWorld = t.position + t.up * (oldHeight * 0.5f);
            rawHeight = Vector3.Dot(fixedEdgeWorld - draggedWorldPos, t.up);
            rawHeight = Mathf.Max(0.01f, rawHeight);

            float newHeight = segment.SnapSize(rawHeight);
            Vector3 newCenter = fixedEdgeWorld - t.up * (newHeight * 0.5f);

            t.position = newCenter;
            segment.ApplyHeight(newHeight);
        }

        EditorUtility.SetDirty(segment);
        EditorUtility.SetDirty(t);
    }
}
#endif