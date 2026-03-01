using UnityEngine;

[DisallowMultipleComponent]
public sealed class AmbientSurfaceAirSource : MonoBehaviour, IAirSource
{
    public float MaxAirBonus => 0f;

    [Header("Refs")]
    public PlayerAirState air;
    public WaveField wave;

    [Tooltip("Point representing where the player's mouth/head is.")]
    public Transform headPoint;

    [Header("Detection")]
    [Tooltip("Head must be this far above water to count as breathing air.")]
    public float headClearance = 0.05f;

    [Tooltip("Optional: smooth underwater detection to avoid flicker at the surface.")]
    public float hysteresis = 0.02f;

    private bool _isUnderwater;

    void Awake()
    {
        if (!air) air = GetComponent<PlayerAirState>();
        if (!headPoint) headPoint = transform;
    }

    public float GetAirFlowPerSecond(PlayerAirState airState, float dt)
    {
        if (wave == null || headPoint == null) return 0f;

        float waterY = wave.SampleHeight(headPoint.position.x);
        float headY = headPoint.position.y;

        // Hysteresis band to prevent rapid toggling at the surface
        float enter = waterY - hysteresis;
        float exit = waterY + hysteresis + headClearance;

        if (_isUnderwater)
        {
            if (headY > exit) _isUnderwater = false;
        }
        else
        {
            if (headY <= enter) _isUnderwater = true;
        }

        airState.IsUnderwater = _isUnderwater;

        // Ambient source doesn't directly add air (PlayerAirState handles surface regen).
        // This source is primarily responsible for setting IsUnderwater.
        return 0f;
    }
}