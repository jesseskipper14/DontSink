using UnityEngine;

// CameraManager positions the active camera in LateUpdate at order -200.
[DefaultExecutionOrder(0)]
public class BackgroundFollower : MonoBehaviour
{
    [Tooltip("Fallback for scenes without a local gameplay camera, such as authored previews.")]
    public Transform target;
    public Vector3 offset;
    public bool lockY = true;

    [Range(0f, 1f)]
    public float celestialScale = 0.1f;

    private void LateUpdate()
    {
        var manager = CameraManager.Instance;
        var camera = manager != null ? manager.ActiveCamera : null;
        var follow = camera != null ? camera.transform : target;
        if (follow == null) return;

        Vector3 pos = follow.position + offset;
        // Camera depth is a projection setting, not the background's draw depth.
        if (camera != null) pos.z = transform.position.z;
        if (lockY) pos.y = offset.y;
        transform.position = pos;

        CelestialBodyManager celestial = ServiceRoot.Instance?.CelestialBodyManager;
        if (celestial != null)
            celestial.SetHorizontalOffset(transform.position.x * celestialScale);
    }
}
