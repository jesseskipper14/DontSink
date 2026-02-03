using UnityEngine;

/// <summary>
/// 1D shallow-water style wave simulation.
/// Tracks vertical height, vertical velocity, and horizontal velocity for momentum redistribution.
/// </summary>
public class WaveField : MonoBehaviour
{
    // Debug stuff
    public float debugThreshold = 0.5f; // deviation from neutral height to trigger debug
    private float debugTimer = 0f;
    private float debugInterval = 1f; // seconds
    public bool debugOn = false;

    [Header("Wave Shape")]
    public float amplitude = 0.1f;
    public float frequency = 0.3f;

    [Header("Wave Motion")]
    public float speed = 0.5f;

    [Header("Dynamic Wave Surface")]
    public int resolution = 200;
    public float width = 100.0f;

    [Header("Wave Physics")]
    public float stiffness = 5.0f;    // vertical restoring force
    public float damping = 0.01f;    // vertical damping
    public float horizontalDamping = 0.1f; // tune this between 0.05 and 0.2 as needed
    public float tension = 50.0f;     // horizontal wave propagation (pressure)
    public float viscosity = 0.02f;   // velocity smoothing
    public float maxWaveVelocity = 10.0f;

    public float LeftX { get; private set; }
    public float Dx { get; private set; }

    private float[] heights;      // vertical displacement
    private float[] velocities;   // vertical velocity
    private float[] accelerations;// vertical acceleration
    private float[] u;            // horizontal velocity
    private float[] uLaplacian;   // for horizontal viscosity
    private float[] newHeights;

    private float phase;

    void Awake()
    {
        Dx = width / (resolution - 1);
        LeftX = transform.position.x - width * 0.5f;

        heights = new float[resolution];
        velocities = new float[resolution];
        accelerations = new float[resolution];
        u = new float[resolution];
        uLaplacian = new float[resolution];
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        phase += dt * speed;

        // --- 1) Compute vertical accelerations (spring + tension) ---
        for (int i = 0; i < resolution; i++)
        {
            float force = -stiffness * heights[i];

            // horizontal spring (pressure) contribution
            float lap = 0f;
            if (i > 0) lap += heights[i - 1] - heights[i];
            if (i < resolution - 1) lap += heights[i + 1] - heights[i];

            force += tension * lap / (Dx * Dx);
            accelerations[i] = force;
        }

        // --- 2) Update vertical velocities ---
        for (int i = 0; i < resolution; i++)
        {
            velocities[i] += accelerations[i] * dt;
            velocities[i] *= Mathf.Exp(-damping * dt);
        }

        // --- 3) Update horizontal velocities from pressure gradients ---
        for (int i = 1; i < resolution - 1; i++)
        {
            // horizontal acceleration ~ -g * dh/dx
            float dhdx = (heights[i + 1] - heights[i - 1]) / (2f * Dx);
            u[i] += -stiffness * dhdx * dt; // pressure drives horizontal flow
        }

        // --- 4) Apply horizontal viscosity ---
        for (int i = 1; i < resolution - 1; i++)
        {
            uLaplacian[i] = u[i - 1] - 2f * u[i] + u[i + 1];
        }

        for (int i = 1; i < resolution - 1; i++)
        {
            u[i] += viscosity * uLaplacian[i];
        }

        // --- 4b) Apply horizontal damping ---
        for (int i = 0; i < resolution; i++)
        {
            u[i] *= Mathf.Exp(-horizontalDamping * dt);
        }

        // --- 5) Update heights using vertical velocities + horizontal advection (safe version) ---
        newHeights = new float[resolution];

        for (int i = 1; i < resolution - 1; i++)
        {
            // --- 5a) Smooth horizontal velocity to reduce oscillations ---
            float uSmooth = (u[i - 1] + u[i] + u[i + 1]) / 3f;

            // --- 5b) Compute derivative safely ---
            float dudx = (u[i + 1] - u[i - 1]) / (2f * Dx);

            // --- 5c) Compute horizontal contribution ---
            float horizontalContribution = -heights[i] * dudx * dt;

            // Clamp horizontal contribution to maxHeightChange for stability
            float maxHeightChange = maxWaveVelocity * dt;
            horizontalContribution = Mathf.Clamp(horizontalContribution, -maxHeightChange, maxHeightChange);

            // --- 5d) Integrate vertical velocity + horizontal advection ---
            float verticalContribution = velocities[i] * dt;
            newHeights[i] = heights[i] + verticalContribution + horizontalContribution;

            // --- 5e) Clamp total height change per frame ---
            newHeights[i] = Mathf.Clamp(newHeights[i], heights[i] - maxHeightChange, heights[i] + maxHeightChange);
        }

        // Copy updated heights back
        for (int i = 1; i < resolution - 1; i++)
        {
            heights[i] = newHeights[i];
        }

        // Optional: smooth heights for stability
        for (int i = 1; i < resolution - 1; i++)
        {
            heights[i] = Mathf.Lerp(
                heights[i],
                (heights[i - 1] + heights[i] + heights[i + 1]) / 3f,
                0.05f
            );
        }

        // Additional Smoothing
        for (int i = 0; i < resolution; i++)
        {
            heights[i] = Mathf.Clamp(heights[i], -1e3f, 1e3f); // some sane max
        }

        // Clamp velocities to prevent extreme values
        for (int i = 0; i < resolution; i++)
        {
            velocities[i] = Mathf.Clamp(velocities[i], -maxWaveVelocity, maxWaveVelocity);
            u[i] = Mathf.Clamp(u[i], -maxWaveVelocity, maxWaveVelocity);
        }


        if (debugOn)
        {
            debugTimer += Time.fixedDeltaTime;
            if (debugTimer >= debugInterval)
            {
                debugTimer = 0f;

                for (int i = 0; i < resolution; i++)
                {
                    if (Mathf.Abs(heights[i]) > debugThreshold)
                    {
                        Debug.Log($"Node {i}: height={heights[i]} vel={velocities[i]} u={u[i]} accel={accelerations[i]}");
                    }
                }
            }
        }
    }



    // --- Add object impulse ---
    public void AddImpulse(float worldX, float totalForce, float radius = 2.0f)
    {
        int center = WorldXToIndex(worldX);
        int r = Mathf.CeilToInt(radius / Dx);

        for (int i = center - r; i <= center + r; i++)
        {
            if (i < 0 || i >= resolution) continue;

            float dist = Mathf.Abs(i - center) * Dx;
            float falloff = Mathf.Exp(-dist * dist / (radius * radius));

            // Split impulse into vertical and horizontal components (approx)
            float verticalImpulse = totalForce * 0.6f * falloff;
            float horizontalImpulse = totalForce * 0.4f * falloff;

            float maxNodeVelocity = maxWaveVelocity;
            velocities[i] = Mathf.Clamp(velocities[i] + verticalImpulse, -maxNodeVelocity, maxNodeVelocity);
            u[i] = Mathf.Clamp(u[i] + horizontalImpulse, -maxNodeVelocity, maxNodeVelocity);
        }
    }

    // Optional: sample horizontal velocity for debugging or object interactions
    public float SampleHorizontalVelocity(float worldX)
    {
        int i = WorldXToIndex(worldX);
        return u[i];
    }

    public float SampleSurfaceVelocity(float worldX)
    {
        int i = WorldXToIndex(worldX);
        return velocities[i]; // vertical velocity at this point
    }

    // --- Wave sampling ---
    public float SampleHeightAtWorldXWrapped(float worldX)
    {
        // Fractional index
        float t = (worldX - LeftX) / Dx;
        int i0 = Mathf.FloorToInt(t);
        int i1 = i0 + 1;
        float frac = t - i0;

        // Wrap indices
        i0 = ((i0 % resolution) + resolution) % resolution;
        i1 = ((i1 % resolution) + resolution) % resolution;

        float baseWave0 = amplitude * Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i0) + phase);
        float baseWave1 = amplitude * Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i1) + phase);

        float y0 = transform.position.y + baseWave0 + heights[i0];
        float y1 = transform.position.y + baseWave1 + heights[i1];

        return Mathf.Lerp(y0, y1, frac);
    }
    public float SampleHeight(float worldX)
    {
        int i0 = WorldXToIndex(worldX);
        int i1 = Mathf.Min(i0 + 1, resolution - 1);
        float t = (worldX - IndexToWorldX(i0)) / Dx;

        float baseWave0 = amplitude * Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i0) + phase);
        float baseWave1 = amplitude * Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i1) + phase);

        float y0 = transform.position.y + baseWave0 + heights[i0];
        float y1 = transform.position.y + baseWave1 + heights[i1];

        return Mathf.Lerp(y0, y1, t);
    }

    public int WorldXToIndex(float worldX)
    {
        float t = (worldX - LeftX) / Dx;
        return Mathf.Clamp(Mathf.RoundToInt(t), 0, resolution - 1);
    }

    public float IndexToWorldX(int i)
    {
        return LeftX + i * Dx;
    }

    private void OnDrawGizmos()
    {
        if (debugOn)
        {
            if (heights == null) return;

            Gizmos.color = Color.red;

            for (int i = 0; i < resolution; i++)
            {
                if (Mathf.Abs(heights[i]) > debugThreshold)
                {
                    // Convert node index to world X
                    float worldX = i * Dx; // or use your WorldXFromIndex(i) function
                    Vector3 pos = new Vector3(worldX, heights[i], 0f);
                    Gizmos.DrawSphere(pos, 1.0f); // small sphere to mark node
                }
            }
        }
    }
}
