using UnityEngine;

public sealed partial class HandheldSoundingLineController
{
    private CartographicChartState soundingEvidence;
    private bool soundingRecorded;
    public string SoundingChartNote { get; private set; }
    public bool CanRecordSounding => isActiveAndEnabled && GameplayAuthority.IsAuthoritative && deployed && !retrieving &&
        bottomLatched && soundingEvidence != null && !soundingRecorded && _activeSounderItem != null &&
        ReferenceEquals(equipment?.Get(BottomBarSlotType.Hands), _activeSounderItem);

    private void ClearSoundingEvidence()
    { soundingEvidence = null; soundingRecorded = false; SoundingChartNote = null; }

    private void CaptureSoundingEvidence()
    {
        ClearSoundingEvidence();
        if (!GameplayAuthority.IsAuthoritative || _spawnedWeight == null ||
            !WorldNavigationService.TryGetTrueWorldPosition(out var position)) return;
        var source = FindFirstObjectByType<WorldMapKnowledgeSource>();
        var waves = FindFirstObjectByType<WaveManager>();
        if (source == null || !source.TryGetSurfaceSurveyWorld(out var field, out _)) return;
        float depth = (waves != null ? waves.SampleSurfaceY(_spawnedWeight.transform.position.x) : 0) - _spawnedWeight.transform.position.y;
        var reading = new SoundingChartEvidence { position = new WorldTopology(field.WorldBounds).Normalize(position), measuredDepthMeters = depth };
        if (!reading.IsValid(field.WorldBounds)) return;
        soundingEvidence = new CartographicChartState { kind = CartographicChartKind.SoundingEvidence,
            title = "Sounding Chart — unprocessed bottom reading", worldSeed = field.Seed,
            topographyVersion = field.GenerationVersion, worldBounds = field.WorldBounds, sounding = reading,
            referenceText = $"Measured bottom depth: {depth:0.0} m. Requires Surveyor processing; grants no map coverage on recording." };
    }

    public bool TryRecordSounding(out string reason)
    {
        reason = "Reach bottom with the sounding weight before recording. Each bottom reading can be recorded once.";
        if (!CanRecordSounding) return false;
        bool success = SoundingCartographyService.TryRecord(gameObject, soundingEvidence, out reason);
        if (success) soundingRecorded = true;
        SoundingChartNote = reason;
        return success;
    }
}
