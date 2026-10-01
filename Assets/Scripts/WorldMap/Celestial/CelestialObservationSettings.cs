using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Celestial/Celestial Observation Settings",
    fileName = "CelestialObservationSettings")]
public sealed class CelestialObservationSettings : ScriptableObject
{
    [Header("Visibility")]
    [Tooltip("Below this star-visibility value, the instrument may be inspected but calibration/connection cannot be completed.")]
    [Range(0f, 1f)] public float minimumStarVisibilityToInteract = 0.20f;

    [Header("Pattern")]
    [Tooltip("Minimum target stars required to create a valid observation puzzle.")]
    [Min(3)] public int minimumAnchors = 3;

    [Tooltip("Maximum target stars the pattern generator may use. Kept for save/asset compatibility with the first Phase 4 prototype.")]
    [Min(3)] public int maximumAnchors = 5;

    [Tooltip("Preferred number of stars in the decoded connect-the-dots pattern.")]
    [Range(3, 6)] public int patternStarCount = 4;

    [Tooltip("Legacy compatibility field. Phase 4 iteration 3 uses landmark stars only for the decoded pattern.")]
    [HideInInspector] public bool preferLandmarkPatternStars = true;

    [Tooltip("Legacy compatibility field from the early near-center survey prototype.")]
    [HideInInspector] public float patternSelectionRadius = 0.82f;

    [Tooltip("Radius, in normalized cartridge-space around the observation reference point, from which landmark stars may be chosen as required survey targets. The outer sky remains visible context but is never required. Larger values create broader/harder searches; smaller values keep patterns closer to the datum.")]
    [Range(0.25f, 0.48f)] public float surveyTargetRadius01 = 0.40f;


    [Header("Survey Variety")]
    [Tooltip("Completed surveys in the same region advance through overlapping landmark subsets instead of repeating the same target pattern.")]
    [Min(0.5f)] public float surveyRegionSizeWorld = 12f;

    [Tooltip("How many stars successive survey targets should normally share. 0.5 means roughly half the pattern overlaps with the previous survey.")]
    [Range(0.15f, 0.85f)] public float surveyPatternOverlapFraction = 0.50f;

    [Header("Instrument")]
    [Min(1f)] public float initialZoom = 1.6f;
    [Min(1f)] public float minimumZoom = 1f;
    [Min(1f)] public float maximumZoom = 6f;

    [Tooltip("Click radius for choosing a star while tracing the decoded pattern.")]
    [Min(4f)] public float notableClickRadiusPixels = 16f;

    [Tooltip("Radius of the usable circular aperture relative to its bounding square. 0.5 reaches the edge of the square.")]
    [Range(0.38f, 0.495f)] public float apertureRadius01 = 0.47f;

    [Header("Calibration Puzzle")]
    [Tooltip("Maximum magnitude of the hidden initial telescope rotation error.")]
    [Range(30f, 175f)] public float maximumInitialRotationErrorDegrees = 155f;

    [Tooltip("Legacy field retained for existing assets. Telescope rotation is intentionally not part of optical calibration anymore.")]
    [HideInInspector] public float bearingToleranceDegrees = 3f;

    [Tooltip("How close each cross-coupled registration pip must be to its center target before the optics can seat.")]
    [Range(0.04f, 0.30f)] public float registrationPipTolerance = 0.12f;

    // Legacy per-control tolerances retained so existing assets deserialize cleanly. Iteration 3
    // judges the visible cross-coupled registration pips instead.
    [HideInInspector] public float plateTolerance = 0.055f;
    [HideInInspector] public float lensTolerance = 0.055f;
    [HideInInspector] public float prismTolerance = 0.055f;

    [Tooltip("How strongly the plate control scrambles alternating decoder points when misaligned.")]
    [Range(0.05f, 0.55f)] public float decoderPlateDistortion = 0.28f;

    [Tooltip("How strongly the lens control radially distorts decoder points when misaligned.")]
    [Range(0.05f, 0.65f)] public float decoderLensDistortion = 0.34f;

    [Tooltip("How strongly prism trim shears/staggers decoder points when misaligned.")]
    [Range(0.05f, 0.65f)] public float decoderPrismDistortion = 0.26f;

    [Header("Observation Capture")]
    [Tooltip("Include ambient stars inside the circular aperture. These become puzzle texture on the eventual paper fragment.")]
    public bool captureAmbientStars = true;

    [Tooltip("Include notable non-pattern celestial objects inside the circular aperture.")]
    public bool captureUnselectedNotableObjects = true;

    // Legacy Phase 4 fields intentionally retained so existing assets deserialize cleanly.
    [HideInInspector] public bool allowLandmarkStars = true;
    [HideInInspector] public bool allowNebulae = true;
    [HideInInspector] public bool allowDeepSkyObjects = true;
    [HideInInspector] public float chartReticleMargin01 = 0.12f;

    private void OnValidate()
    {
        minimumAnchors = Mathf.Clamp(minimumAnchors, 3, 6);
        maximumAnchors = Mathf.Clamp(maximumAnchors, minimumAnchors, 6);
        patternStarCount = Mathf.Clamp(patternStarCount, minimumAnchors, maximumAnchors);

        minimumZoom = Mathf.Max(1f, minimumZoom);
        maximumZoom = Mathf.Max(minimumZoom, maximumZoom);
        initialZoom = Mathf.Clamp(initialZoom, minimumZoom, maximumZoom);

        surveyTargetRadius01 = Mathf.Clamp(surveyTargetRadius01, 0.25f, 0.48f);
        surveyRegionSizeWorld = Mathf.Max(0.5f, surveyRegionSizeWorld);
        surveyPatternOverlapFraction = Mathf.Clamp(surveyPatternOverlapFraction, 0.15f, 0.85f);

        notableClickRadiusPixels = Mathf.Max(4f, notableClickRadiusPixels);
        apertureRadius01 = Mathf.Clamp(apertureRadius01, 0.38f, 0.495f);
        maximumInitialRotationErrorDegrees = Mathf.Clamp(maximumInitialRotationErrorDegrees, 30f, 175f);
        bearingToleranceDegrees = Mathf.Clamp(bearingToleranceDegrees, 0.5f, 12f);
        registrationPipTolerance = Mathf.Clamp(registrationPipTolerance, 0.04f, 0.30f);
        plateTolerance = Mathf.Clamp(plateTolerance, 0.01f, 0.20f);
        lensTolerance = Mathf.Clamp(lensTolerance, 0.01f, 0.20f);
        prismTolerance = Mathf.Clamp(prismTolerance, 0.01f, 0.20f);
        decoderPlateDistortion = Mathf.Clamp(decoderPlateDistortion, 0.05f, 0.55f);
        decoderLensDistortion = Mathf.Clamp(decoderLensDistortion, 0.05f, 0.65f);
        decoderPrismDistortion = Mathf.Clamp(decoderPrismDistortion, 0.05f, 0.65f);
    }
}
