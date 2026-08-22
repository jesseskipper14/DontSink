using UnityEngine;

/// <summary>
/// Boat-owned authoritative piloting/navigation state for the current scene.
///
/// Important:
/// - Player input does not live here.
/// - The mini-game overlay does not own or reset this state.
/// - Leaving/dying/disconnecting from the helm does not reset throttle, rudder,
///   position, velocity, heading, or angular velocity.
/// - Save/travel persistence can snapshot these values later.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoatPilotingState : MonoBehaviour
{
    [Header("Physical Controls")]
    [Tooltip("-1 = full reverse, 0 = neutral, +1 = full forward.")]
    [SerializeField, Range(-1f, 1f)] private float throttle01;
    [SerializeField] private float rudderDegrees;

    [Header("Virtual Navigation")]
    [Tooltip("+Y is forward along the current navigation space; +X is starboard/cross-track.")]
    [SerializeField] private Vector2 navigationPosition;

    [SerializeField] private Vector2 navigationVelocity;

    [Tooltip("0 = up/forward in the piloting view. Positive rotates clockwise/starboard.")]
    [SerializeField] private float headingDegrees;

    [SerializeField] private float angularVelocityDegrees;

    /// <summary>
    /// Persistent signed throttle position.
    /// -1 = full reverse, 0 = neutral, +1 = full forward.
    /// </summary>
    public float Throttle => throttle01;

    // Compatibility alias for the first prototype. Do not use for new code:
    // this value is signed, not 0..1 anymore.
    public float Throttle01 => throttle01;

    public float RudderDegrees => rudderDegrees;

    public Vector2 NavigationPosition => navigationPosition;
    public Vector2 NavigationVelocity => navigationVelocity;

    public float HeadingDegrees => headingDegrees;
    public float AngularVelocityDegrees => angularVelocityDegrees;

    internal void SetControlPositions(float throttle, float rudderDeg)
    {
        throttle01 = Mathf.Clamp(throttle, -1f, 1f);
        rudderDegrees = rudderDeg;
    }

    internal void SetNavigationState(
        Vector2 position,
        Vector2 velocity,
        float headingDeg,
        float angularVelocityDeg)
    {
        navigationPosition = position;
        navigationVelocity = velocity;
        headingDegrees = NormalizeSignedDegrees(headingDeg);
        angularVelocityDegrees = angularVelocityDeg;
    }

    [ContextMenu("Reset Piloting State")]
    public void ResetRuntimeState()
    {
        throttle01 = 0f;
        rudderDegrees = 0f;
        navigationPosition = Vector2.zero;
        navigationVelocity = Vector2.zero;
        headingDegrees = 0f;
        angularVelocityDegrees = 0f;
    }

    private void OnValidate()
    {
        throttle01 = Mathf.Clamp(throttle01, -1f, 1f);
        headingDegrees = NormalizeSignedDegrees(headingDegrees);
    }

    private static float NormalizeSignedDegrees(float degrees)
    {
        return Mathf.Repeat(degrees + 180f, 360f) - 180f;
    }
}