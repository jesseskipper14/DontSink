using UnityEngine;

public sealed partial class CelestialSkyRenderer
{
    private Vector3 SkyViewportToWorld(Vector2 viewport) =>
        BoatObservationPresentationController.ProjectSkyViewport(targetCamera, viewport, ResolveProjectionDepth());
}
