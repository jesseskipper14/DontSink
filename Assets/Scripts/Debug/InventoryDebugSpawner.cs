using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryDebugSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;

    [Header("Spawn Selection")]
    [SerializeField] private int selectedIndex = 0;
    [Min(1)]
    [SerializeField] private int spawnQuantity = 1;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode spawnSelectedKey = KeyCode.F6;
    [SerializeField] private KeyCode nextItemKey = KeyCode.PageDown;
    [SerializeField] private KeyCode previousItemKey = KeyCode.PageUp;
    [SerializeField] private KeyCode printCatalogKey = KeyCode.F7;

    [Header("Debug")]
    [SerializeField] private bool logSelectionChanges = true;
    [SerializeField] private bool logSpawnResults = true;

    private readonly List<ItemDefinition> cachedItems = new();

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        RebuildCache();
        ClampSelectedIndex();
        LogCurrentSelection("Awake");
    }

    private void Update()
    {
        if (Input.GetKeyDown(nextItemKey))
        {
            selectedIndex++;
            ClampSelectedIndex();
            LogCurrentSelection("Next");
        }

        if (Input.GetKeyDown(previousItemKey))
        {
            selectedIndex--;
            ClampSelectedIndex();
            LogCurrentSelection("Previous");
        }

        if (Input.GetKeyDown(printCatalogKey))
            PrintCatalog();

        if (Input.GetKeyDown(spawnSelectedKey))
            SpawnSelected();
    }

    [ContextMenu("Rebuild Cache")]
    public void RebuildCache()
    {
        cachedItems.Clear();

        if (itemCatalog == null)
        {
            Debug.LogWarning("[InventoryDebugSpawner] No item catalog assigned.", this);
            return;
        }

        // Uses the helper added below to ItemDefinitionCatalog.
        cachedItems.AddRange(itemCatalog.GetAllItems());

        // Remove nulls just in case the catalog has dead entries.
        for (int i = cachedItems.Count - 1; i >= 0; i--)
        {
            if (cachedItems[i] == null)
                cachedItems.RemoveAt(i);
        }
    }

    [ContextMenu("Spawn Selected")]
    public void SpawnSelected()
    {
        if (inventory == null)
        {
            Debug.LogWarning("[InventoryDebugSpawner] No PlayerInventory assigned.", this);
            return;
        }

        if (cachedItems.Count == 0)
        {
            Debug.LogWarning("[InventoryDebugSpawner] Catalog cache is empty. Rebuild cache or assign catalog.", this);
            return;
        }

        ClampSelectedIndex();

        ItemDefinition def = cachedItems[selectedIndex];
        if (def == null)
        {
            Debug.LogWarning($"[InventoryDebugSpawner] Selected item at index {selectedIndex} is null.", this);
            return;
        }

        int qty = Mathf.Clamp(spawnQuantity, 1, def.MaxStack);

        ItemInstance instance = ItemInstance.Create(def, qty);
        bool success = inventory.TryAddInstance(instance);

        if (logSpawnResults)
        {
            Debug.Log(
                $"[InventoryDebugSpawner] SpawnSelected | index={selectedIndex} | itemId={def.ItemId} | name={def.DisplayName} | qty={qty} | instanceId={instance.InstanceId} | success={success}",
                this);
        }
    }

    [ContextMenu("Print Catalog")]
    public void PrintCatalog()
    {
        if (cachedItems.Count == 0)
        {
            Debug.Log("[InventoryDebugSpawner] Catalog cache is empty.", this);
            return;
        }

        for (int i = 0; i < cachedItems.Count; i++)
        {
            ItemDefinition def = cachedItems[i];
            if (def == null)
            {
                Debug.Log($"[InventoryDebugSpawner] [{i}] <null>", this);
                continue;
            }

            Debug.Log(
                $"[InventoryDebugSpawner] [{i}] itemId={def.ItemId} | name={def.DisplayName} | maxStack={def.MaxStack} | equipSlot={def.EquipSlot} | container={def.IsContainer}",
                this);
        }
    }

    private void ClampSelectedIndex()
    {
        if (cachedItems.Count <= 0)
        {
            selectedIndex = 0;
            return;
        }

        if (selectedIndex < 0)
            selectedIndex = cachedItems.Count - 1;
        else if (selectedIndex >= cachedItems.Count)
            selectedIndex = 0;
    }

    private void LogCurrentSelection(string reason)
    {
        if (!logSelectionChanges)
            return;

        if (cachedItems.Count == 0)
        {
            Debug.Log($"[InventoryDebugSpawner] {reason} | no catalog items available", this);
            return;
        }

        ItemDefinition def = cachedItems[selectedIndex];
        Debug.Log(
            $"[InventoryDebugSpawner] {reason} | selectedIndex={selectedIndex} | itemId={def.ItemId} | name={def.DisplayName} | spawnQty={Mathf.Clamp(spawnQuantity, 1, def.MaxStack)}",
            this);
    }
}