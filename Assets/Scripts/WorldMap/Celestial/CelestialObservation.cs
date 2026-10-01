using System;
using System.Collections.Generic;
using UnityEngine;

public enum CelestialObservationSource
{
    PlayerCharted = 0,
    FoundChart = 1,
    PurchasedChart = 2,
    QuestReward = 3,
    Ruin = 4,
    Debug = 5
}

[Serializable]
public sealed class CelestialObservationObject
{
    public string stableId;
    public CelestialObjectKind kind;
    public CelestialColorClass colorClass;
    public Vector2 worldPosition;

    [Range(0f, 1f)] public float brightness01;
    [Range(0f, 1f)] public float prominence01;

    [Tooltip("Position as observed inside the calibrated circular instrument. X increases right, Y increases up.")]
    public Vector2 instrumentPosition01;

    public bool isAnchor;
    public bool isReference;
}

/// <summary>
/// Immutable-ish evidence produced by a successful celestial charting session.
/// Phase 4 intentionally does not persist this yet. Phase 5 will turn observations
/// into authoritative physical chart fragments and chart-paper consumption.
/// </summary>
[Serializable]
public sealed class CelestialObservation
{
    public string observationId;
    public CelestialObservationSource source = CelestialObservationSource.PlayerCharted;

    [Header("Celestial Truth Identity")]
    public int worldSeed;
    public int celestialGeneratorVersion;
    public string celestialConfigHash;

    [Header("Observation Origin")]
    public Vector2 observerTrueWorldPosition;
    public Rect baseVisibleWorldRect;

    [Header("Instrument Framing")]
    public Vector2 instrumentCenter01 = new Vector2(0.5f, 0.5f);
    public float instrumentZoom = 1f;
    public float instrumentRotationDegrees;
    public float circularApertureRadius01 = 0.47f;

    // Retained for compatibility with the first Phase 4 observation shape.
    public Rect chartReticle01 = new Rect(0f, 0f, 1f, 1f);

    [Header("Calibration")]
    [Range(0f, 1f)] public float calibrationQuality01;
    public float bearingControlDegrees;
    public float plateControl;
    public float lensControl;
    public float prismControl;

    [Header("Conditions")]
    [Range(0f, 1f)] public float starVisibility01;
    [Range(0f, 1f)] public float quality01;
    public float capturedHour;
    public int capturedYear;
    public int capturedMonth;
    public int capturedDay;

    [Header("Optional Reference Star")]
    public string referenceObjectStableId;

    [Header("Survey Sequence")]
    [Tooltip("Deterministic survey sequence used to choose this observation's overlapping landmark subset.")]
    public int surveySequence;

    [Header("Connect-The-Dots Pattern")]
    [Tooltip("Stable IDs in the path order the player successfully transcribed. Reverse traversal is considered equivalent gameplay-wise.")]
    public List<string> patternObjectStableIds = new();

    [Header("Observed Objects")]
    public List<CelestialObservationObject> objects = new();

    public int AnchorCount
    {
        get
        {
            int count = 0;
            if (objects == null) return 0;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].isAnchor)
                    count++;
            }
            return count;
        }
    }

    public bool HasReference => !string.IsNullOrWhiteSpace(referenceObjectStableId);
}
