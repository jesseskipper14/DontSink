public interface IAirSource
{
    /// <summary>Extra max air capacity provided by this source (units).</summary>
    float MaxAirBonus { get; }

    /// <summary>
    /// Returns air flow in units/sec (+ supplies air, - consumes air).
    /// Called every fixed tick by PlayerAirState.
    /// </summary>
    float GetAirFlowPerSecond(PlayerAirState air, float dt);
}