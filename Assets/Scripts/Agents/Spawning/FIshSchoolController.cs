using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FishSchoolController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<FishSchoolMember2D> members = new();

    private CreatureSchoolProfile profile;
    private AgentSpawnZone zone;
    private System.Random rng;

    private Vector2 target;
    private Vector2 velocity;

    private float nextTargetTime;
    private float nextThreatScanTime;

    private float panic01;
    private Vector2 fleeDirection;

    public IReadOnlyList<FishSchoolMember2D> Members => members;

    public Vector2 AnchorPosition => transform.position;
    public Vector2 DesiredVelocity => velocity;
    public float Panic01 => panic01;
    public Vector2 FleeDirection => fleeDirection.sqrMagnitude > 0.001f ? fleeDirection.normalized : Vector2.right;
    public CreatureSchoolProfile Profile => profile;
    public AgentSpawnZone Zone => zone;

    public void Initialize(
        CreatureSchoolProfile schoolProfile,
        AgentSpawnZone spawnZone,
        int seed)
    {
        profile = schoolProfile;
        zone = spawnZone;
        rng = new System.Random(seed);

        target = zone != null
            ? zone.GetRandomPoint(rng)
            : (Vector2)transform.position;

        nextTargetTime = Time.time + GetTargetInterval();
        nextThreatScanTime = Time.time + GetScanOffset();
    }

    public void Register(FishSchoolMember2D member)
    {
        if (member == null)
            return;

        if (!members.Contains(member))
            members.Add(member);
    }

    public void Unregister(FishSchoolMember2D member)
    {
        if (member == null)
            return;

        members.Remove(member);
    }

    private void Update()
    {
        if (profile == null)
            return;

        float dt = Time.deltaTime;

        UpdateThreatState(dt);
        UpdateTargetIfNeeded();
        UpdateMovement(dt);
    }

    private void UpdateThreatState(float dt)
    {
        if (Time.time < nextThreatScanTime)
        {
            DecayPanic(dt);
            return;
        }

        nextThreatScanTime = Time.time + profile.ThreatScanInterval;

        float schoolNoticeRadius = profile.NoticeRadius + profile.SchoolRadius;

        if (CreatureThreatRegistry.TryGetStrongestThreat(
                AnchorPosition,
                schoolNoticeRadius,
                out CreatureThreatSource threat,
                out Vector2 away,
                out float threat01))
        {
            fleeDirection = away;

            float targetPanic = Mathf.Clamp01(threat01);

            float dist = Vector2.Distance(AnchorPosition, threat.Position);
            if (dist <= profile.FleeRadius + profile.SchoolRadius)
                targetPanic = 1f;

            panic01 = Mathf.MoveTowards(
                panic01,
                targetPanic,
                profile.PanicRiseSpeed * dt);
        }
        else
        {
            DecayPanic(dt);
        }
    }

    private void DecayPanic(float dt)
    {
        panic01 = Mathf.MoveTowards(
            panic01,
            0f,
            profile.PanicDecaySpeed * dt);
    }

    private void UpdateTargetIfNeeded()
    {
        float dist = Vector2.Distance(AnchorPosition, target);

        if (Time.time < nextTargetTime && dist > profile.TargetReachDistance)
            return;

        target = zone != null
            ? zone.GetRandomPoint(rng)
            : AnchorPosition + RandomInsideCircle(rng, profile.SchoolRadius * 2f);

        nextTargetTime = Time.time + GetTargetInterval();
    }

    private void UpdateMovement(float dt)
    {
        Vector2 pos = AnchorPosition;

        Vector2 desired;

        if (panic01 > 0.05f)
        {
            desired = FleeDirection * profile.SchoolSpeed * profile.FleeSpeedMultiplier;
        }
        else
        {
            Vector2 toTarget = target - pos;

            desired = toTarget.sqrMagnitude > 0.001f
                ? toTarget.normalized * profile.SchoolSpeed
                : Vector2.zero;
        }

        velocity = Vector2.MoveTowards(
            velocity,
            desired,
            profile.SchoolAcceleration * dt);

        Vector2 next = pos + velocity * dt;

        if (zone != null)
            next = zone.ClampWorldPoint(next);

        transform.position = next;
    }

    private float GetTargetInterval()
    {
        return profile != null
            ? profile.GetRandomTargetChangeInterval(rng)
            : 3f;
    }

    private float GetScanOffset()
    {
        if (rng == null || profile == null)
            return 0.1f;

        return Mathf.Lerp(
            0.02f,
            profile.ThreatScanInterval,
            (float)rng.NextDouble());
    }

    public Vector2 GetSeparationVector(FishSchoolMember2D requester, Vector2 requesterPosition)
    {
        if (profile == null || profile.SeparationRadius <= 0f)
            return Vector2.zero;

        Vector2 result = Vector2.zero;
        float radius = profile.SeparationRadius;
        float radiusSqr = radius * radius;

        for (int i = 0; i < members.Count; i++)
        {
            FishSchoolMember2D other = members[i];
            if (other == null || other == requester)
                continue;

            Vector2 delta = requesterPosition - other.Position;
            float sqr = delta.sqrMagnitude;

            if (sqr <= 0.0001f || sqr > radiusSqr)
                continue;

            float dist = Mathf.Sqrt(sqr);
            float strength01 = 1f - Mathf.Clamp01(dist / radius);

            result += delta.normalized * strength01;
        }

        return result * profile.SeparationStrength;
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

        Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, profile != null ? profile.SchoolRadius : 1.5f);

        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.85f);
        Gizmos.DrawLine(transform.position, target);
        Gizmos.DrawWireSphere(target, 0.15f);

        if (panic01 > 0.05f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.85f);
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + FleeDirection * 1.5f);
        }
    }
}