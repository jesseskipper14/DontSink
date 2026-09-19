using System;
using UnityEngine;

[Serializable]
public sealed class BoatLooseItemSnapshot
{
    public int version = 3;

    public string owningBoatInstanceId;

    [SerializeReference]
    public ItemInstanceSnapshot item;

    public Vector2 localPosition;
    public float localRotationZ;

    public string sceneHint;
    public bool wasSleeping;

    // v3: optional physical diving-bell containment context.
    // Ordinary boat-local pose above remains as a safe fallback for legacy saves
    // or if the referenced bell cannot be reconstructed.
    public bool wasContainedInDivingBell;
    public string divingBellPayloadInstanceId;
    public Vector2 divingBellLocalPosition;
    public float divingBellLocalRotationZ;

    // Secured cargo/item state.
    public bool isSecured;
    public string secureZoneStableId;
    public int secureSlotIndex = -1;

    [Range(0f, 1f)]
    public float secureQualityMax01;

    [Range(0f, 1f)]
    public float secureQualityCurrent01;

    public Vector2 securedLocalPosition;
    public float securedLocalRotationZ;

    public bool usedRope;
    public float ropeBonus01;
}