using System.Collections.Generic;

/// <summary>
/// Read-only runtime lookup for deterministic constellation truth.
/// </summary>
public sealed class CelestialConstellationCatalog
{
    private readonly List<CelestialConstellation> _constellations;
    private readonly Dictionary<string, CelestialConstellation> _byId;
    private readonly Dictionary<string, CelestialConstellation> _byMemberStarId;

    public IReadOnlyList<CelestialConstellation> All => _constellations;
    public int Count => _constellations.Count;

    internal CelestialConstellationCatalog(List<CelestialConstellation> constellations)
    {
        _constellations = constellations ?? new List<CelestialConstellation>();
        _byId = new Dictionary<string, CelestialConstellation>(_constellations.Count);
        _byMemberStarId = new Dictionary<string, CelestialConstellation>(_constellations.Count * 5);

        for (int i = 0; i < _constellations.Count; i++)
        {
            CelestialConstellation constellation = _constellations[i];
            if (constellation == null || string.IsNullOrWhiteSpace(constellation.StableId))
                continue;

            _byId[constellation.StableId] = constellation;

            IReadOnlyList<string> members = constellation.MemberStarStableIds;
            for (int m = 0; m < members.Count; m++)
            {
                string memberId = members[m];
                if (!string.IsNullOrWhiteSpace(memberId) && !_byMemberStarId.ContainsKey(memberId))
                    _byMemberStarId.Add(memberId, constellation);
            }
        }
    }

    public bool TryGetById(string stableId, out CelestialConstellation constellation)
    {
        constellation = null;
        return !string.IsNullOrWhiteSpace(stableId) && _byId.TryGetValue(stableId, out constellation);
    }

    public bool TryGetByMemberStar(string starStableId, out CelestialConstellation constellation)
    {
        constellation = null;
        return !string.IsNullOrWhiteSpace(starStableId) && _byMemberStarId.TryGetValue(starStableId, out constellation);
    }
}
