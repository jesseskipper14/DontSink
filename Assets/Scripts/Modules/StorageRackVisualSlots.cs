using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StorageRackVisualSlots : MonoBehaviour, IInstalledModuleLifecycle
{
    [Header("Refs")]
    [SerializeField] private StorageModule storageModule;

    [Tooltip("Used to resolve stored physical cargo snapshots into cargo crate prefab sprites.")]
    [SerializeField] private TradeCargoPrefabCatalog cargoPrefabCatalog;

    [Tooltip("Optional parent containing Slot_00, Slot_01, etc. If empty, anchors are auto-generated at runtime.")]
    [SerializeField] private Transform anchorRoot;

    [Header("Auto Grid")]
    [SerializeField] private bool autoGenerateAnchorsIfMissing = true;

    [Tooltip("Local position of slot 0 if anchors are generated.")]
    [SerializeField] private Vector2 gridOrigin = new Vector2(-0.45f, 0.25f);

    [Tooltip("Spacing between generated visual slots.")]
    [SerializeField] private Vector2 gridSpacing = new Vector2(0.45f, -0.38f);

    [Header("Visuals")]
    [SerializeField] private float visualScale = 0.45f;
    [SerializeField] private int sortingOrderOffset = 5;

    [Tooltip("If true, only ContainerRack storage modules show visuals.")]
    [SerializeField] private bool onlyForContainerRack = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private readonly List<Transform> _anchors = new();
    private readonly List<GameObject> _spawnedVisuals = new();

    private bool _needsRefresh;

    private ItemContainerState _boundItemState;
    private CargoRackState _boundCargoState;

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        CacheRefs();
        Bind();
        RefreshVisuals();
    }

    private void Update()
    {
        if (storageModule == null)
            CacheRefs();

        if (storageModule == null)
            return;

        storageModule.EnsureContainer();

        ItemContainerState currentItemState = storageModule.ContainerState;
        CargoRackState currentCargoState = storageModule.CargoRackState;

        // Save/load restore can replace either state object.
        // If that happens after this visual component already subscribed,
        // rebind and refresh. Unity lifecycle roulette, now with shelves.
        if (!ReferenceEquals(currentItemState, _boundItemState) ||
            !ReferenceEquals(currentCargoState, _boundCargoState))
        {
            Bind();
            RefreshVisuals();
            return;
        }

        if (_needsRefresh)
        {
            _needsRefresh = false;
            RefreshVisuals();
        }
    }

    private void OnDisable()
    {
        Unbind();
        ClearVisuals();
    }

    public void OnInstalled(Hardpoint hardpoint)
    {
        CacheRefs();
        Bind();
        RefreshVisuals();
    }

    public void OnRemoved()
    {
        Unbind();
        ClearVisuals();
    }

    public void RefreshVisuals()
    {
        CacheRefs();
        ClearVisuals();

        if (storageModule == null)
        {
            Log("Refresh skipped: storageModule null.");
            return;
        }

        if (onlyForContainerRack && !storageModule.IsContainerRack)
        {
            Log("Refresh skipped: storage module is not a container rack.");
            return;
        }

        storageModule.EnsureContainer();

        ItemContainerState itemState = storageModule.ContainerState;
        CargoRackState cargoState = storageModule.CargoRackState;

        if (itemState == null && cargoState == null)
        {
            Log("Refresh skipped: both itemState and cargoState are null.");
            return;
        }

        int slotCount = GetVisualSlotCount(itemState, cargoState);
        int columnCount = GetVisualColumnCount(itemState, cargoState);

        EnsureAnchors(slotCount, columnCount);

        for (int i = 0; i < slotCount; i++)
        {
            if (i < 0 || i >= _anchors.Count || _anchors[i] == null)
                continue;

            // Lane 1: portable containers / item instances.
            InventorySlot itemSlot = itemState != null ? itemState.GetSlot(i) : null;
            if (itemSlot != null && !itemSlot.IsEmpty && itemSlot.Instance != null)
            {
                GameObject itemVisual = CreateVisualForItem(itemSlot.Instance, _anchors[i], i);
                if (itemVisual != null)
                    _spawnedVisuals.Add(itemVisual);

                continue;
            }

            // Lane 2: physical cargo crates stored as snapshots.
            CargoRackSlot cargoSlot = cargoState != null ? cargoState.GetSlot(i) : null;
            if (cargoSlot != null && !cargoSlot.IsEmpty && cargoSlot.Crate != null)
            {
                GameObject cargoVisual = CreateVisualForCargo(cargoSlot.Crate, _anchors[i], i);
                if (cargoVisual != null)
                    _spawnedVisuals.Add(cargoVisual);

                continue;
            }
        }

        Log(
            $"Refreshed rack visuals. slots={slotCount}, " +
            $"itemState={(itemState != null ? "yes" : "no")}, " +
            $"cargoState={(cargoState != null ? "yes" : "no")}, " +
            $"visuals={_spawnedVisuals.Count}");
    }

    private void Bind()
    {
        Unbind();

        if (storageModule == null)
            return;

        storageModule.EnsureContainer();

        _boundItemState = storageModule.ContainerState;
        _boundCargoState = storageModule.CargoRackState;

        if (_boundItemState != null)
            _boundItemState.Changed += HandleStorageChanged;

        if (_boundCargoState != null)
            _boundCargoState.Changed += HandleStorageChanged;
    }

    private void Unbind()
    {
        if (_boundItemState != null)
            _boundItemState.Changed -= HandleStorageChanged;

        if (_boundCargoState != null)
            _boundCargoState.Changed -= HandleStorageChanged;

        _boundItemState = null;
        _boundCargoState = null;
    }

    private void HandleStorageChanged()
    {
        _needsRefresh = true;
    }

    private void EnsureAnchors(int desiredSlotCount, int columnCount)
    {
        _anchors.Clear();

        if (anchorRoot == null)
            anchorRoot = transform;

        // First use existing children as anchors.
        if (anchorRoot != null)
        {
            for (int i = 0; i < anchorRoot.childCount; i++)
            {
                Transform child = anchorRoot.GetChild(i);
                if (child != null && child.name.StartsWith("Slot"))
                    _anchors.Add(child);
            }
        }

        if (!autoGenerateAnchorsIfMissing)
            return;

        desiredSlotCount = Mathf.Max(0, desiredSlotCount);
        columnCount = Mathf.Max(1, columnCount);

        for (int i = _anchors.Count; i < desiredSlotCount; i++)
        {
            GameObject anchorGO = new GameObject($"Slot_{i:00}_Auto");
            anchorGO.transform.SetParent(anchorRoot != null ? anchorRoot : transform, false);

            int col = i % columnCount;
            int row = i / columnCount;

            anchorGO.transform.localPosition = new Vector3(
                gridOrigin.x + col * gridSpacing.x,
                gridOrigin.y + row * gridSpacing.y,
                0f);

            anchorGO.transform.localRotation = Quaternion.identity;
            anchorGO.transform.localScale = Vector3.one;

            _anchors.Add(anchorGO.transform);
        }
    }

    private GameObject CreateVisualForItem(ItemInstance item, Transform anchor, int slotIndex)
    {
        if (item == null || item.Definition == null || anchor == null)
            return null;

        Sprite sprite = TryGetWorldSprite(item.Definition);
        if (sprite == null)
            sprite = item.Definition.Icon;

        if (sprite == null)
        {
            Log($"No sprite/icon found for item '{item.Definition.DisplayName}'.");
            return null;
        }

        GameObject visual = CreateSpriteVisual(
            $"RackVisual_Item_{slotIndex:00}_{item.Definition.ItemId}",
            sprite,
            anchor);

        if (visual == null)
            return null;

        // Give visible stored item instances a pickup hitbox.
        // Cargo gets its own interactable later, because it must restore a physical CargoCrate,
        // not enter normal inventory like an item.
        BoxCollider2D col = visual.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        RackStoredItemInteractable interactable = visual.AddComponent<RackStoredItemInteractable>();
        interactable.Initialize(storageModule, slotIndex);

        return visual;
    }

    private GameObject CreateVisualForCargo(CargoCrateStoredSnapshot snapshot, Transform anchor, int slotIndex)
    {
        if (snapshot == null || anchor == null)
            return null;

        Sprite sprite = TryGetCargoSprite(snapshot);
        if (sprite == null)
        {
            Log(
                $"No cargo sprite found for slot={slotIndex}, " +
                $"typeGuid='{snapshot.typeGuid}', itemId='{snapshot.itemId}'. " +
                $"Is Cargo Prefab Catalog assigned? Does the resolved prefab have a SpriteRenderer?");

            return null;
        }

        string itemId = string.IsNullOrWhiteSpace(snapshot.itemId)
            ? "cargo"
            : snapshot.itemId;

        GameObject visual = CreateSpriteVisual(
            $"RackVisual_Cargo_{slotIndex:00}_{itemId}",
            sprite,
            anchor);

        BoxCollider2D col = visual.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        RackStoredCargoInteractable interactable = visual.AddComponent<RackStoredCargoInteractable>();
        interactable.Initialize(storageModule, slotIndex, cargoPrefabCatalog);

        return visual;
    }

    private GameObject CreateSpriteVisual(string objectName, Sprite sprite, Transform anchor)
    {
        if (sprite == null || anchor == null)
            return null;

        GameObject visual = new GameObject(objectName);
        visual.transform.SetParent(anchor, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * visualScale;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        SpriteRenderer rackRenderer = GetComponentInChildren<SpriteRenderer>();
        if (rackRenderer != null)
        {
            sr.sortingLayerID = rackRenderer.sortingLayerID;
            sr.sortingOrder = rackRenderer.sortingOrder + sortingOrderOffset;
        }

        return visual;
    }

    private static Sprite TryGetWorldSprite(ItemDefinition definition)
    {
        if (definition == null || definition.WorldPrefab == null)
            return null;

        SpriteRenderer sr = definition.WorldPrefab.GetComponentInChildren<SpriteRenderer>(true);
        return sr != null ? sr.sprite : null;
    }

    private Sprite TryGetCargoSprite(CargoCrateStoredSnapshot snapshot)
    {
        if (snapshot == null)
            return null;

        if (cargoPrefabCatalog == null)
        {
            Log("TryGetCargoSprite failed: cargoPrefabCatalog is null.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(snapshot.typeGuid))
        {
            Log($"TryGetCargoSprite failed: snapshot typeGuid is empty. itemId='{snapshot.itemId}'.");
            return null;
        }

        GameObject prefab = cargoPrefabCatalog.Resolve(snapshot.typeGuid);
        if (prefab == null)
        {
            Log($"TryGetCargoSprite failed: catalog could not resolve typeGuid='{snapshot.typeGuid}'.");
            return null;
        }

        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
        return sr != null ? sr.sprite : null;
    }

    private static int GetVisualSlotCount(ItemContainerState itemState, CargoRackState cargoState)
    {
        int itemCount = itemState != null ? itemState.SlotCount : 0;
        int cargoCount = cargoState != null ? cargoState.SlotCount : 0;
        return Mathf.Max(itemCount, cargoCount);
    }

    private static int GetVisualColumnCount(ItemContainerState itemState, CargoRackState cargoState)
    {
        if (itemState != null && itemState.ColumnCount > 0)
            return itemState.ColumnCount;

        if (cargoState != null && cargoState.ColumnCount > 0)
            return cargoState.ColumnCount;

        return 1;
    }

    private void ClearVisuals()
    {
        for (int i = _spawnedVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = _spawnedVisuals[i];
            if (visual == null)
                continue;

            if (Application.isPlaying)
                Destroy(visual);
            else
                DestroyImmediate(visual);
        }

        _spawnedVisuals.Clear();
    }

    private void CacheRefs()
    {
        if (storageModule == null)
            storageModule = GetComponent<StorageModule>();

        if (storageModule == null)
            storageModule = GetComponentInParent<StorageModule>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[StorageRackVisualSlots:{name}] {msg}", this);
    }
}