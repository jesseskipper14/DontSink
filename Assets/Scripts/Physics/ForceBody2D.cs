using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ForceBody2D : MonoBehaviour, IForceBody
{
    public Rigidbody2D rb { get; private set; }

    private Vector2 accumulatedForce;
    private float accumulatedTorque;

    [Header("Geometry")]
    [SerializeField, Min(0.01f)] private float width = 1f;
    [SerializeField, Min(0.01f)] private float height = 1f;

    // Runtime physical-volume additions such as flotation gear, inflatable
    // bladders, chemical flotation, etc. The authored Width x Height remains
    // the base displacement volume; contributors add only meaningful extra volume.
    private readonly List<IVolumeContribution> volumeContributions =
        new List<IVolumeContribution>();

    public Vector2 Position => rb != null ? rb.position : (Vector2)transform.position;
    public float Mass => rb != null ? rb.mass : 0f;
    public float MomentOfInertia => rb != null ? rb.inertia : 0f;

    public float Width => width;
    public float Height => height;

    // Geometry and displacement are normally the same thing for simple bodies.
    // Dynamic inflatable bodies may temporarily use their dimensions only as
    // water-contact geometry while supplying authoritative displacement through
    // IVolumeContribution. Default 1 preserves every existing body's behavior.
    private float baseDisplacementScale = 1f;

    /// <summary>
    /// Geometric area represented by the current Width x Height rectangle.
    /// This remains the shape used by the simple buoyancy solver.
    /// </summary>
    public float GeometryVolume =>
        Mathf.Max(0f, width) *
        Mathf.Max(0f, height);

    /// <summary>
    /// Base displacement contributed by the current geometry. Ordinary bodies
    /// use GeometryVolume unchanged. Dynamic inflatables may suppress it while
    /// providing their total displacement through IVolumeContribution, avoiding
    /// double-counting when their physical geometry grows.
    /// </summary>
    public float DimensionVolume =>
        GeometryVolume * Mathf.Max(0f, baseDisplacementScale);

    public float BaseDisplacementScale => baseDisplacementScale;

    /// <summary>
    /// Sum of all currently live, finite, positive volume contributions.
    /// Values are read live rather than cached so dynamic flotation can change
    /// without requiring a second notification/state system.
    /// </summary>
    public float VolumeContributionTotal
    {
        get
        {
            GetVolumeContributionTotals(
                out float cappedVolume,
                out float uncappedVolume);

            return cappedVolume + uncappedVolume;
        }
    }

    /// <summary>
    /// Splits runtime displacement into the normal capped bucket and an
    /// explicitly opted-in uncapped bucket. Ordinary contributors remain
    /// capped by default.
    /// </summary>
    public void GetVolumeContributionTotals(
        out float cappedVolume,
        out float uncappedVolume)
    {
        cappedVolume = 0f;
        uncappedVolume = 0f;

        for (int i = 0; i < volumeContributions.Count; i++)
        {
            IVolumeContribution contribution =
                volumeContributions[i];

            if (!IsLiveVolumeContribution(contribution))
                continue;

            float value =
                contribution.VolumeContribution;

            if (value <= 0f ||
                float.IsNaN(value) ||
                float.IsInfinity(value))
            {
                continue;
            }

            float effectiveness01 = 1f;
            float bypass01 = 0f;

            if (contribution is IBuoyancyVolumePolicy policy)
            {
                effectiveness01 = policy.VolumeEffectiveness01;
                bypass01 = policy.BodyAccelerationCapBypass01;

                if (float.IsNaN(effectiveness01) ||
                    float.IsInfinity(effectiveness01))
                {
                    effectiveness01 = 0f;
                }

                if (float.IsNaN(bypass01) ||
                    float.IsInfinity(bypass01))
                {
                    bypass01 = 0f;
                }

                effectiveness01 = Mathf.Clamp01(effectiveness01);
                bypass01 = Mathf.Clamp01(bypass01);
            }

            // Apply contribution effectiveness first, then split the remaining
            // effective volume between capped and uncapped paths. Ordinary
            // contributors remain 100% effective and capped by default.
            float effectiveValue = value * effectiveness01;

            uncappedVolume += effectiveValue * bypass01;
            cappedVolume += effectiveValue * (1f - bypass01);
        }
    }

    /// <summary>
    /// Per-body scale applied to the historical buoyant-acceleration ceiling.
    /// Ordinary bodies remain at 1. Policies may only make the capped path more
    /// restrictive; multiple policies resolve to the most restrictive live scale.
    /// </summary>
    public float BodyAccelerationCapScale01
    {
        get
        {
            float scale01 = 1f;

            for (int i = 0; i < volumeContributions.Count; i++)
            {
                IVolumeContribution contribution = volumeContributions[i];

                if (!IsLiveVolumeContribution(contribution))
                    continue;

                IBuoyancyVolumePolicy policy =
                    contribution as IBuoyancyVolumePolicy;

                if (policy == null)
                    continue;

                float candidate = policy.BodyAccelerationCapScale01;

                if (float.IsNaN(candidate) || float.IsInfinity(candidate))
                    candidate = 0f;

                scale01 = Mathf.Min(scale01, Mathf.Clamp01(candidate));
            }

            return scale01;
        }
    }

    /// <summary>
    /// Authoritative displacement volume for simple ForceBody2D physics.
    /// </summary>
    public float Volume =>
        DimensionVolume +
        VolumeContributionTotal;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.01f, width);
        height = Mathf.Max(0.01f, height);
    }

    /// <summary>
    /// Authoritative runtime/editor hook for changing the body's base dimensions.
    /// Volume follows automatically from Width x Height.
    /// </summary>
    public void SetDimensions(
        float newWidth,
        float newHeight)
    {
        width = Mathf.Max(0.01f, newWidth);
        height = Mathf.Max(0.01f, newHeight);
    }

    /// <summary>
    /// Runtime-only scale for how much the current Width x Height geometry
    /// contributes as base displacement. Default 1. A value of 0 keeps the
    /// geometry for submersion/drag while displacement is supplied elsewhere.
    /// </summary>
    public void SetBaseDisplacementScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale))
            scale = 0f;

        baseDisplacementScale = Mathf.Max(0f, scale);
    }

    public void RegisterVolumeContribution(
        IVolumeContribution contribution)
    {
        if (!IsLiveVolumeContribution(contribution))
            return;

        if (!volumeContributions.Contains(contribution))
            volumeContributions.Add(contribution);
    }

    public void UnregisterVolumeContribution(
        IVolumeContribution contribution)
    {
        if (contribution == null)
            return;

        // Remove every copy defensively so historical/bad registration state
        // cannot leave a ghost contribution behind.
        while (volumeContributions.Remove(contribution))
        {
        }
    }

    private static bool IsLiveVolumeContribution(
        IVolumeContribution contribution)
    {
        if (contribution == null)
            return false;

        if (contribution is UnityEngine.Object unityObject &&
            unityObject == null)
        {
            return false;
        }

        if (contribution is Behaviour behaviour &&
            !behaviour.isActiveAndEnabled)
        {
            return false;
        }

        return true;
    }

    public void AddForce(Vector2 force)
    {
        accumulatedForce += force;
    }

    public void AddTorque(float torque)
    {
        accumulatedTorque += torque;
    }

    void FixedUpdate()
    {
        rb.AddForce(accumulatedForce, ForceMode2D.Force);
        rb.AddTorque(accumulatedTorque, ForceMode2D.Force);

        accumulatedForce = Vector2.zero;
        accumulatedTorque = 0f;
    }
}
