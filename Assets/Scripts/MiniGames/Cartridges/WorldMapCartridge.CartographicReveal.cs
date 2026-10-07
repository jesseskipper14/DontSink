using UnityEngine;

public sealed partial class WorldMapCartridge
{
    private WorldMapKnowledgeState _revealBefore;
    private float _revealStarted;
    private float _revealDuration = 2f;
    private readonly WorldMapBathymetryConcealment _priorBathymetry = new();
    private float CartographicReveal01 => Mathf.Clamp01((Time.unscaledTime - _revealStarted) / _revealDuration);
    private WorldMapKnowledgeState RevealBefore
    {
        get
        {
            if (_revealBefore != null && CartographicReveal01 >= 1f)
            { _revealBefore = null; _priorBathymetry.Clear(); }
            return _revealBefore;
        }
    }
    public bool TryGetCartographicContext(out WorldMapKnowledgeSource source, out WorldMapTopographyField field)
    {
        AutoWireKnowledgeSource();
        source = _knowledgeSource;
        source?.EnsureInitialized();
        field = _topographyDebugSource?.Field;
        return source != null && source.HasState && field != null && field.IsValid;
    }
    public void BeginCartographicReveal(WorldMapKnowledgeSaveSnapshot previous, float duration = 2f)
    {
        // Local session only: closing/reopening shows immediately committed knowledge.
        _priorBathymetry.Clear();
        _revealBefore = new WorldMapKnowledgeState();
        if (!_revealBefore.TryRestoreFromSnapshot(previous)) _revealBefore = null;
        _revealStarted = Time.unscaledTime;
        _revealDuration = Mathf.Clamp(duration, 1f, 3f);
    }
}
