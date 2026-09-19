public interface IHoldPickupTickReceiver
{
    /// Return false to cancel the active hold action.
    bool TickHoldPickup(in InteractContext context, float deltaTime);
}