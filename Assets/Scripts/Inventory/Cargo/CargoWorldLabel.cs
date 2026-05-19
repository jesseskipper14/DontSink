using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CargoWorldLabel : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldItem worldItem;
    [SerializeField] private TMP_Text label;

    [Tooltip("Sprite renderer whose sorting layer/order the text should follow. Usually the crate sprite.")]
    [SerializeField] private SpriteRenderer sortingSource;

    [Header("Formatting")]
    [SerializeField] private int maxCharacters = 14;

    [Header("Sorting")]
    [SerializeField] private bool syncSortingEveryFrame = true;
    [SerializeField] private int sortingOrderOffset = 2;

    [Header("GameObject Layer")]
    [SerializeField] private bool syncGameObjectLayer = true;
    [Tooltip("If true, applies the source layer to the label object and all children.")]
    [SerializeField] private bool applyLayerRecursively = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Renderer _labelRenderer;

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        if (syncSortingEveryFrame)
            ApplySorting();
    }

    public void Refresh()
    {
        CacheRefs();

        if (label == null)
        {
            Log("Refresh skipped: label null.");
            return;
        }

        if (worldItem == null || worldItem.Instance == null || worldItem.Instance.Definition == null)
        {
            label.text = "";
            ApplySorting();
            return;
        }

        label.text = CargoLabelFormatter.Format(worldItem.Instance.Definition, maxCharacters);
        ApplySorting();
    }

    public void ApplySorting()
    {
        CacheRefs();

        if (_labelRenderer != null && sortingSource != null)
        {
            _labelRenderer.sortingLayerID = sortingSource.sortingLayerID;
            _labelRenderer.sortingOrder = sortingSource.sortingOrder + sortingOrderOffset;
        }

        if (syncGameObjectLayer && sortingSource != null)
        {
            int sourceLayer = sortingSource.gameObject.layer;

            if (applyLayerRecursively)
                SetLayerRecursively(gameObject, sourceLayer);
            else
                gameObject.layer = sourceLayer;
        }
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

    private void CacheRefs()
    {
        if (worldItem == null)
            worldItem = GetComponentInParent<WorldItem>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        if (_labelRenderer == null && label != null)
            _labelRenderer = label.GetComponent<Renderer>();

        if (sortingSource == null)
        {
            SpriteRenderer[] renderers = GetComponentsInParent<SpriteRenderer>(true);

            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (renderers != null && renderers.Length > 0)
                sortingSource = renderers[0];
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[CargoWorldLabel:{name}] {msg}", this);
    }
}