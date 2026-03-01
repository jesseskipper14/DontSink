using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScubaTankAirSource : MonoBehaviour, IAirSource
{
    [Header("Tank")]
    [Min(0f)] public float tankMax = 120f;
    [Min(0f)] public float tankCurrent = 120f;

    [Tooltip("Units/sec supplied while underwater.")]
    [Min(0f)] public float supplyPerSecond = 10f;

    [Tooltip("Optional extra max air 'lung capacity' bonus while equipped.")]
    [Min(0f)] public float maxAirBonus = 0f;

    public float MaxAirBonus => maxAirBonus;

    public float GetAirFlowPerSecond(PlayerAirState air, float dt)
    {
        if (!air.IsUnderwater) return 0f;
        if (tankCurrent <= 0.0001f) return 0f;

        float supply = supplyPerSecond;

        // Can't supply more than remaining tank / dt
        float maxSupplyThisTick = tankCurrent / Mathf.Max(0.0001f, dt);
        supply = Mathf.Min(supply, maxSupplyThisTick);

        tankCurrent -= supply * dt;
        tankCurrent = Mathf.Max(0f, tankCurrent);

        // Positive flow adds air back, countering underwater drain.
        return supply;
    }
}