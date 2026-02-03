using UnityEngine;

public enum ExternalWaterSourceType
{
    Rain,
    Sea,
    SeaBreach,
    Hose,
}

[System.Serializable]
public class ExternalWaterSource
{
    public string name = "Source";
    public ExternalWaterSourceType type = ExternalWaterSourceType.Rain;

    [Tooltip("Rate in meters per second of water entering the compartment")]
    public float rate = 0.1f;

    [Tooltip("Current state of the source")]
    public bool IsActive = true;

    /// <summary>
    /// Compute water contribution for this timestep
    /// </summary>
    public float GetWaterContribution(float deltaTime)
    {
        return rate * deltaTime;
    }
}
