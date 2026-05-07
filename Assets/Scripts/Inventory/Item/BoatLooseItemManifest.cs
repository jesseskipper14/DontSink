using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BoatLooseItemManifest
{
    public int version = 1;

    [SerializeReference]
    public List<BoatLooseItemSnapshot> looseItems = new();
}