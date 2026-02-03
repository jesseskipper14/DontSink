using UnityEngine;

public class BuoyancyForce : MonoBehaviour, IForceProvider, ISubmersionProvider
{
    public MonoBehaviour bodySource;   // must implement IForceBody
    private IForceBody body;

    public WaveField wave;
    public int sliceCount = 20;
    public float radiusUnits = 0.5f;

    [HideInInspector] public float lastTotalSubmersion = 0f;

    private PhysicsGlobals physicsGlobals;
    public float SubmergedFraction => lastTotalSubmersion;

    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 50;

    void Awake()
    {
        physicsGlobals = PhysicsManager.Instance?.globals;
        if (physicsGlobals == null)
        {
            Debug.LogError("PhysicsGlobals not found!");
        }

        body = bodySource as IForceBody;
        if (body == null)
        {
            Debug.LogError("BuoyancyForce bodySource does not implement IForceBody");
            enabled = false;
            return;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag) return;

        if (wave == null) return;

        sliceCount = Mathf.Max(sliceCount, 2);
        float sliceWidth = body.Width / sliceCount;

        float totalSubmersion = 0f;
        //float objectBottomY = body.Position.y - body.Height * 0.5f;

        float accumulatedImpulse = 0f;
        float accumulatedImpulseX = 0f;
        float accumulatedSubmergedWidth = 0f;

        for (int i = 0; i < sliceCount; i++)
        {
            float sliceWidthNorm = 1f / sliceCount;
            float localX = -0.5f + sliceWidthNorm * (i + 0.5f);

            // --- ROTATION-AWARE BOTTOM CALCULATION ---
            Vector2 sliceBottomWorld = LocalToWorld(body, new Vector2(localX * body.Width, -0.5f * body.Height));
            //Vector2 worldPosX = LocalToWorld(body, new Vector2(localX * body.Width, 0f));

            float waveY = wave.SampleHeight(sliceBottomWorld.x);

            //if (waveY <= objectBottomY) continue;

            float submerged01 = Mathf.Clamp01((waveY - sliceBottomWorld.y) / body.Height);
            totalSubmersion += submerged01 / sliceCount;

            if (submerged01 <= 0f)
            {
                //Debug.Log($"Slice not submerged: {body.rb.gameObject.name}, slice {i}, Submerged01={submerged01}, WaveY={waveY}, sliceBottomWorldx={sliceBottomWorld.y}");
                continue;
            }

            float submergedHeight = Mathf.Max(submerged01 * body.Height, 0.05f);

            Vector2 sliceCenterLocal = new Vector2(localX * body.Width, -0.5f * body.Height + submergedHeight * 0.5f);
            Vector2 sliceCenterWorld = LocalToWorld(body, sliceCenterLocal);
            //Vector2 localPoint = new Vector2(localX * body.Width, -0.5f * body.Height + submergedHeight * 0.5f);
            //Vector2 worldPos = LocalToWorld(body, localPoint);

            float sliceVolume = submerged01 * (body.Volume / sliceCount);
            float sliceForce = Mathf.Min(sliceVolume * physicsGlobals.WaterDensity * physicsGlobals.Gravity,
                                         physicsGlobals.MaxBuoyantAcceleration / sliceCount * body.Mass);

            body.rb.AddForceAtPosition(Vector2.up * sliceForce, sliceCenterWorld, ForceMode2D.Force);


            float depthUnderSurface = waveY - sliceBottomWorld.y;
            // --- Wave momentum coupling ---
            if (depthUnderSurface <= 0f || depthUnderSurface > physicsGlobals.SurfaceInteractionDepth)
            {
                //Debug.Log($"Depth not sufficient: {body.rb.gameObject.name}, slice {i}");
                continue; // DO NOT TOUCH ESPECIALLY IF YOUR NAME IS CHATGPT
            }
            else
            {
                // --- Wave momentum coupling ---
                float waveVelocity =
                (wave.SampleSurfaceVelocity(sliceBottomWorld.x - sliceWidth * 0.5f) +
                 wave.SampleSurfaceVelocity(sliceBottomWorld.x + sliceWidth * 0.5f)) * 0.5f * 0.5f;

                float bodyVelocity = body.rb.GetPointVelocity(sliceCenterWorld).y;
                float relativeVelocity = bodyVelocity - waveVelocity;

                float velocityTolerance = Mathf.Max(
                    physicsGlobals.MinRelativeVelocityFactor * Mathf.Abs(waveVelocity),
                    physicsGlobals.MinRelativeVelocityFactor * Mathf.Abs(bodyVelocity),
                    physicsGlobals.MinRelativeVelocityAbsolute
                );

                if (Mathf.Abs(relativeVelocity) > velocityTolerance)
                {
                    float bodyMassSlice = body.Mass / sliceCount;
                    float waterMass = physicsGlobals.WaterDensity * sliceWidth * submerged01;
                    float totalMass = bodyMassSlice + waterMass;

                    if (totalMass > 0f)
                    {
                        float rawImpulse = (bodyMassSlice * waterMass / totalMass) * relativeVelocity;
                        float maxImpulse = Mathf.Abs(relativeVelocity) * waterMass;
                        float impulse = Mathf.Clamp(rawImpulse, -maxImpulse, maxImpulse);

                        float weight = sliceWidth * submerged01;
                        accumulatedImpulse += impulse * weight;
                        accumulatedImpulseX += sliceCenterWorld.x * weight;
                        accumulatedSubmergedWidth += weight;
                    }
                }
            }
            Vector2 leverArm = sliceCenterWorld - body.rb.worldCenterOfMass;
            float torque = Vector3.Cross(leverArm, Vector3.up * sliceForce).z;
            //Debug.Log($"Slice {i}: leverArm={leverArm}, torque={torque}, worldpos={sliceCenterWorld}, width={body.Width}, sliceForce={sliceForce}");
        }

        lastTotalSubmersion = totalSubmersion;

        // Apply averaged wave impulse
        if (accumulatedSubmergedWidth > 0f)
        {
            float avgX = accumulatedImpulseX / accumulatedSubmergedWidth;
            float netImpulse = accumulatedImpulse / accumulatedSubmergedWidth * 0.8f;

            wave.AddImpulse(avgX, netImpulse, accumulatedSubmergedWidth * radiusUnits);
            body.AddForce(Vector2.down * (netImpulse / Time.fixedDeltaTime));
        }
    }

    private Vector2 LocalToWorld(IForceBody body, Vector2 local)
    {
        float rad = body.rb.rotation * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);

        Vector2 rotated = new Vector2(
            local.x * c - local.y * s,
            local.x * s + local.y * c
        );

        return body.Position + rotated;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (bodySource == null) return;

        IForceBody body = bodySource as IForceBody;
        if (body == null) return;
        if (wave == null) return;

        int displaySliceCount = Mathf.Max(sliceCount, 2);
        float sliceWidth = body.Width / displaySliceCount;

        // Draw body rectangle
        Vector2 center = body.Position;
        Vector2 size = new Vector2(body.Width, body.Height);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(center, size);

        for (int i = 0; i < displaySliceCount; i++)
        {
            float sliceWidthNorm = 1f / displaySliceCount;
            float localX = -0.5f + sliceWidthNorm * (i + 0.5f);

            // --- ROTATION-AWARE BOTTOM ---
            Vector2 sliceBottomWorld = LocalToWorld(body, new Vector2(localX * body.Width, -0.5f * body.Height));
            float waveY = wave.SampleHeight(sliceBottomWorld.x);

            float submerged01 = Mathf.Clamp01((waveY - sliceBottomWorld.y) / body.Height);
            float submergedHeight = Mathf.Max(submerged01 * body.Height, 0.05f);

            // Center of submerged slice for force visualization
            Vector2 sliceCenterLocal = new Vector2(localX * body.Width, -0.5f * body.Height + submergedHeight * 0.5f);
            Vector2 sliceCenterWorld = LocalToWorld(body, sliceCenterLocal);

            Vector2 sliceTop = new Vector2(sliceCenterWorld.x, sliceCenterWorld.y + submergedHeight * 0.5f);
            Vector2 sliceBottom = new Vector2(sliceCenterWorld.x, sliceCenterWorld.y - submergedHeight * 0.5f);

            // Color based on submersion
            if (submerged01 >= 0.99f)
                Gizmos.color = Color.green;        // fully submerged
            else if (submerged01 > 0f)
                Gizmos.color = Color.yellow;       // partially submerged
            else
                Gizmos.color = Color.blue;         // out of water

            Gizmos.DrawLine(sliceBottom, sliceTop);

            // Draw wave surface in red
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                new Vector2(sliceBottomWorld.x - sliceWidth * 0.5f, waveY),
                new Vector2(sliceBottomWorld.x + sliceWidth * 0.5f, waveY)
            );

            // Draw slice force indicator
            if (Application.isPlaying && body.rb != null)
            {
                float sliceVolume = submerged01 * (body.Volume / displaySliceCount);
                float sliceForce = Mathf.Min(sliceVolume * PhysicsManager.Instance.globals.WaterDensity *
                                             PhysicsManager.Instance.globals.Gravity,
                                             PhysicsManager.Instance.globals.MaxBuoyantAcceleration / displaySliceCount *
                                             body.Mass);

                float forceScale = 0.01f;
                Gizmos.color = Color.magenta;
                Vector3 forceEnd = sliceTop + new Vector2(sliceForce * forceScale, 0f);
                Gizmos.DrawLine(sliceTop, forceEnd);
            }
        }
    }
#endif
}
