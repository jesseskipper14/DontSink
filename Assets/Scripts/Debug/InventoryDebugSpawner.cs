using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryDebugSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ItemDefinitionCatalog itemCatalog;
    [SerializeField] private ItemAcquisitionResolver acquisitionResolver;
    [SerializeField] private Transform dropOrigin;

    [Header("Spawn Selection")]
    [SerializeField] private int selectedIndex = 0;
    [Min(1)]
    [SerializeField] private int spawnQuantity = 1;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode spawnSelectedKey = KeyCode.F6;
    [SerializeField] private KeyCode nextItemKey = KeyCode.PageDown;
    [SerializeField] private KeyCode previousItemKey = KeyCode.PageUp;
    [SerializeField] private KeyCode printCatalogKey = KeyCode.F7;
    [SerializeField] private KeyCode toggleHudKey = KeyCode.F8;

    [SerializeField] private KeyCode increaseQuantityKey = KeyCode.Equals;
    [SerializeField] private KeyCode decreaseQuantityKey = KeyCode.Minus;
    [SerializeField] private KeyCode increaseQuantityFastKey = KeyCode.RightBracket;
    [SerializeField] private KeyCode decreaseQuantityFastKey = KeyCode.LeftBracket;

    [Header("HUD")]
    [SerializeField] private bool showHud = true;
    [SerializeField] private Vector2 hudPosition = new Vector2(16f, 16f);

    [SerializeField] private bool autoFitHudToScreen = true;
    [SerializeField] private float hudWidth = 640f;
    [SerializeField] private float hudHeight = 176f;
    [SerializeField] private float hudScreenMargin = 16f;

    [SerializeField] private int fontSize = 12;
    [SerializeField] private bool showNeighborItems = true;

    [Header("Charge Override")]
    [SerializeField] private bool overrideSpawnCharges = false;
    [SerializeField] private int spawnCharges = 100;
    [SerializeField] private int chargeSmallStep = 1;
    [SerializeField] private int chargeLargeStep = 10;

    [Header("Debug")]
    [SerializeField] private bool logSelectionChanges = true;
    [SerializeField] private bool logSpawnResults = true;

    private readonly List<ItemDefinition> cachedItems = new();

    private GUIStyle _boxStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _hintStyle;

    private void Awake()
    {
        if (acquisitionResolver == null)
            acquisitionResolver = GetComponent<ItemAcquisitionResolver>();

        if (dropOrigin == null)
            dropOrigin = transform;

        RebuildCache();
        ClampSelectedIndex();
        LogCurrentSelection("Awake");
    }

    private void Update()
    {
        if (GameplayInputBlocker.IsBlocked)
            return;

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

        if (Input.GetKeyDown(toggleHudKey))
            showHud = !showHud;

        if (Input.GetKeyDown(printCatalogKey))
            PrintCatalog();

        if (Input.GetKeyDown(spawnSelectedKey))
            SpawnSelected();

        if (Input.GetKeyDown(increaseQuantityKey))
        {
            AdjustSpawnQuantity(+1);
        }

        if (Input.GetKeyDown(decreaseQuantityKey))
        {
            AdjustSpawnQuantity(-1);
        }

        if (Input.GetKeyDown(increaseQuantityFastKey))
        {
            AdjustSpawnQuantity(+10);
        }

        if (Input.GetKeyDown(decreaseQuantityFastKey))
        {
            AdjustSpawnQuantity(-10);
        }
    }

    private void OnGUI()
    {
        if (!showHud)
            return;

        EnsureStyles();

        float width = autoFitHudToScreen
            ? Mathf.Min(hudWidth, Mathf.Max(260f, Screen.width - hudPosition.x - hudScreenMargin))
            : hudWidth;

        float height = hudHeight;

        Rect box = new Rect(hudPosition.x, hudPosition.y, width, height);
        GUI.Box(box, GUIContent.none, _boxStyle);

        if (cachedItems.Count == 0)
        {
            GUI.Label(
                new Rect(box.x + 12f, box.y + 10f, box.width - 24f, 24f),
                "Debug Spawn: No catalog items",
                _titleStyle);

            GUI.Label(
                new Rect(box.x + 12f, box.y + 42f, box.width - 24f, 22f),
                "Assign ItemDefinitionCatalog or rebuild cache.",
                _bodyStyle);

            return;
        }

        ClampSelectedIndex();

        ItemDefinition selected = cachedItems[selectedIndex];

        if (selected == null)
        {
            GUI.Label(
                new Rect(box.x + 12f, box.y + 10f, box.width - 24f, 24f),
                $"F6 Spawn: [{selectedIndex + 1}/{cachedItems.Count}] <null>",
                _titleStyle);

            return;
        }

        int qty = Mathf.Clamp(spawnQuantity, 1, selected.MaxStack);

        GUI.Label(
            new Rect(box.x + 12f, box.y + 8f, box.width - 24f, 24f),
            $"F6 Spawn: [{selectedIndex + 1}/{cachedItems.Count}] {selected.DisplayName}",
            _titleStyle);

        string traits = BuildTraitText(selected);
        GUI.Label(
            new Rect(box.x + 12f, box.y + 34f, box.width - 24f, 22f),
            $"itemId: {selected.ItemId}   qty: {qty}   {traits}",
            _bodyStyle);

        GUI.Label(
            new Rect(box.x + 12f, box.y + 58f, box.width - 24f, 20f),
            $"{previousItemKey}/{nextItemKey}: item     {decreaseQuantityKey}/{increaseQuantityKey}: qty -/+1     {decreaseQuantityFastKey}/{increaseQuantityFastKey}: qty -/+10",
            _hintStyle);

        GUI.Label(
            new Rect(box.x + 12f, box.y + 80f, box.width - 24f, 20f),
            $"{spawnSelectedKey}: spawn     {toggleHudKey}: toggle HUD     {printCatalogKey}: print catalog",
            _hintStyle);

        float y = 104f;

        if (showNeighborItems)
        {
            string prev = GetNeighborName(-1);
            string next = GetNeighborName(+1);

            GUI.Label(
                new Rect(box.x + 12f, box.y + y, box.width - 24f, 22f),
                $"Prev: {prev}    |    Next: {next}",
                _hintStyle);

            y += 24f;
        }

        DrawChargeOverrideControls(box, selected, y);
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

        cachedItems.AddRange(itemCatalog.GetAllItems());

        for (int i = cachedItems.Count - 1; i >= 0; i--)
        {
            if (cachedItems[i] == null)
                cachedItems.RemoveAt(i);
        }
    }

    [ContextMenu("Spawn Selected")]
    public void SpawnSelected()
    {
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

        if (overrideSpawnCharges && instance.HasCharges)
        {
            instance.SetCharges(spawnCharges);
        }

        bool acquired = false;
        bool dropped = false;

        if (acquisitionResolver != null)
            acquired = acquisitionResolver.TryAcquire(instance);

        if (!acquired)
            dropped = TrySpawnToWorld(instance);

        if (logSpawnResults)
        {
            Debug.Log(
                $"[InventoryDebugSpawner] SpawnSelected | index={selectedIndex} | itemId={def.ItemId} | name={def.DisplayName} | qty={qty} | instanceId={instance.InstanceId} | acquired={acquired} | dropped={dropped}",
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
                $"[InventoryDebugSpawner] [{i}] itemId={def.ItemId} | name={def.DisplayName} | maxStack={def.MaxStack} | equipSlot={def.EquipSlot} | container={def.IsContainer} | module={def.IsModule}",
                this);
        }
    }

    private bool TrySpawnToWorld(ItemInstance instance)
    {
        if (instance == null || instance.Definition == null)
            return false;

        if (instance.Definition.WorldPrefab == null)
        {
            Debug.LogWarning($"[InventoryDebugSpawner] Cannot drop '{instance.Definition.ItemId}' because WorldPrefab is missing.", this);
            return false;
        }

        Vector3 pos = dropOrigin != null ? dropOrigin.position : transform.position;

        // Important: use the same path as real drops so boat ownership,
        // collision policy, and visual policy are applied.
        return WorldItemDropUtility.TryDrop(instance, pos, gameObject, out _);
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
        if (def == null)
        {
            Debug.Log($"[InventoryDebugSpawner] {reason} | selectedIndex={selectedIndex} | <null>", this);
            return;
        }

        Debug.Log(
            $"[InventoryDebugSpawner] {reason} | selectedIndex={selectedIndex} | itemId={def.ItemId} | name={def.DisplayName} | spawnQty={Mathf.Clamp(spawnQuantity, 1, def.MaxStack)}",
            this);
    }

    private string BuildTraitText(ItemDefinition def)
    {
        if (def == null)
            return "";

        List<string> bits = new List<string>(6);

        if (def.IsModule)
            bits.Add("Module");

        if (def.IsContainer)
            bits.Add($"Container:{def.ContainerSlotCount}");

        if (def.HasCharges)
            bits.Add($"Charges:{def.MaxCharges}");

        if (def.IsEquippable)
            bits.Add($"Equip:{def.EquipSlot}");

        if (def.WorldPrefab != null)
            bits.Add("WorldPrefab");

        if (!def.Droppable)
            bits.Add("NotDroppable");

        return bits.Count > 0 ? string.Join(" | ", bits) : "General";
    }

    private string GetNeighborName(int offset)
    {
        if (cachedItems.Count == 0)
            return "-";

        int index = selectedIndex + offset;

        if (index < 0)
            index = cachedItems.Count - 1;
        else if (index >= cachedItems.Count)
            index = 0;

        ItemDefinition def = cachedItems[index];
        return def != null ? def.DisplayName : "<null>";
    }

    private void EnsureStyles()
    {
        if (_boxStyle != null)
            return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 8, 8)
        };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize + 2,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            normal = { textColor = Color.white }
        };

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, fontSize - 1),
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 1f) }
        };
    }

    private void AdjustSpawnQuantity(int delta)
    {
        int max = 999;

        if (cachedItems.Count > 0)
        {
            ClampSelectedIndex();

            ItemDefinition selected = cachedItems[selectedIndex];
            if (selected != null)
                max = Mathf.Max(1, selected.MaxStack);
        }

        spawnQuantity = Mathf.Clamp(spawnQuantity + delta, 1, max);

        LogCurrentSelection(delta > 0 ? "Quantity +" : "Quantity -");
    }

    private void DrawChargeOverrideControls(Rect box, ItemDefinition selected, float yOffset)
    {
        if (selected == null || !selected.HasCharges)
        {
            GUI.Label(
                new Rect(box.x + 12f, box.y + yOffset, box.width - 24f, 22f),
                "Charges: selected item has no charges",
                _hintStyle);

            return;
        }

        int max = Mathf.Max(1, selected.MaxCharges);
        spawnCharges = Mathf.Clamp(spawnCharges, 0, max);

        float x = box.x + 12f;
        float y = box.y + yOffset;
        float buttonH = 24f;
        float gap = 6f;

        GUI.Label(
            new Rect(x, y, 190f, buttonH),
            $"Charges: {(overrideSpawnCharges ? spawnCharges.ToString() : "default")} / {max}",
            _bodyStyle);

        x += 196f;

        if (GUI.Button(new Rect(x, y, 76f, buttonH), overrideSpawnCharges ? "Override" : "Default"))
            overrideSpawnCharges = !overrideSpawnCharges;

        x += 76f + gap;

        GUI.enabled = overrideSpawnCharges;

        if (GUI.Button(new Rect(x, y, 42f, buttonH), "-10"))
            spawnCharges = Mathf.Clamp(spawnCharges - chargeLargeStep, 0, max);

        x += 42f + gap;

        if (GUI.Button(new Rect(x, y, 34f, buttonH), "-1"))
            spawnCharges = Mathf.Clamp(spawnCharges - chargeSmallStep, 0, max);

        x += 34f + gap;

        if (GUI.Button(new Rect(x, y, 34f, buttonH), "+1"))
            spawnCharges = Mathf.Clamp(spawnCharges + chargeSmallStep, 0, max);

        x += 34f + gap;

        if (GUI.Button(new Rect(x, y, 42f, buttonH), "+10"))
            spawnCharges = Mathf.Clamp(spawnCharges + chargeLargeStep, 0, max);

        x += 42f + gap;

        if (GUI.Button(new Rect(x, y, 42f, buttonH), "Zero"))
            spawnCharges = 0;

        x += 42f + gap;

        if (GUI.Button(new Rect(x, y, 42f, buttonH), "Max"))
            spawnCharges = max;

        GUI.enabled = true;
    }
}