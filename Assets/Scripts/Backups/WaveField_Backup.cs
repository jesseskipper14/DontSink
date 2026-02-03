//using UnityEngine;

///// <summary>
///// Authoritative wave data source.
///// ALL physics & visuals must sample from here.
///// </summary>
//public class WaveField : MonoBehaviour
//{
//    [Header("Wave Shape")]
//    public float amplitude = 0.1f;
//    public float frequency = 0.3f;

//    [Header("Wave Motion")]
//    public float speed = 0.5f;

//    [Header("Dynamic Wave Surface")]
//    public int resolution = 200;
//    public float width = 100.0f;

//    [Header("Wave Physics")]
//    public float stiffness = 5.0f;   // how strongly water returns to rest
//    public float damping = 0.001f;      // how quickly energy is lost
//    public float tension = 100.0f;   // propagation strength
//    public float maxWaveVelocity = 10.0f;

//    [Header("Wave Smoothing")]
//    public float viscosity = 0.02f;

//    public float LeftX { get; private set; }
//    public float Dx { get; private set; }

//    float[] heights;
//    float[] velocities;
//    float[] accelerations;
//    float[] velocityLaplacian;

//    public float Phase { get; private set; }

//    void Awake()
//    {
//        Dx = width / (resolution - 1);
//        LeftX = transform.position.x - width * 0.5f;

//        heights = new float[resolution];
//        velocities = new float[resolution];
//        accelerations = new float[resolution];
//        velocityLaplacian = new float[resolution];
//    }

//    void FixedUpdate()
//    {
//        Phase += Time.fixedDeltaTime * speed;
//        float dt = Time.fixedDeltaTime;

//        // --- Force pass ---
//        for (int i = 0; i < resolution; i++)
//        {
//            float force = 0f;

//            // Weak vertical restoring force
//            force += -stiffness * heights[i];

//            // Proper discrete Laplacian
//            float laplacian = 0f;
//            if (i > 0) laplacian += heights[i - 1] - heights[i];
//            if (i < resolution - 1) laplacian += heights[i + 1] - heights[i];

//            force += tension * laplacian / (Dx * Dx);

//            accelerations[i] = force;
//        }

//        // --- Integration pass ---
//        for (int i = 0; i < resolution; i++)
//        {
//            velocities[i] += accelerations[i] * dt;
//            velocities[i] *= Mathf.Exp(-damping * dt);
//            heights[i] += velocities[i] * dt;
//        }

//        // --- apply max speed water can move ---
//        for (int i = 0; i < resolution; i++)
//        {
//            velocities[i] = Mathf.Clamp(velocities[i], -maxWaveVelocity, maxWaveVelocity);
//        }

//        // --- Velocity viscosity (kills saw patterns) ---
//        for (int i = 1; i < resolution - 1; i++)
//        {
//            velocityLaplacian[i] =
//                velocities[i - 1]
//                - 2f * velocities[i]
//                + velocities[i + 1];
//        }
        
//        for (int i = 1; i < resolution - 1; i++)
//        {
//            velocities[i] += viscosity * velocityLaplacian[i];
//        }

//        for (int i = 1; i < resolution - 1; i++)
//        {
//            heights[i] = Mathf.Lerp(
//                heights[i],
//                (heights[i - 1] + heights[i] + heights[i + 1]) / 3f,
//                0.05f
//            );
//        }
//    }

//    /// <summary>
//    /// Sample wave height at world X (Y offset is transform.position.y).
//    /// </summary>
//    public float SampleHeight(float worldX)
//    {
//        int i = WorldXToIndex(worldX);

//        float baseWave =
//            amplitude *
//            Mathf.Sin(Mathf.PI * frequency * worldX + Phase);

//        return transform.position.y + baseWave + heights[i];
//    }

//    public float SampleHeightAtIndex(int i)
//    {
//        float baseWave =
//            amplitude *
//            Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i) + Phase);

//        return transform.position.y + baseWave + heights[i];
//    }

//    /// <summary>
//    /// Optional: slope for normals / boat tilt later
//    /// </summary>
//    public float SampleSlope(float worldX)
//    {
//        return amplitude *
//               Mathf.PI *
//               frequency *
//               Mathf.Cos(Mathf.PI * frequency * worldX + Phase);
//    }

//    public void AddImpulse(float worldX, float strength, float radius = 1.0f)
//    {
//        int center = WorldXToIndex(worldX);
//        int r = Mathf.CeilToInt(radius);

//        // Clamp strength to prevent mega impulses
//        float maxStrength = 5f;  // tune this
//        strength = Mathf.Min(strength, maxStrength);

//        for (int i = center - r; i <= center + r; i++)
//        {
//            if (i < 0 || i >= resolution) continue;

//            float dist = Mathf.Abs(i - center) * Dx;
//            float falloff = Mathf.Exp(-(dist * dist) / (radius * radius));

//            // Apply smaller fraction to height, bigger to velocity
//            heights[i] += strength * falloff * 0.1f;
//            velocities[i] += strength * falloff * 0.3f;

//            // Clamp velocity immediately to keep wave stable
//            velocities[i] = Mathf.Clamp(velocities[i], -maxWaveVelocity, maxWaveVelocity);
//        }
//    }

//    public void AddImpulseSlice(float worldX, float totalForce, float radius = 2f)
//    {
//        int center = WorldXToIndex(worldX);
//        int spreadNodes = Mathf.CeilToInt(radius / Dx);
//        spreadNodes = Mathf.Max(spreadNodes, 1);

//        for (int i = center - spreadNodes; i <= center + spreadNodes; i++)
//        {
//            if (i < 0 || i >= resolution) continue;

//            // Distance falloff (Gaussian)
//            float dist = Mathf.Abs(i - center) * Dx;
//            float falloff = Mathf.Exp(-dist * dist / (radius * radius));

//            // Compute node impulse
//            float nodeImpulse = totalForce * falloff / (spreadNodes * 2 + 1);

//            // Clamp velocity
//            float maxVelocity = PhysicsGlobal.MaxWaveVelocity;
//            float newVelocity = velocities[i] + nodeImpulse;
//            if (Mathf.Abs(newVelocity) > maxVelocity)
//            {
//                nodeImpulse = Mathf.Sign(nodeImpulse) * (maxVelocity - Mathf.Abs(velocities[i]));
//            }

//            // Apply safely
//            velocities[i] += nodeImpulse;
//            heights[i] += nodeImpulse * 0.5f; // optional partial height contribution
//        }
//    }



//    public int WorldXToIndex(float worldX)
//    {
//        float t = (worldX - LeftX) / Dx;
//        return Mathf.Clamp(Mathf.RoundToInt(t), 0, resolution - 1);
//    }

//    public float IndexToWorldX(int i)
//    {
//        return LeftX + i * Dx;
//    }

//    /// <summary>
//    /// Sample wave height at any world X, wrapping infinitely.
//    /// Interpolates between nodes for smoothness.
//    /// </summary>
//    public float SampleHeightAtWorldXWrapped(float worldX)
//    {
//        // Relative X in local wave array
//        float relativeX = worldX - LeftX;
//        float t = relativeX / Dx; // fractional index
//        int i0 = Mathf.FloorToInt(t);
//        int i1 = i0 + 1;
//        float frac = t - i0;

//        // Wrap indices
//        i0 = ((i0 % resolution) + resolution) % resolution;
//        i1 = ((i1 % resolution) + resolution) % resolution;

//        // Base wave + dynamic height
//        float y0 = transform.position.y + amplitude * Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i0) + Phase) + heights[i0];
//        float y1 = transform.position.y + amplitude * Mathf.Sin(Mathf.PI * frequency * IndexToWorldX(i1) + Phase) + heights[i1];

//        return Mathf.Lerp(y0, y1, frac);
//    }

//    public float SampleSurfaceVelocity(float worldX)
//    {
//        int i = WorldXToIndex(worldX);
//        return velocities[i];
//    }

//    //void OnDrawGizmos()
//    //{
//    //    if (heights == null) return;

//    //    Gizmos.color = Color.cyan;

//    //    if (transform == null) return;

//    //    for (int i = 0; i < resolution; i++)
//    //    {
//    //        float x = IndexToWorldX(i);
//    //        float y = transform.position.y + heights[i];
//    //        Gizmos.DrawSphere(new Vector3(x, y, 0f), 0.03f);
//    //    }
//    //}
//}
