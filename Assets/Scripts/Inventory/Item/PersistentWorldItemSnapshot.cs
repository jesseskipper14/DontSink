using System;
using UnityEngine;

public enum PersistentWorldItemContextKind
{
    Scene = 0,
    Node = 1,
    Route = 2
}

[Serializable]
public sealed class PersistentWorldItemSnapshot
{
    public int version = 1;

    [SerializeReference]
    public ItemInstanceSnapshot item;

    public PersistentWorldItemContextKind contextKind;

    // Always retained as a fallback/disambiguator.
    public string sceneName;

    // Node context.
    public string nodeStableId;

    // Route context. Direction is intentionally preserved in v1.
    public string routeFromNodeId;
    public string routeToNodeId;
    public int routeSeed;

    public Vector2 worldPosition;
    public float worldRotationZ;
}
