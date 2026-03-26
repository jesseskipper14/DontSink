public interface IWorldItemDropTarget
{
    bool CanAcceptWorldDrop(ItemInstance incoming);
    bool TryAcceptWorldDrop(ItemInstance incoming, out ItemInstance remainder);
}