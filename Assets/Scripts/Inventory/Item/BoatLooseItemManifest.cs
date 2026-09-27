using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BoatLooseItemManifest
{
    public int version = 4;

    [SerializeReference]
    public List<BoatLooseItemSnapshot> looseItems = new();

    // Independent world objects that are explicitly configured to survive even
    // after they are no longer boat-owned (cut anchors, future bells/hooks, etc.).
    [SerializeReference]
    public List<PersistentWorldItemSnapshot> persistentWorldItems = new();

    // Explicit SAVE/LOAD only. Scene transitions deliberately capture this as empty so
    // deployed flotation bags are allowed to expire during the implied travel time.
    [SerializeReference]
    public List<FlotationBagSaveSnapshot> saveOnlyFlotationBags = new();
}