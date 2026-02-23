using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerBoardingState : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private string hullLayerName = "Hull";

    public bool IsBoarded { get; private set; }
    public Transform CurrentBoatRoot { get; private set; }

    private Rigidbody2D _rb;
    private int _hullLayer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _hullLayer = LayerMask.NameToLayer(hullLayerName);
        if (_hullLayer < 0)
            Debug.LogError($"Layer '{hullLayerName}' not found.");
    }

    public void Board(Transform boatRoot)
    {
        IsBoarded = true;
        CurrentBoatRoot = boatRoot;
        ApplyMask();
    }

    public void Unboard()
    {
        IsBoarded = false;
        CurrentBoatRoot = null;
        ApplyMask();
    }

    private void ApplyMask()
    {
        int mask = _rb.excludeLayers;

        if (IsBoarded)
            mask &= ~(1 << _hullLayer);   // stop excluding hull
        else
            mask |= (1 << _hullLayer);    // exclude hull again

        _rb.excludeLayers = mask;
    }
}
