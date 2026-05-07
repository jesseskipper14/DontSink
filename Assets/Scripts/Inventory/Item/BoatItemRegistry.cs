using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatItemRegistry : MonoBehaviour
{
    private readonly HashSet<BoatOwnedItem> _items = new();

    public IReadOnlyCollection<BoatOwnedItem> Items => _items;

    public void Register(BoatOwnedItem item)
    {
        if (item == null)
            return;

        _items.Add(item);
    }

    public void Unregister(BoatOwnedItem item)
    {
        if (item == null)
            return;

        _items.Remove(item);
    }

    public List<BoatOwnedItem> SnapshotItems()
    {
        var result = new List<BoatOwnedItem>(_items.Count);

        foreach (var item in _items)
        {
            if (item != null && item.IsOwnedByBoat)
                result.Add(item);
        }

        return result;
    }
}