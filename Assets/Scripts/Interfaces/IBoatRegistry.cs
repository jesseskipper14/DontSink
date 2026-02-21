using System;
using System.Collections.Generic;

public interface IBoatRegistry
{
    event Action<Boat> BoatAdded;
    event Action<Boat> BoatRemoved;

    IReadOnlyList<Boat> Boats { get; }

    void Register(Boat boat);
    void Unregister(Boat boat);

    bool TryGetById(string id, out Boat boat);
}
