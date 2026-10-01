using System;
using System.Collections.Generic;

/// <summary>
/// Session-local cache of deterministic Phase-5B fragment textures.
/// Chart evidence is immutable, so one generated visual per fragment is sufficient for a map-table session.
/// </summary>
public sealed class CelestialChartBoardRenderCache : IDisposable
{
    private readonly Dictionary<string, CelestialChartFragmentVisual> _visuals =
        new(StringComparer.Ordinal);

    private readonly CelestialChartFragmentVisualSettings _settings;

    public CelestialChartBoardRenderCache(CelestialChartFragmentVisualSettings settings)
    {
        _settings = settings;
    }

    public CelestialChartFragmentVisual Get(CelestialChartFragmentSnapshot fragment)
    {
        if (fragment == null || string.IsNullOrWhiteSpace(fragment.fragmentId))
            return null;

        if (_visuals.TryGetValue(fragment.fragmentId, out CelestialChartFragmentVisual existing) && existing != null)
            return existing;

        CelestialChartFragmentVisual built = CelestialChartFragmentVisualBuilder.Build(fragment, _settings);
        if (built != null)
            _visuals[fragment.fragmentId] = built;

        return built;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<string, CelestialChartFragmentVisual> pair in _visuals)
            pair.Value?.Dispose();

        _visuals.Clear();
    }
}
