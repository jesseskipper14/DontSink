using UnityEngine;

public enum BoatWaterExposureKind
{
    None = 0,

    // Global/ocean water. Uses WaveManager/WaveField sampling.
    Ocean = 1,

    // Point is inside an intact interior compartment, but that compartment has no meaningful water.
    DryInterior = 2,

    // Point is inside a partially flooded compartment. Uses a flat compartment water surface.
    CompartmentPartial = 3,

    // Point is inside a fully flooded compartment. Uses a dumb full-submersion flat surface.
    CompartmentFull = 4,

    // Entire boat is truly flooded, so we intentionally simplify back to ocean behavior.
    FullyFloodedBoatOcean = 5
}

public struct BoatWaterExposure
{
    public BoatWaterExposureKind Kind;
    public Compartment Compartment;
    public float FlatSurfaceY;

    public bool HasWater =>
        Kind == BoatWaterExposureKind.Ocean ||
        Kind == BoatWaterExposureKind.CompartmentPartial ||
        Kind == BoatWaterExposureKind.CompartmentFull ||
        Kind == BoatWaterExposureKind.FullyFloodedBoatOcean;

    public bool UsesOceanSurface =>
        Kind == BoatWaterExposureKind.Ocean ||
        Kind == BoatWaterExposureKind.FullyFloodedBoatOcean;

    public bool UsesFlatSurface =>
        Kind == BoatWaterExposureKind.CompartmentPartial ||
        Kind == BoatWaterExposureKind.CompartmentFull;

    public bool AllowsWaveMomentumCoupling => UsesOceanSurface;

    public static BoatWaterExposure Ocean()
    {
        return new BoatWaterExposure
        {
            Kind = BoatWaterExposureKind.Ocean
        };
    }

    public static BoatWaterExposure FullyFloodedBoatOcean()
    {
        return new BoatWaterExposure
        {
            Kind = BoatWaterExposureKind.FullyFloodedBoatOcean
        };
    }

    public static BoatWaterExposure DryInterior(Compartment compartment)
    {
        return new BoatWaterExposure
        {
            Kind = BoatWaterExposureKind.DryInterior,
            Compartment = compartment
        };
    }

    public static BoatWaterExposure CompartmentPartial(Compartment compartment, float surfaceY)
    {
        return new BoatWaterExposure
        {
            Kind = BoatWaterExposureKind.CompartmentPartial,
            Compartment = compartment,
            FlatSurfaceY = surfaceY
        };
    }

    public static BoatWaterExposure CompartmentFull(Compartment compartment, float surfaceY)
    {
        return new BoatWaterExposure
        {
            Kind = BoatWaterExposureKind.CompartmentFull,
            Compartment = compartment,
            FlatSurfaceY = surfaceY
        };
    }
}

[DisallowMultipleComponent]
public sealed class BoatWaterContextResolver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Boat boat;

    [Header("Thresholds")]
    [Tooltip("Water fraction at/below this is treated as dry for water exposure.")]
    [Range(0f, 0.1f)]
    [SerializeField] private float dryWaterFractionEpsilon = 0.0025f;

    [Tooltip("A compartment must be this full before it is treated as fully flooded.")]
    [Range(0.9f, 1f)]
    [SerializeField] private float fullCompartmentThreshold01 = 0.999f;

    [Tooltip("If every compartment is this full, the boat can simplify back to ocean exposure.")]
    [SerializeField] private bool treatFullyFloodedBoatAsOcean = true;

    [Tooltip("Surface Y padding used for fully flooded compartments so polygon clipping treats bodies as submerged.")]
    [Min(0.01f)]
    [SerializeField] private float fullCompartmentSurfacePadding = 0.5f;

    [Header("Cache")]
    [SerializeField] private bool includeInactiveCompartments = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Compartment[] _compartments;

    public Boat Boat => boat;
    public string BoatInstanceId => boat != null ? boat.BoatInstanceId : null;

    private void Awake()
    {
        CacheRefs();
        RefreshCompartmentCache();
    }

    private void OnValidate()
    {
        dryWaterFractionEpsilon = Mathf.Clamp(dryWaterFractionEpsilon, 0f, 0.1f);
        fullCompartmentThreshold01 = Mathf.Clamp(fullCompartmentThreshold01, 0.9f, 1f);
        fullCompartmentSurfacePadding = Mathf.Max(0.01f, fullCompartmentSurfacePadding);

        CacheRefs();
    }

    [ContextMenu("Refresh Compartment Cache")]
    public void RefreshCompartmentCache()
    {
        _compartments = GetComponentsInChildren<Compartment>(includeInactiveCompartments);

        Log($"Cached compartments={(_compartments != null ? _compartments.Length : 0)}");
    }

    public bool TryResolveAtPoint(Vector2 worldPoint, out BoatWaterExposure exposure)
    {
        CacheRefs();

        if (_compartments == null || _compartments.Length == 0)
            RefreshCompartmentCache();

        if (treatFullyFloodedBoatAsOcean && IsBoatFullyFlooded())
        {
            exposure = BoatWaterExposure.FullyFloodedBoatOcean();
            return true;
        }

        Compartment compartment = FindContainingCompartment(worldPoint);



        if (compartment == null)
        {
            exposure = BoatWaterExposure.Ocean();
            return true;
        }

        float fraction = GetWaterFraction01(compartment);

        if (fraction <= dryWaterFractionEpsilon)
        {
            exposure = BoatWaterExposure.DryInterior(compartment);
            return true;
        }

        if (fraction >= fullCompartmentThreshold01)
        {
            exposure = BoatWaterExposure.CompartmentFull(
                compartment,
                GetFullCompartmentSurfaceY(compartment));

            return true;
        }

        exposure = BoatWaterExposure.CompartmentPartial(
            compartment,
            compartment.GetWaterSurfaceWorldY());

        return true;
    }

    public bool TryFindContainingCompartment(Vector2 worldPoint, out Compartment compartment)
    {
        compartment = FindContainingCompartment(worldPoint);
        return compartment != null;
    }

    public bool IsBoatFullyFlooded()
    {
        if (_compartments == null || _compartments.Length == 0)
            return false;

        bool hasAnyCompartment = false;

        for (int i = 0; i < _compartments.Length; i++)
        {
            Compartment c = _compartments[i];
            if (c == null)
                continue;

            hasAnyCompartment = true;

            if (GetWaterFraction01(c) < fullCompartmentThreshold01)
                return false;
        }

        return hasAnyCompartment;
    }

    private Compartment FindContainingCompartment(Vector2 worldPoint)
    {
        if (_compartments == null)
            return null;

        for (int i = 0; i < _compartments.Length; i++)
        {
            Compartment c = _compartments[i];
            if (c == null || !c.isActiveAndEnabled)
                continue;

            if (PointInPolygon(worldPoint, c.GetWorldCorners()))
                return c;
        }

        return null;
    }

    private static float GetWaterFraction01(Compartment compartment)
    {
        if (compartment == null)
            return 0f;

        float max = compartment.MaxWaterArea;
        if (max <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(compartment.WaterArea / max);
    }

    private float GetFullCompartmentSurfaceY(Compartment compartment)
    {
        if (compartment == null)
            return 0f;

        Vector2[] corners = compartment.GetWorldCorners();

        float maxY = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
            maxY = Mathf.Max(maxY, corners[i].y);

        return maxY + fullCompartmentSurfacePadding;
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
            return false;

        bool inside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            bool crosses =
                (a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / ((b.y - a.y) + 0.000001f) + a.x;

            if (crosses)
                inside = !inside;
        }

        return inside;
    }

    private void CacheRefs()
    {
        if (boat == null)
            boat = GetComponent<Boat>();

        if (boat == null)
            boat = GetComponentInParent<Boat>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatWaterContextResolver:{name}] {msg}", this);
    }
}