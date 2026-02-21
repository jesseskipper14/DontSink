using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BoatRegistry : MonoBehaviour, IBoatRegistry
{
    public event Action<Boat> BoatAdded;
    public event Action<Boat> BoatRemoved;

    private readonly List<Boat> _boats = new List<Boat>(8);
    private readonly HashSet<Boat> _set = new HashSet<Boat>();

    public IReadOnlyList<Boat> Boats => _boats;

    public void Register(Boat boat)
    {
        if (boat == null) return;
        if (_set.Contains(boat)) return;

        _set.Add(boat);
        _boats.Add(boat);

        if (!string.IsNullOrEmpty(boat.BoatInstanceId))
            _byId[boat.BoatInstanceId] = boat;

        BoatAdded?.Invoke(boat);
    }

    public void Unregister(Boat boat)
    {
        if (boat == null) return;
        if (!_set.Remove(boat)) return;

        _boats.Remove(boat);

        if (!string.IsNullOrEmpty(boat.BoatInstanceId) &&
            _byId.TryGetValue(boat.BoatInstanceId, out var cur) &&
            cur == boat)
        {
            _byId.Remove(boat.BoatInstanceId);
        }

        BoatRemoved?.Invoke(boat);
    }

    private void OnDestroy()
    {
        // Avoid dangling subscribers on domain reload / shutdown.
        BoatAdded = null;
        BoatRemoved = null;
        _byId.Clear();
    }

    private readonly Dictionary<string, Boat> _byId = new();

    public bool TryGetById(string id, out Boat boat)
    {
        boat = null;
        if (string.IsNullOrEmpty(id)) return false;
        return _byId.TryGetValue(id, out boat) && boat != null;
    }

}
