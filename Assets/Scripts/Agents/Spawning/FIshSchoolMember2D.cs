using UnityEngine;

[DisallowMultipleComponent]
public sealed class FishSchoolMember2D : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool drawGizmos;

    private Rigidbody2D rb;

    private CreatureSchoolProfile profile;
    private FishSchoolController school;
    private AgentSpawnZone zone;

    private Vector2 schoolOffset;
    private Vector2 noiseSeed;

    public FishSchoolController School => school;
    public CreatureSchoolProfile Profile => profile;
    public AgentSpawnZone Zone => zone;
    public Vector2 SchoolOffset => schoolOffset;
    public Vector2 NoiseSeed => noiseSeed;

    public Vector2 Position
    {
        get
        {
            if (rb != null)
                return rb.position;

            return transform.position;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnDisable()
    {
        if (school != null)
            school.Unregister(this);
    }

    public void InitializeSolo(
        CreatureSchoolProfile schoolProfile,
        AgentSpawnZone spawnZone,
        int seed)
    {
        if (school != null)
            school.Unregister(this);

        school = null;
        profile = schoolProfile;
        zone = spawnZone;

        System.Random rng = new System.Random(seed);
        schoolOffset = Vector2.zero;
        noiseSeed = RandomInsideCircle(rng, 100f);
    }

    public void JoinSchool(
        FishSchoolController newSchool,
        CreatureSchoolProfile schoolProfile,
        Vector2 offset,
        int seed)
    {
        if (school != null)
            school.Unregister(this);

        school = newSchool;
        profile = schoolProfile != null
            ? schoolProfile
            : newSchool != null
                ? newSchool.Profile
                : null;

        zone = newSchool != null ? newSchool.Zone : null;
        schoolOffset = offset;

        System.Random rng = new System.Random(seed);
        noiseSeed = RandomInsideCircle(rng, 100f);

        if (school != null)
            school.Register(this);
    }

    public Vector2 GetNoiseVector(float frequency, float strength)
    {
        frequency = Mathf.Max(0.01f, frequency);
        strength = Mathf.Max(0f, strength);

        float t = Time.time * frequency;

        float x = Mathf.PerlinNoise(noiseSeed.x, t) - 0.5f;
        float y = Mathf.PerlinNoise(noiseSeed.y, t + 17.37f) - 0.5f;

        return new Vector2(x, y) * strength;
    }

    private static Vector2 RandomInsideCircle(System.Random rng, float radius)
    {
        rng ??= new System.Random();

        float angle = Mathf.Lerp(0f, Mathf.PI * 2f, (float)rng.NextDouble());
        float r = Mathf.Sqrt((float)rng.NextDouble()) * Mathf.Max(0f, radius);

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, 0.12f);

        if (school != null)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.75f);
            Gizmos.DrawLine(transform.position, school.AnchorPosition + schoolOffset);
        }
    }
}