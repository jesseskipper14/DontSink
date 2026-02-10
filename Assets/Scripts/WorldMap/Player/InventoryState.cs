[System.Serializable]
public sealed class InventoryState
{
    public System.Collections.Generic.Dictionary<string, int> stacks =
        new System.Collections.Generic.Dictionary<string, int>();

    public int GetCount(string itemId) => stacks.TryGetValue(itemId, out var c) ? c : 0;

    public void Add(string itemId, int amount)
    {
        if (amount <= 0) return;
        stacks[itemId] = GetCount(itemId) + amount;
    }

    public bool Remove(string itemId, int amount)
    {
        if (amount <= 0) return true;
        int have = GetCount(itemId);
        if (have < amount) return false;
        int left = have - amount;
        if (left == 0) stacks.Remove(itemId);
        else stacks[itemId] = left;
        return true;
    }
}
