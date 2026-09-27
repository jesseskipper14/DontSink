using System;
using UnityEngine;

public enum FlotationPersistenceTargetKind
{
    None = 0,
    WorldFixed = 1,
    Boat = 2,
    Player = 3,
    WorldItem = 4,
    DivingBellPayload = 5
}

/// <summary>
/// Save/load-only snapshot for one deployed flotation bag.
///
/// This deliberately does NOT participate in ordinary scene-transition persistence.
/// NodeScene -> BoatScene / BoatScene -> NodeScene transitions are allowed to discard
/// deployed bags; the implied passage of time is treated as sufficient for them to
/// deflate. Explicit save/load is the only persistence boundary for this snapshot.
/// </summary>
[Serializable]
public sealed class FlotationBagSaveSnapshot
{
    public int version = 1;

    [SerializeReference]
    public ItemInstanceSnapshot item;

    public Vector2 worldPosition;
    public float worldRotationZ;

    public FlotationBagState state;
    public bool hasActivated;
    public float stateElapsedSeconds;

    [Range(0f, 1f)]
    public float flotation01;

    public FlotationPersistenceTargetKind targetKind;

    /// <summary>
    /// Meaning depends on targetKind:
    /// Boat -> BoatInstanceId
    /// Player -> PlayerLoadoutPersistence.PersistenceKey
    /// WorldItem -> ItemInstance.InstanceId
    /// DivingBellPayload -> deployed/stored tether payload ItemInstance.InstanceId
    /// WorldFixed/None -> unused
    /// </summary>
    public string targetStableId;

    // Rigidbody targets retain their body-local clicked attachment point.
    public Vector2 targetLocalPoint;

    // WorldFixed targets retain the literal world point.
    public Vector2 fixedWorldPoint;

    public float tetherLength = 0.65f;
}
