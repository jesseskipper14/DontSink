using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResizableSegment2D))]
public sealed class ExteriorShellAuthoring : MonoBehaviour
{
    [SerializeField] private ResizableSegment2D resizable;

    private void Reset()
    {
        resizable = GetComponent<ResizableSegment2D>();

        BoatVisualMarker marker = GetComponent<BoatVisualMarker>();
        if (marker == null)
            marker = gameObject.AddComponent<BoatVisualMarker>();

#if UNITY_EDITOR
        marker.EditorSetCategory(BoatVisualCategory.ExteriorShell);
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-fit To Boat Interior Bounds")]
    private void EditorAutoFitToBoatInteriorBounds()
    {
        if (resizable == null)
            resizable = GetComponent<ResizableSegment2D>();

        Boat boat = GetComponentInParent<Boat>();
        if (boat == null)
        {
            Debug.LogWarning("[ExteriorShellAuthoring] No Boat found in parents.", this);
            return;
        }

        Compartment[] compartments = boat.GetComponentsInChildren<Compartment>(true);
        if (compartments == null || compartments.Length == 0)
        {
            Debug.LogWarning("[ExteriorShellAuthoring] No compartments found to fit shell to.", this);
            return;
        }

        bool hasAny = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

        foreach (Compartment c in compartments)
        {
            if (c == null)
                continue;

            Vector2[] corners = c.GetWorldCorners();
            if (corners == null)
                continue;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 localToBoat = boat.transform.InverseTransformPoint(corners[i]);
                min = Vector3.Min(min, localToBoat);
                max = Vector3.Max(max, localToBoat);
                hasAny = true;
            }
        }

        if (!hasAny)
        {
            Debug.LogWarning("[ExteriorShellAuthoring] Could not compute interior bounds.", this);
            return;
        }

        Bounds boatLocalBounds = new Bounds((min + max) * 0.5f, max - min);

        UnityEditor.Undo.RecordObject(transform, "Auto-fit Exterior Shell");
        UnityEditor.Undo.RecordObject(resizable, "Auto-fit Exterior Shell");

        transform.position = boat.transform.TransformPoint(boatLocalBounds.center);
        transform.rotation = boat.transform.rotation;

        resizable.ApplySize(boatLocalBounds.size.x, boatLocalBounds.size.y);

        UnityEditor.EditorUtility.SetDirty(transform);
        UnityEditor.EditorUtility.SetDirty(resizable);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log(
            $"[ExteriorShellAuthoring] Auto-fit shell to interior bounds. size={boatLocalBounds.size}, center={boatLocalBounds.center}",
            this);
    }
#endif
}