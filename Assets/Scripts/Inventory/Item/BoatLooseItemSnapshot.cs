using System;
using UnityEngine;

[Serializable]
public sealed class BoatLooseItemSnapshot
{
    public int version = 1;

    public string owningBoatInstanceId;

    [SerializeReference]
    public ItemInstanceSnapshot item;

    public Vector2 localPosition;
    public float localRotationZ;

    public string sceneHint;
    public bool wasSleeping;
}