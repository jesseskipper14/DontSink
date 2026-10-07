using UnityEngine;

public sealed partial class CelestialSkyRenderer
{
    /// <summary>Observer's own sky origin, using the same fixed observation frame as the stars.</summary>
    public bool TryGetSurveyCenterScreen(Camera camera, out Vector2 screen)
    {
        screen = default;
        if (!isActiveAndEnabled || targetCamera == null || targetCamera != camera || projectionSettings == null) return false;
        Vector3 world = SkyViewportToWorld(new Vector2(.5f, projectionSettings.SkyViewportCenterY));
        world.z = ResolveRenderWorldZ();
        Vector3 projected = camera.WorldToScreenPoint(world);
        if (projected.z <= 0 || !camera.pixelRect.Contains(new Vector2(projected.x, projected.y))) return false;
        screen = new Vector2(projected.x, Screen.height - projected.y);
        return true;
    }

    private Vector3 SkyViewportToWorld(Vector2 viewport) =>
        BoatObservationPresentationController.ProjectSkyViewport(targetCamera, viewport, ResolveProjectionDepth());
}
