public interface IWorldItemDropTarget
{
    bool CanAcceptWorldDrop(
        in WorldItemDropContext context,
        ItemInstance incoming);

    bool TryAcceptWorldDrop(
        in WorldItemDropContext context,
        ItemInstance incoming,
        out ItemInstance remainder);
}
