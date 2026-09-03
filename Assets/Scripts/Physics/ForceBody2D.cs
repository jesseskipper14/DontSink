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

    /// <summary>
    /// Base displacement represented by this body's authored dimensions.
    /// In the game's 2D physics model, Width x Height is the volume proxy.
    /// </summary>
    public float DimensionVolume =>
        Mathf.Max(0f, width) *
        Mathf.Max(0f, height);

    /// <summary>
    /// Sum of all currently live, finite, positive volume contributions.
    /// Values are read live rather than cached so dynamic flotation can change
    /// without requiring a second notification/state system.
    /// </summary>
    public float VolumeContributionTotal
    {
        get
        {
            float total = 0f;

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

                total += value;
            }

            return total;
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
