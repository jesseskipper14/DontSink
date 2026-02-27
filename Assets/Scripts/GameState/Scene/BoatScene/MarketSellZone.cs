using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MarketSellZone : MonoBehaviour
{
    private readonly HashSet<CargoCrate> _inside = new HashSet<CargoCrate>();

    public IReadOnlyCollection<CargoCrate> CratesInside => _inside;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var crate = other != null ? other.GetComponentInParent<CargoCrate>() : null;
        if (crate != null) _inside.Add(crate);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var crate = other != null ? other.GetComponentInParent<CargoCrate>() : null;
        if (crate != null) _inside.Remove(crate);
    }

    public List<CargoCrate> SnapshotList()
    {
        var list = new List<CargoCrate>(_inside.Count);
        foreach (var c in _inside)
            if (c != null) list.Add(c);
        return list;
    }
}
