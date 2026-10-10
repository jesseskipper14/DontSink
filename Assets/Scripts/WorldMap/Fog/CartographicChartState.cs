using System;
using UnityEngine;

public enum CartographicChartKind { Reference = 0, Georeferenced = 1, SoundingEvidence = 2 }

/// <summary>Host-issued per-item evidence. Registration is independent of where the item is carried.</summary>
[Serializable]
public sealed class CartographicChartState : ISerializationCallbackReceiver
{
    public int version = 1;
    public CartographicChartKind kind;
    public string title;
    [TextArea] public string referenceText;
    public string referenceSpriteResourcePath;
    // Optional immutable, carried star-bearing evidence. Empty legacy payloads
    // remain ordinary charts and are never eligible telescope references.
    public CelestialChartFragmentSnapshot starReference;
    public int worldSeed;
    public int topographyVersion;
    public Rect worldBounds;
    public WorldMapCartographicPayload payload;
    public SoundingChartEvidence sounding;
    [SerializeField] private bool hasSounding;
    [SerializeField] private bool hasPayload;
    [SerializeField] private bool hasSurfaceMask;
    [SerializeField] private bool hasBathymetryMask;
    public bool HasState => !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(referenceText) ||
        !string.IsNullOrWhiteSpace(payload?.sourceId);

    // Detached serialization copy: inventory transfers/save data cannot share mutable masks.
    public CartographicChartState Copy() => new CartographicChartState {
        version = version, kind = kind, title = title, referenceText = referenceText,
        referenceSpriteResourcePath = referenceSpriteResourcePath,
        starReference = StarReferenceItems.CopyFragment(starReference),
        worldSeed = worldSeed, topographyVersion = topographyVersion, worldBounds = worldBounds,
        sounding = sounding?.Copy(),
        payload = payload == null ? null : new WorldMapCartographicPayload {
            sourceId = payload.sourceId, surface = CopyMask(payload.surface), bathymetry = CopyMask(payload.bathymetry),
            nodeIds = payload.nodeIds == null ? null : (string[])payload.nodeIds.Clone(),
            surfacePoiIds = payload.surfacePoiIds == null ? null : (string[])payload.surfacePoiIds.Clone(),
            underwaterPoiIds = payload.underwaterPoiIds == null ? null : (string[])payload.underwaterPoiIds.Clone()
        }
    };
    private static WorldMapCoverageMask CopyMask(WorldMapCoverageMask mask) => mask == null ? null :
        new WorldMapCoverageMask { width = mask.width, height = mask.height, worldBounds = mask.worldBounds,
            cells = mask.cells == null ? null : (bool[])mask.cells.Clone() };
    // Unity inline serialization materializes null nested classes as empty objects.
    // Presence flags preserve absent optional payloads and masks across JSON and scene saves.
    public void OnBeforeSerialize()
    {
        hasPayload = payload != null;
        hasSounding = sounding != null;
        hasSurfaceMask = payload?.surface != null;
        hasBathymetryMask = payload?.bathymetry != null;
    }
    public void OnAfterDeserialize()
    {
        if (!hasSounding) sounding = null;
        if (!hasPayload) payload = null;
        else if (payload != null)
        {
            if (!hasSurfaceMask) payload.surface = null;
            if (!hasBathymetryMask) payload.bathymetry = null;
        }
    }

    public bool CanIntegrate(WorldMapTopographyField field, out string reason)
    {
        reason = null;
        if (kind != CartographicChartKind.Georeferenced) { reason = kind == CartographicChartKind.SoundingEvidence ? "Have a Surveyor process this Sounding Chart first." : "Reference charts cannot be integrated."; return false; }
        if (version != 1 || field == null || !field.IsValid || worldSeed != field.Seed ||
            topographyVersion != field.GenerationVersion || worldBounds != field.WorldBounds)
        { reason = "This chart is not registered to the current world."; return false; }
        if (payload == null) { reason = "The chart contains no registered data."; return false; }
        return true;
    }
}
