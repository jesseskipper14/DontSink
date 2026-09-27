public enum WorldItemAttachmentPickupPolicy
{
    DetachOnPickup = 0,
    BlockPickup = 1
}

/// <summary>
/// A live external physical relationship involving a WorldItem.
/// The relationship remains runtime/world state and is deliberately not stored
/// inside ItemInstance data.
/// </summary>
public interface IWorldItemAttachment
{
    bool IsWorldItemAttachmentActive { get; }
    WorldItemAttachmentPickupPolicy PickupPolicy { get; }

    /// <summary>
    /// Applies the relationship's detach state after the attached WorldItem has
    /// already been successfully acquired.
    /// </summary>
    void DetachForWorldItemPickup();
}
