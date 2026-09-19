using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BoatLooseItemManifest
{
    public int version = 3;

    [SerializeReference]
    public List<BoatLooseItemSnapshot> looseItems = new();

    // Independent world objects that are explicitly configured to survive even
    // after they are no longer boat-owned (cut anchors, future bells/hooks, etc.).
    [SerializeReference]
    public List<PersistentWorldItemSnapshot> persistentWorldItems = new();
}