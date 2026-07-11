using UnityEngine;

[CreateAssetMenu(
    menuName = "Agents/Movement Modules/Stay Below Water Surface",
    fileName = "StayBelowWaterSurfaceMovementModule")]
public sealed class StayBelowWaterSurfaceMovementModuleDefinition : AgentMovementModuleDefinition
{
    [Header("Surface Sampling")]
    [SerializeField] private WaveField explicitWaveField;

    [Tooltip("If true, uses WaveField.SampleHeightAtWorldXWrapped. Otherwise uses SampleHeight.")]
    [SerializeField] private bool useWrappedSampling = true;

    [Header("Clearance")]
    [Tooltip("Fish target position below the sampled water surface.")]
    [SerializeField, Min(0f)] private float desiredDepthBelowSurface = 0.45f;

    [Tooltip("Starts pushing fish down before they actually breach the surface.")]
    [SerializeField, Min(0f)] private float surfaceBuffer = 0.25f;

    [Header("Response")]
    [SerializeField, Min(0f)] private float downwardSpeed = 4f;

    [Tooltip("Extra downward speed when fish is above the allowed surface band.")]
    [SerializeField, Min(0f)] private float emergencyDownwardSpeedBoost = 6f;

    [Tooltip("How strongly this module contributes while near the surface.")]
    [SerializeField, Range(0f, 1f)] private float nearSurfaceWeight = 0.5f;

    [Tooltip("How strongly this module contributes once above the allowed waterline.")]
    [SerializeField, Range(0f, 1f)] private float aboveSurfaceWeight = 1f;

    [Tooltip("When above the allowed waterline, suppress lower priority movement.")]
    [SerializeField] private bool suppressLowerPriorityWhenAboveSurface = true;

    [Header("Motor Override")]
    [SerializeField] private bool overrideMotorLimits = true;

    [SerializeField, Min(0f)] private float maxSpeedOverride = 8f;
    [SerializeField, Min(0f)] private float accelerationOverride = 80f;

    [Header("Debug")]
    [SerializeField] private bool logMissingWaveField;

    public override IAgentMovementModuleRuntime CreateRuntime()
    {
        return new Runtime(this);
    }

    private sealed class Runtime : IAgentMovementModuleRuntime
    {
        private readonly StayBelowWaterSurfaceMovementModuleDefinition def;

        private WaveField waveField;
        private string lastDebugLabel = "StayBelowWaterSurface not initialized";
        private bool warnedMissingWave;

        public Runtime(StayBelowWaterSurfaceMovementModuleDefinition def)
        {
            this.def = def;
        }

        public void Initialize(AgentController agent, AgentMovementModuleSlot slot)
        {
            waveField = def.explicitWaveField;

            if (waveField == null && ServiceRoot.Instance != null && ServiceRoot.Instance.WaveManager != null)
            {
                // Prefer WaveManager if your scene uses it as the stable service wrapper.
                // But WaveManager API varies in this project, so we still fall back to WaveField below.
                waveField = Object.FindFirstObjectByType<WaveField>();
            }

            if (waveField == null)
                waveField = Object.FindFirstObjectByType<WaveField>();

            lastDebugLabel = waveField != null
                ? $"StayBelowWaterSurface using '{waveField.name}'"
                : "StayBelowWaterSurface missing WaveField";
        }

        public AgentMovementIntent Evaluate(
            AgentMovementBlackboard blackboard,
            AgentMovementModuleSlot slot,
            float dt)
        {
            if (blackboard == null)
                return AgentMovementIntent.None("StayBelowWaterSurface missing blackboard");

            if (waveField == null)
            {
                waveField = Object.FindFirstObjectByType<WaveField>();

                if (waveField == null)
                {
                    if (def.logMissingWaveField && !warnedMissingWave)
                    {
                        warnedMissingWave = true;
                        Debug.LogWarning("[StayBelowWaterSurface] No WaveField found. Fish waterline constraint inactive.");
                    }

                    return AgentMovementIntent.None("StayBelowWaterSurface no WaveField");
                }
            }

            Vector2 pos = blackboard.Position;

            float surfaceY = def.useWrappedSampling
                ? waveField.SampleHeightAtWorldXWrapped(pos.x)
                : waveField.SampleHeight(pos.x);

            float targetMaxY = surfaceY - def.desiredDepthBelowSurface;
            float softLimitY = targetMaxY - def.surfaceBuffer;

            // Safely below the soft band: do nothing.
            if (pos.y <= softLimitY)
            {
                lastDebugLabel =
                    $"StayBelowWaterSurface safe. posY={pos.y:0.00}, surfaceY={surfaceY:0.00}, limit={targetMaxY:0.00}";

                return AgentMovementIntent.None(lastDebugLabel);
            }

            float aboveSoftBand = Mathf.Max(0f, pos.y - softLimitY);
            float aboveHardLimit = Mathf.Max(0f, pos.y - targetMaxY);

            bool aboveSurfaceLimit = pos.y > targetMaxY;

            float severity01 = def.surfaceBuffer > 0f
                ? Mathf.Clamp01(aboveSoftBand / def.surfaceBuffer)
                : 1f;

            if (aboveSurfaceLimit)
                severity01 = 1f;

            float speed =
                def.downwardSpeed +
                def.emergencyDownwardSpeedBoost * Mathf.Clamp01(aboveHardLimit);

            Vector2 desired = Vector2.down * speed;

            float weight = aboveSurfaceLimit
                ? def.aboveSurfaceWeight
                : Mathf.Lerp(0.05f, def.nearSurfaceWeight, severity01);

            bool suppress =
                aboveSurfaceLimit &&
                def.suppressLowerPriorityWhenAboveSurface;

            float maxSpeedOverride = def.overrideMotorLimits
                ? Mathf.Max(def.maxSpeedOverride, speed)
                : 0f;

            float accelerationOverride = def.overrideMotorLimits
                ? def.accelerationOverride
                : 0f;

            lastDebugLabel =
                $"StayBelowWaterSurface correcting. posY={pos.y:0.00}, surfaceY={surfaceY:0.00}, " +
                $"limit={targetMaxY:0.00}, aboveLimit={aboveHardLimit:0.00}, suppress={suppress}";

            return AgentMovementIntent.Velocity(
                desired,
                slot.Strength * weight,
                slot.Priority,
                suppressLowerPriority: suppress,
                debugLabel: lastDebugLabel,
                maxSpeedOverride: maxSpeedOverride,
                accelerationOverride: accelerationOverride);
        }
    }
}