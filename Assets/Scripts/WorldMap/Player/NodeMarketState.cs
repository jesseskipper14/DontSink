[System.Serializable]
public sealed class NodeMarketState
{
    public string nodeId;
    public InventoryState stock = new InventoryState();
    public int lastGeneratedDay; // or timestamp
}
