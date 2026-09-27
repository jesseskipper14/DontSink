/// <summary>
/// Optional participant in the WorldItem pickup transaction.
///
/// WorldItem first validates every participant. Only after the item has been
/// successfully acquired does it invoke OnWorldItemPickupCommitted on the
/// participants that were present for that transaction.
/// </summary>
public interface IWorldItemPickupParticipant
{
    /// <summary>
    /// Return false to block this WorldItem from being picked up right now.
    /// This must be side-effect free: pickup may still fail for another reason.
    /// </summary>
    bool AllowsWorldItemPickup(in InteractContext context);

    /// <summary>
    /// Called only after the ItemInstance has successfully entered the acquiring
    /// player's inventory/equipment path. Relationship cleanup belongs here,
    /// never in the validation phase.
    /// </summary>
    void OnWorldItemPickupCommitted();
}
