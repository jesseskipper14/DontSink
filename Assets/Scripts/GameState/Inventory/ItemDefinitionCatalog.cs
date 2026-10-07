using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinitionCatalog", menuName = "Game/Inventory/Item Definition Catalog")]
public sealed class ItemDefinitionCatalog : ScriptableObject, IItemDefinitionResolver
{
    [SerializeField] private List<ItemDefinition> items = new();

    private Dictionary<string, ItemDefinition> byId;

    public IReadOnlyList<ItemDefinition> GetAllItems()
    {
        return items;
    }

    private void BuildLookup()
    {
        if (byId != null)
            return;

        byId = new Dictionary<string, ItemDefinition>();

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition def = items[i];
            if (def == null || string.IsNullOrWhiteSpace(def.ItemId))
                continue;

            byId[def.ItemId] = def;
        }
    }

    public ItemDefinition Resolve(string itemId)
    {
        BuildLookup();

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        byId.TryGetValue(itemId, out ItemDefinition def);
        // Built-in chart carrier is additive; existing serialized catalog lists remain untouched.
        if (def == null && itemId == "item_cartographic_chart")
            def = Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart");
        return def;
    }
}
