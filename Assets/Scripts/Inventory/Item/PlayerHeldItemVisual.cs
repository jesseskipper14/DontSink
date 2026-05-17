using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHeldItemVisual : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerEquipment equipment;

    [Tooltip("Where the held item visual should appear. Usually a child transform near the player's hands.")]
    [SerializeField] private Transform handVisualAnchor;

    [Tooltip("Optional sorting source. If assigned, held visual follows this renderer's sorting layer/order.")]
    [SerializeField] private SpriteRenderer sortingSource;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer heldSpriteRenderer;

    [Tooltip("Optional parent object for the held sprite/label. If null, this component will create one.")]
    [SerializeField] private GameObject visualRoot;

    [SerializeField] private Vector3 localPosition = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 localScale = Vector3.one;

    [Header("Sprite Source")]
    [Tooltip("Prefer sprite from ItemDefinition.WorldPrefab over ItemDefinition.Icon when available.")]
    [SerializeField] private bool preferWorldPrefabSprite = true;

    [Header("Sorting / Layer")]
    [SerializeField] private bool syncSortingEveryFrame = true;
    [SerializeField] private int sortingOrderOffset = 5;

    [SerializeField] private bool syncGameObjectLayer = true;
    [SerializeField] private bool applyLayerRecursively = true;

    [Header("Cargo Label")]
    [SerializeField] private bool showCargoLabel = true;
    [SerializeField] private TMP_Text cargoLabel;
    [SerializeField] private Vector3 cargoLabelLocalPosition = new Vector3(0f, 0.22f, -0.01f);
    [SerializeField] private float cargoLabelFontSize = 1.6f;
    [SerializeField] private int cargoLabelMaxCharacters = 14;
    [SerializeField] private int cargoLabelSortingOrderOffset = 2;
    [SerializeField] private Color cargoLabelColor = Color.black;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private ItemInstance _shownItem;
    private Renderer _cargoLabelRenderer;

    private void Reset()
    {
        equipment = GetComponentInParent<PlayerEquipment>();
        handVisualAnchor = transform;
    }

    private void Awake()
    {
        CacheRefs();
        EnsureVisualObjects();
        Refresh();
    }

    private void OnEnable()
    {
        CacheRefs();

        if (equipment != null)
            equipment.EquipmentChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (equipment != null)
            equipment.EquipmentChanged -= Refresh;
    }

    private void LateUpdate()
    {
        if (syncSortingEveryFrame)
            ApplySortingAndLayer();

        ApplyAnchorTransform();
    }

    public void Refresh()
    {
        CacheRefs();
        EnsureVisualObjects();

        ItemInstance handsItem = equipment != null
            ? equipment.Get(BottomBarSlotType.Hands)
            : null;

        _shownItem = handsItem;

        if (handsItem == null || handsItem.Definition == null)
        {
            SetVisible(false);
            Log("Refresh: hands empty.");
            return;
        }

        Sprite sprite = ResolveSprite(handsItem.Definition);
        if (sprite == null)
        {
            SetVisible(false);
            Log($"Refresh: no sprite for held item '{DescribeItem(handsItem)}'.");
            return;
        }

        if (heldSpriteRenderer != null)
            heldSpriteRenderer.sprite = sprite;

        RefreshCargoLabel(handsItem);

        SetVisible(true);
        ApplyAnchorTransform();
        ApplySortingAndLayer();

        Log($"Refresh: showing held item '{DescribeItem(handsItem)}'.");
    }

    private void RefreshCargoLabel(ItemInstance item)
    {
        bool isCargo = showCargoLabel && CargoLabelFormatter.IsCargo(item);

        if (cargoLabel == null)
            return;

        cargoLabel.gameObject.SetActive(isCargo);

        if (!isCargo || item == null || item.Definition == null)
        {
            cargoLabel.text = "";
            return;
        }

        cargoLabel.text = CargoLabelFormatter.Format(item.Definition, cargoLabelMaxCharacters);
        cargoLabel.alignment = TextAlignmentOptions.Center;
        cargoLabel.color = cargoLabelColor;
        cargoLabel.fontSize = cargoLabelFontSize;
        cargoLabel.fontStyle = FontStyles.Bold;
        cargoLabel.textWrappingMode = TextWrappingModes.NoWrap;
        cargoLabel.raycastTarget = false;

        Transform labelTransform = cargoLabel.transform;
        labelTransform.localPosition = cargoLabelLocalPosition;
        labelTransform.localRotation = Quaternion.identity;
        labelTransform.localScale = Vector3.one;

        if (_cargoLabelRenderer == null)
            _cargoLabelRenderer = cargoLabel.GetComponent<Renderer>();
    }

    private Sprite ResolveSprite(ItemDefinition definition)
    {
        if (definition == null)
            return null;

        if (preferWorldPrefabSprite && definition.WorldPrefab != null)
        {
            SpriteRenderer worldSprite =
                definition.WorldPrefab.GetComponentInChildren<SpriteRenderer>(true);

            if (worldSprite != null && worldSprite.sprite != null)
                return worldSprite.sprite;
        }

        if (definition.Icon != null)
            return definition.Icon;

        if (!preferWorldPrefabSprite && definition.WorldPrefab != null)
        {
            SpriteRenderer worldSprite =
                definition.WorldPrefab.GetComponentInChildren<SpriteRenderer>(true);

            if (worldSprite != null && worldSprite.sprite != null)
                return worldSprite.sprite;
        }

        return null;
    }

    private void SetVisible(bool visible)
    {
        if (visualRoot != null)
            visualRoot.SetActive(visible);

        if (heldSpriteRenderer != null)
            heldSpriteRenderer.enabled = visible;

        if (!visible && cargoLabel != null)
        {
            cargoLabel.text = "";
            cargoLabel.gameObject.SetActive(false);
        }
    }

    private void ApplyAnchorTransform()
    {
        if (visualRoot == null)
            return;

        if (handVisualAnchor != null && visualRoot.transform.parent != handVisualAnchor)
            visualRoot.transform.SetParent(handVisualAnchor, false);

        visualRoot.transform.localPosition = localPosition;
        visualRoot.transform.localRotation = Quaternion.Euler(localEulerAngles);
        visualRoot.transform.localScale = localScale;
    }

    private void ApplySortingAndLayer()
    {
        if (sortingSource == null)
            CacheSortingSource();

        if (sortingSource != null)
        {
            if (heldSpriteRenderer != null)
            {
                heldSpriteRenderer.sortingLayerID = sortingSource.sortingLayerID;
                heldSpriteRenderer.sortingOrder = sortingSource.sortingOrder + sortingOrderOffset;
            }

            if (_cargoLabelRenderer != null)
            {
                _cargoLabelRenderer.sortingLayerID = sortingSource.sortingLayerID;
                _cargoLabelRenderer.sortingOrder =
                    sortingSource.sortingOrder + sortingOrderOffset + cargoLabelSortingOrderOffset;
            }

            if (syncGameObjectLayer && visualRoot != null)
            {
                int sourceLayer = sortingSource.gameObject.layer;

                if (applyLayerRecursively)
                    SetLayerRecursively(visualRoot, sourceLayer);
                else
                    visualRoot.layer = sourceLayer;
            }
        }
    }

    private void EnsureVisualObjects()
    {
        if (handVisualAnchor == null)
            handVisualAnchor = transform;

        if (visualRoot == null)
        {
            visualRoot = new GameObject("HeldItemVisual");
            visualRoot.transform.SetParent(handVisualAnchor, false);
        }

        if (heldSpriteRenderer == null)
        {
            Transform existingSprite = visualRoot.transform.Find("HeldSprite");
            GameObject spriteGO;

            if (existingSprite != null)
            {
                spriteGO = existingSprite.gameObject;
            }
            else
            {
                spriteGO = new GameObject("HeldSprite");
                spriteGO.transform.SetParent(visualRoot.transform, false);
            }

            heldSpriteRenderer = spriteGO.GetComponent<SpriteRenderer>();
            if (heldSpriteRenderer == null)
                heldSpriteRenderer = spriteGO.AddComponent<SpriteRenderer>();
        }

        if (cargoLabel == null)
        {
            Transform existingLabel = visualRoot.transform.Find("CargoLabel");
            GameObject labelGO;

            if (existingLabel != null)
            {
                labelGO = existingLabel.gameObject;
            }
            else
            {
                labelGO = new GameObject("CargoLabel");
                labelGO.transform.SetParent(visualRoot.transform, false);
            }

            cargoLabel = labelGO.GetComponent<TMP_Text>();
            if (cargoLabel == null)
                cargoLabel = labelGO.AddComponent<TextMeshPro>();
        }

        if (_cargoLabelRenderer == null && cargoLabel != null)
            _cargoLabelRenderer = cargoLabel.GetComponent<Renderer>();

        ApplyAnchorTransform();
        SetVisible(false);
    }

    private void CacheRefs()
    {
        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>(true);

        if (handVisualAnchor == null)
            handVisualAnchor = transform;

        if (heldSpriteRenderer == null && visualRoot != null)
            heldSpriteRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);

        if (cargoLabel == null && visualRoot != null)
            cargoLabel = visualRoot.GetComponentInChildren<TMP_Text>(true);

        CacheSortingSource();
    }

    private void CacheSortingSource()
    {
        if (sortingSource != null)
            return;

        SpriteRenderer[] renderers = GetComponentsInParent<SpriteRenderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            sortingSource = renderers[0];
            return;
        }

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers != null && renderers.Length > 0)
            sortingSource = renderers[0];
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;

        Transform t = root.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }

    private string DescribeItem(ItemInstance item)
    {
        if (item == null)
            return "empty";

        string itemId = item.Definition != null ? item.Definition.ItemId : "NO_DEF";
        return $"{itemId} x{item.Quantity} inst={item.InstanceId}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[PlayerHeldItemVisual:{name}] {msg}", this);
    }
}