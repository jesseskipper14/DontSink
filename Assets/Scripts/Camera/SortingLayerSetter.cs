using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SortingLayerSetter : MonoBehaviour
{
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    private MeshRenderer _meshRenderer;

    private void Awake()
    {
        CacheRenderer();
    }

    private void Start()
    {
        ApplySorting();
    }

    [ContextMenu("Reapply Sorting Layer")]
    public void ApplySorting()
    {
        CacheRenderer();

        if (_meshRenderer == null)
            return;

        _meshRenderer.sortingLayerName = sortingLayerName;
        _meshRenderer.sortingOrder = sortingOrder;

        Debug.Log(
            $"[SortingLayerSetter:{name}] Applied sorting layer " +
            $"'{sortingLayerName}' order={sortingOrder}.",
            this);
    }

    private void CacheRenderer()
    {
        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();
    }
}
