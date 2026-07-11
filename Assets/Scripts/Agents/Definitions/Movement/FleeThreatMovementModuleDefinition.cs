using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Movement Modules/Flee Threat",
    fileName = "FleeThreatMovementModule")]
public sealed class FleeThreatMovementModuleDefinition : AgentMovementModuleDefinition
{
    [Header("Threat Detection")]
    [SerializeField, Min(0f)] private float noticeRadius = 6f;
    [SerializeField, Min(0f)] private float fleeRadius = 3f;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.15f;

    [Header("Response")]
    [SerializeField, Min(0f)] private float fleeSpeed = 3.5f;
    [SerializeField, Min(0f)] private float closeRangeSpeedBoost = 4f;
    [SerializeField, Min(0f)] private float emergencyRadius = 1.5f;

    [SerializeField, Min(0f)] private float panicRiseSpeed = 12f;
    [SerializeField, Min(0f)] private float panicDecaySpeed = 2f;

    [Header("Motor Override")]
    [SerializeField] private bool overrideMotorLimitsWhileFleeing = true;

    [SerializeField, Min(0f)]
    private float fleeMaxSpeedOverride = 12f;

    [SerializeField, Min(0f)]
    private float fleeAccelerationOverride = 60f;

    [Tooltip("Makes notice-radius threat more visible before entering hard flee range.")]
    [SerializeField, Min(0f)] private float noticeThreatMultiplier = 2.5f;

    [Tooltip("Minimum blend/weight once any threat is detected.")]
    [SerializeField, Range(0f, 1f)] private float minimumThreatWeight = 0.25f;

    [Tooltip("Minimum intent weight inside flee radius.")]
    [SerializeField, Range(0f, 1f)] private float minimumFleeRadiusWeight = 0.8f;

    [Tooltip("Minimum intent weight inside emergency radius.")]
    [SerializeField, Range(0f, 1f)] private float minimumEmergencyWeight = 1f;

    [Header("Priority")]
    [SerializeField] private bool suppressLowerPriorityInsideFleeRadius = true;
    [SerializeField] private bool useSchoolThreatState = true;

    [Header("Debug")]
    [SerializeField] private bool logThreatDetections;

    public override IAgentMovementModuleRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : IAgentMovementModuleRuntime
    {
        private readonly FleeThreatMovementModuleDefinition def;

        private float panic01;
        private float nextScanTime;
        private bool hasScanned;
        private bool insideFleeRadius;
        private bool insideEmergencyRadius;
        private Vector2 fleeDirection = Vector2.right;
        private CreatureThreatSource currentThreat;

        private float currentFleeSpeed;
        private float currentIntentWeightFloor;

        private string lastDebugLabel = "FleeThreat not initialized";
        private float lastThreatDistance = -1f;
        private string lastThreatName = "";


        public Runtime(FleeThreatMovementModuleDefinition def)
        {
            this.def = def;
        }

        public void Initialize(AgentController agent, AgentMovementModuleSlot slot)
        {
            panic01 = 0f;

            nextScanTime = 0f;
            hasScanned = false;

            insideFleeRadius = false;
            insideEmergencyRadius = false;

            fleeDirection = Vector2.right;
            currentThreat = null;

            currentFleeSpeed = def.fleeSpeed;
            currentIntentWeightFloor = def.minimumThreatWeight;

            lastDebugLabel = "FleeThreat initialized, first scan pending";
            lastThreatDistance = -1f;
            lastThreatName = "";
        }

        public AgentMovementIntent Evaluate(
            AgentMovementBlackboard blackboard,
            AgentMovementModuleSlot slot,
            float dt)
        {
            if (blackboard == null)
                return AgentMovementIntent.None("FleeThreat missing blackboard");

            UpdateThreatState(blackboard, dt);

            if (panic01 <= 0.01f)
                return AgentMovementIntent.None(lastDebugLabel);

            float threatWeight = Mathf.Clamp01(
                Mathf.Max(currentIntentWeightFloor, panic01));

            Vector2 safeFleeDirection =
                fleeDirection.sqrMagnitude > 0.001f
                    ? fleeDirection.normalized
                    : Vector2.right;

            Vector2 desired = safeFleeDirection * Mathf.Max(0f, currentFleeSpeed);

            bool suppress =
                insideFleeRadius &&
                def.suppressLowerPriorityInsideFleeRadius;

            float maxSpeedOverride = def.overrideMotorLimitsWhileFleeing
                ? Mathf.Max(def.fleeMaxSpeedOverride, currentFleeSpeed)
                : 0f;

            float accelerationOverride = def.overrideMotorLimitsWhileFleeing
                ? def.fleeAccelerationOverride
                : 0f;

            return AgentMovementIntent.Velocity(
                desired,
                slot.Strength * threatWeight,
                slot.Priority,
                suppressLowerPriority: suppress,
                debugLabel: "FleeThreat",
                maxSpeedOverride: maxSpeedOverride,
                accelerationOverride: accelerationOverride);
        }

        private void UpdateThreatState(AgentMovementBlackboard blackboard, float dt)
        {
            bool dueForScan =
                !hasScanned ||
                Time.time >= nextScanTime;

            if (!dueForScan)
            {
                Decay(dt);

                if (panic01 <= 0.01f)
                {
                    float timeUntilScan = Mathf.Max(0f, nextScanTime - Time.time);

                    lastDebugLabel =
                        $"FleeThreat between scans. next={timeUntilScan:0.00}s, " +
                        $"activeThreats={CreatureThreatRegistry.ActiveSourceCount}, " +
                        $"notice={def.noticeRadius:0.00}, flee={def.fleeRadius:0.00}";
                }

                return;
            }

            hasScanned = true;
            nextScanTime = Time.time + Mathf.Max(0.02f, def.scanInterval);

            Vector2 pos = blackboard.Position;

            bool foundDirectThreat =
                CreatureThreatRegistry.TryGetStrongestThreat(
                    pos,
                    def.noticeRadius,
                    out CreatureThreatSource threat,
                    out Vector2 away,
                    out float threat01);

            bool foundSchoolThreat = TryGetSchoolThreat(
                blackboard,
                out Vector2 schoolAway,
                out float schoolPanic);

            if (foundDirectThreat)
            {
                currentThreat = threat;
                fleeDirection = away;

                float dist = Vector2.Distance(pos, threat.Position);

                insideFleeRadius = dist <= def.fleeRadius;
                insideEmergencyRadius = dist <= def.emergencyRadius;

                lastThreatDistance = dist;
                lastThreatName = threat != null ? threat.name : "<null>";

                // 0 at outer notice edge, 1 at threat center.
                float noticeCloseness01 =
                    def.noticeRadius > 0f
                        ? 1f - Mathf.Clamp01(dist / def.noticeRadius)
                        : 0f;

                // 0 at flee radius edge, 1 at threat center.
                float fleeCloseness01 =
                    def.fleeRadius > 0f
                        ? 1f - Mathf.Clamp01(dist / def.fleeRadius)
                        : 0f;

                // Close fish should not gently consider leaving.
                // They should discover religion and evacuate.
                float targetPanic = Mathf.Clamp01(
                    Mathf.Max(threat01, noticeCloseness01) *
                    def.noticeThreatMultiplier);

                if (insideFleeRadius)
                    targetPanic = Mathf.Max(targetPanic, 0.85f);

                if (insideEmergencyRadius)
                    targetPanic = 1f;

                float riseSpeed = insideEmergencyRadius
                    ? def.panicRiseSpeed * 4f
                    : insideFleeRadius
                        ? def.panicRiseSpeed * 2f
                        : def.panicRiseSpeed;

                panic01 = Mathf.MoveTowards(
                    panic01,
                    targetPanic,
                    riseSpeed * dt);

                // Speed scales up as fish get closer.
                currentFleeSpeed =
                    def.fleeSpeed +
                    def.closeRangeSpeedBoost * Mathf.Clamp01(fleeCloseness01);

                // Weight floor also scales up as fish get closer.
                currentIntentWeightFloor = def.minimumThreatWeight;

                if (insideFleeRadius)
                    currentIntentWeightFloor = Mathf.Max(
                        currentIntentWeightFloor,
                        def.minimumFleeRadiusWeight);

                if (insideEmergencyRadius)
                    currentIntentWeightFloor = Mathf.Max(
                        currentIntentWeightFloor,
                        def.minimumEmergencyWeight);

                lastDebugLabel =
                    $"FleeThreat sees '{lastThreatName}' dist={lastThreatDistance:0.00}, " +
                    $"panic={panic01:0.00}, speed={currentFleeSpeed:0.00}, " +
                    $"weightFloor={currentIntentWeightFloor:0.00}, " +
                    $"insideFlee={insideFleeRadius}, emergency={insideEmergencyRadius}, " +
                    $"threat01={threat01:0.00}";

                if (def.logThreatDetections)
                {
                    Debug.Log(
                        $"[FleeThreatMovement] '{blackboard.agent.name}' threat='{lastThreatName}' " +
                        $"dist={dist:0.00}, panic={panic01:0.00}, insideFlee={insideFleeRadius}, away={away}",
                        blackboard.agent);
                }

                return;
            }

            if (foundSchoolThreat)
            {
                currentThreat = null;
                fleeDirection = schoolAway;
                insideFleeRadius = false;

                panic01 = Mathf.MoveTowards(
                    panic01,
                    Mathf.Clamp01(schoolPanic),
                    def.panicRiseSpeed * dt);

                lastDebugLabel =
                    $"FleeThreat using school panic={schoolPanic:0.00}, " +
                    $"activeThreats={CreatureThreatRegistry.ActiveSourceCount}";

                return;
            }

            currentThreat = null;
            insideFleeRadius = false;
            insideEmergencyRadius = false;
            currentFleeSpeed = def.fleeSpeed;
            currentIntentWeightFloor = def.minimumThreatWeight;

            Decay(dt);

            lastThreatDistance = -1f;
            lastThreatName = "";

            lastDebugLabel =
                $"FleeThreat no direct threat. activeThreats={CreatureThreatRegistry.ActiveSourceCount}, " +
                $"notice={def.noticeRadius:0.00}, flee={def.fleeRadius:0.00}";
        }

        private bool TryGetSchoolThreat(
            AgentMovementBlackboard blackboard,
            out Vector2 away,
            out float schoolPanic)
        {
            away = Vector2.zero;
            schoolPanic = 0f;

            if (!def.useSchoolThreatState)
                return false;

            FishSchoolMember2D member = blackboard.fishSchoolMember;
            if (member == null || member.School == null)
                return false;

            FishSchoolController school = member.School;

            if (school.Panic01 <= 0.01f)
                return false;

            away = school.FleeDirection;
            schoolPanic = school.Panic01;

            return away.sqrMagnitude > 0.001f;
        }

        private void Decay(float dt)
        {
            panic01 = Mathf.MoveTowards(
                panic01,
                0f,
                def.panicDecaySpeed * dt);
        }
    }
}