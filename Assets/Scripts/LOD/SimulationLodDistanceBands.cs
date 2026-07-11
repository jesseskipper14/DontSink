using UnityEngine;

[System.Serializable]
public sealed class SimulationLodDistanceBands
{
    [Min(0f)] public float fullRadius = 30f;
    [Min(0f)] public float reducedRadius = 55f;
    [Min(0f)] public float frozenRadius = 75f;

    [Tooltip("Prevents rapid flipping when near a boundary.")]
    [Min(0f)] public float hysteresis = 5f;

    public SimulationLodState Evaluate(float distance, SimulationLodState current)
    {
        float full = Mathf.Max(0f, fullRadius);
        float reduced = Mathf.Max(full, reducedRadius);
        float frozen = Mathf.Max(reduced, frozenRadius);
        float h = Mathf.Max(0f, hysteresis);

        switch (current)
        {
            case SimulationLodState.Full:
                if (distance > reduced + h)
                    return SimulationLodState.Reduced;
                return SimulationLodState.Full;

            case SimulationLodState.Reduced:
                if (distance <= full - h)
                    return SimulationLodState.Full;

                if (distance > frozen + h)
                    return SimulationLodState.Frozen;

                return SimulationLodState.Reduced;

            case SimulationLodState.Frozen:
                if (distance <= reduced - h)
                    return SimulationLodState.Reduced;

                return SimulationLodState.Frozen;

            default:
                if (distance <= full)
                    return SimulationLodState.Full;

                if (distance <= reduced)
                    return SimulationLodState.Reduced;

                return SimulationLodState.Frozen;
        }
    }
}