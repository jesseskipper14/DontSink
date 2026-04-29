using UnityEngine;

[DisallowMultipleComponent]
public sealed class PumpModule : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool isOn;

    [Header("Pump")]
    [Tooltip("Water area removed per second from the target compartment.")]
    [SerializeField] private float pumpRatePerSecond = 0.35f;

    [Tooltip("If true, the pump searches for its compartment automatically.")]
    [SerializeField] private bool autoResolveCompartment = true;

    [Tooltip("Optional explicit target. If assigned, auto-resolution is skipped.")]
    [SerializeField] private Compartment targetCompartment;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private InstalledModule installedModule;
    private Hardpoint ownerHardpoint;
    private Boat ownerBoat;

    public bool IsOn => isOn;
    public float PumpRatePerSecond => Mathf.Max(0f, pumpRatePerSecond);
    public Compartment TargetCompartment => targetCompartment;

    private void Awake()
    {
        installedModule = GetComponent<InstalledModule>();
    }

    private void Start()
    {
        ResolveOwnership();
        ResolveTargetCompartment();
    }

    private void Update()
    {
        if (!isOn)
            return;

        if (PumpRatePerSecond <= 0f)
            return;

        if (targetCompartment == null && autoResolveCompartment)
            ResolveTargetCompartment();

        if (targetCompartment == null)
        {
            if (debugLogs)
                Debug.LogWarning("[PumpModule] Pump is on but no target compartment was resolved.", this);

            return;
        }

        if (targetCompartment.WaterArea <= 0f)
            return;

        float amount = PumpRatePerSecond * Time.deltaTime;
        targetCompartment.RemoveWater(amount);
    }

    public bool SetOn(bool value)
    {
        isOn = value;
        return true;
    }

    public bool Toggle()
    {
        return SetOn(!isOn);
    }

    public bool CanRun()
    {
        return targetCompartment != null;
    }

    public void SetTargetCompartment(Compartment compartment)
    {
        targetCompartment = compartment;
    }

    public void ResolveTargetCompartment()
    {
        if (!autoResolveCompartment && targetCompartment != null)
            return;

        ResolveOwnership();

        if (ownerBoat == null)
            return;

        Vector2 pumpWorldPos = ownerHardpoint != null
            ? ownerHardpoint.transform.position
            : transform.position;

        targetCompartment = FindBestCompartment(ownerBoat, pumpWorldPos);

        if (debugLogs)
        {
            Debug.Log(
                $"[PumpModule] Resolved target compartment: {(targetCompartment != null ? targetCompartment.name : "NULL")}",
                this);
        }
    }

    private void ResolveOwnership()
    {
        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();

        if (installedModule != null)
            ownerHardpoint = installedModule.OwnerHardpoint;

        if (ownerHardpoint != null)
        {
            ownerBoat = ownerHardpoint.GetComponentInParent<Boat>();
            if (ownerBoat == null)
                ownerBoat = ownerHardpoint.GetComponentInChildren<Boat>();
        }

        if (ownerBoat == null)
            ownerBoat = GetComponentInParent<Boat>();
    }

    private static Compartment FindBestCompartment(Boat boat, Vector2 worldPoint)
    {
        if (boat == null || boat.Compartments == null || boat.Compartments.Count == 0)
            return null;

        Compartment containing = null;
        float bestContainingScore = float.PositiveInfinity;

        Compartment nearest = null;
        float bestNearestScore = float.PositiveInfinity;

        for (int i = 0; i < boat.Compartments.Count; i++)
        {
            Compartment c = boat.Compartments[i];
            if (c == null)
                continue;

            Vector2[] poly = c.GetWorldCorners();
            if (poly == null || poly.Length < 3)
                continue;

            Vector2 center = GetPolygonCenter(poly);
            float distSq = ((Vector2)center - worldPoint).sqrMagnitude;

            if (distSq < bestNearestScore)
            {
                bestNearestScore = distSq;
                nearest = c;
            }

            if (PointInsideConvex(poly, worldPoint) && distSq < bestContainingScore)
            {
                bestContainingScore = distSq;
                containing = c;
            }
        }

        return containing != null ? containing : nearest;
    }

    private static Vector2 GetPolygonCenter(Vector2[] poly)
    {
        Vector2 sum = Vector2.zero;

        for (int i = 0; i < poly.Length; i++)
            sum += poly[i];

        return sum / Mathf.Max(1, poly.Length);
    }

    private static bool PointInsideConvex(Vector2[] poly, Vector2 point)
    {
        if (poly == null || poly.Length < 3)
            return false;

        bool hasPositive = false;
        bool hasNegative = false;

        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Length];

            Vector2 edge = b - a;
            Vector2 toPoint = point - a;

            float cross = edge.x * toPoint.y - edge.y * toPoint.x;

            if (cross > 0.0001f) hasPositive = true;
            if (cross < -0.0001f) hasNegative = true;

            if (hasPositive && hasNegative)
                return false;
        }

        return true;
    }
}