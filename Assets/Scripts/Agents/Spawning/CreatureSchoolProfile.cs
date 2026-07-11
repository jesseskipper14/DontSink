using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Creatures/Creature School Profile", fileName = "CreatureSchoolProfile")]
public sealed class CreatureSchoolProfile : ScriptableObject
{
    [Header("School Macro Movement")]
    [SerializeField, Min(0f)] private float schoolSpeed = 1.1f;
    [SerializeField, Min(0f)] private float schoolAcceleration = 2.5f;
    [SerializeField] private Vector2 targetChangeIntervalRange = new Vector2(3f, 7f);
    [SerializeField, Min(0.05f)] private float targetReachDistance = 0.55f;

    [Header("School Shape")]
    [SerializeField, Min(0.05f)] private float schoolRadius = 1.75f;
    [SerializeField, Min(0f)] private float cohesionStrength = 1.8f;

    [Header("Individual Movement")]
    [SerializeField, Min(0f)] private float memberMaxSpeed = 2.2f;
    [SerializeField, Min(0f)] private float memberAcceleration = 7f;
    [SerializeField, Min(0f)] private float individualNoiseStrength = 0.35f;
    [SerializeField, Min(0.01f)] private float individualNoiseFrequency = 1.2f;

    [Header("Separation")]
    [SerializeField, Min(0f)] private float separationRadius = 0.35f;
    [SerializeField, Min(0f)] private float separationStrength = 1.5f;

    [Header("Threat Response")]
    [SerializeField, Min(0f)] private float noticeRadius = 3.5f;
    [SerializeField, Min(0f)] private float fleeRadius = 1.8f;
    [SerializeField, Min(0f)] private float fleeSpeedMultiplier = 1.85f;
    [SerializeField, Min(0f)] private float panicRiseSpeed = 8f;
    [SerializeField, Min(0f)] private float panicDecaySpeed = 1.6f;
    [SerializeField, Min(0.02f)] private float threatScanInterval = 0.18f;

    [Header("Visual")]
    [SerializeField] private bool flipVisualOnXMovement = true;

    public float SchoolSpeed => Mathf.Max(0f, schoolSpeed);
    public float SchoolAcceleration => Mathf.Max(0f, schoolAcceleration);
    public float TargetReachDistance => Mathf.Max(0.05f, targetReachDistance);
    public float SchoolRadius => Mathf.Max(0.05f, schoolRadius);
    public float CohesionStrength => Mathf.Max(0f, cohesionStrength);

    public float MemberMaxSpeed => Mathf.Max(0f, memberMaxSpeed);
    public float MemberAcceleration => Mathf.Max(0f, memberAcceleration);
    public float IndividualNoiseStrength => Mathf.Max(0f, individualNoiseStrength);
    public float IndividualNoiseFrequency => Mathf.Max(0.01f, individualNoiseFrequency);

    public float SeparationRadius => Mathf.Max(0f, separationRadius);
    public float SeparationStrength => Mathf.Max(0f, separationStrength);

    public float NoticeRadius => Mathf.Max(0f, noticeRadius);
    public float FleeRadius => Mathf.Max(0f, fleeRadius);
    public float FleeSpeedMultiplier => Mathf.Max(0f, fleeSpeedMultiplier);
    public float PanicRiseSpeed => Mathf.Max(0f, panicRiseSpeed);
    public float PanicDecaySpeed => Mathf.Max(0f, panicDecaySpeed);
    public float ThreatScanInterval => Mathf.Max(0.02f, threatScanInterval);

    public bool FlipVisualOnXMovement => flipVisualOnXMovement;

    public float GetRandomTargetChangeInterval(System.Random rng)
    {
        rng ??= new System.Random();

        float min = Mathf.Max(0.1f, Mathf.Min(targetChangeIntervalRange.x, targetChangeIntervalRange.y));
        float max = Mathf.Max(min, Mathf.Max(targetChangeIntervalRange.x, targetChangeIntervalRange.y));

        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        schoolSpeed = Mathf.Max(0f, schoolSpeed);
        schoolAcceleration = Mathf.Max(0f, schoolAcceleration);
        targetReachDistance = Mathf.Max(0.05f, targetReachDistance);
        schoolRadius = Mathf.Max(0.05f, schoolRadius);

        memberMaxSpeed = Mathf.Max(0f, memberMaxSpeed);
        memberAcceleration = Mathf.Max(0f, memberAcceleration);
        individualNoiseFrequency = Mathf.Max(0.01f, individualNoiseFrequency);

        separationRadius = Mathf.Max(0f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);

        noticeRadius = Mathf.Max(0f, noticeRadius);
        fleeRadius = Mathf.Max(0f, fleeRadius);
        threatScanInterval = Mathf.Max(0.02f, threatScanInterval);
    }
#endif
}