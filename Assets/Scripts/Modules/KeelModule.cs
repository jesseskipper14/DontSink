using UnityEngine;

/// <summary>
/// Installed boat module that resists simulation-owned environmental course
/// disturbance.
///
/// This intentionally does not simulate hydrodynamics. The keel contributes two
/// gameplay-facing handling values and the boat-level handling aggregator decides
/// how installed modules combine.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeelModule :
    MonoBehaviour,
    IBoatDisturbanceResistanceContributor
{
    [Header("Disturbance Resistance")]

    [Tooltip("Fraction of lateral environmental course disturbance resisted. 0 = none, 1 = fully resisted.")]
    [SerializeField, Range(0f, 1f)]
    private float lateralDisturbanceResistance = 0.30f;

    [Tooltip("Fraction of environmental yaw disturbance resisted. 0 = none, 1 = fully resisted.")]
    [SerializeField, Range(0f, 1f)]
    private float yawDisturbanceResistance = 0.30f;

    public bool IsHandlingContributionActive =>
        isActiveAndEnabled;

    public float LateralDisturbanceResistanceContribution =>
        Mathf.Clamp01(
            lateralDisturbanceResistance);

    public float YawDisturbanceResistanceContribution =>
        Mathf.Clamp01(
            yawDisturbanceResistance);

#if UNITY_EDITOR
    private void OnValidate()
    {
        lateralDisturbanceResistance =
            Mathf.Clamp01(
                lateralDisturbanceResistance);

        yawDisturbanceResistance =
            Mathf.Clamp01(
                yawDisturbanceResistance);
    }
#endif
}
