using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class CargoCrate : MonoBehaviour, IInteractable, IInteractPromptProvider, ICargoManifestPayload
{
    [Header("Crate Data")]
    public string itemId;
    public int quantity = 1;

    [Header("Interaction")]
    [SerializeField] private int priority = 10;
    [SerializeField] private float maxUseDistance = 1.8f;

    private Rigidbody2D _rb;

    public int InteractionPriority => priority;
    public bool IsCarried { get; internal set; }

    // Runtime sorting config (set by a single authority: the scene's store)
    private string _groundLayer;
    private int _groundOrder;
    private string _heldLayer;
    private int _heldOrder;
    private bool _hasSortingConfig;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
    }

    public bool CanInteract(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxUseDistance;
    }

    public void Interact(in InteractContext context)
    {
        var carry = context.InteractorGO != null
            ? context.InteractorGO.GetComponentInParent<PlayerCarryController2D>()
            : null;

        if (carry == null) return;
        carry.ToggleCarry(this);
    }

    public string GetPromptVerb(in InteractContext context) => IsCarried ? "Drop" : "Pick Up";
    public Transform GetPromptAnchor() => transform;

    // ===== Sorting (configured by authority) =====

    public void ConfigureSorting(string groundLayer, int groundOrder, string heldLayer, int heldOrder)
    {
        _groundLayer = groundLayer;
        _groundOrder = groundOrder;
        _heldLayer = heldLayer;
        _heldOrder = heldOrder;
        _hasSortingConfig = true;

        // Apply immediately to match current state.
        if (IsCarried) ApplyHeldSorting();
        else ApplyGroundSorting();
    }

    public void ApplyGroundSorting()
    {
        if (!_hasSortingConfig) return;

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            srs[i].sortingLayerName = _groundLayer;
            srs[i].sortingOrder = _groundOrder;
        }
    }

    public void ApplyHeldSorting()
    {
        if (!_hasSortingConfig) return;

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            srs[i].sortingLayerName = _heldLayer;
            srs[i].sortingOrder = _heldOrder;
        }
    }

    public Rigidbody2D GetRigidbody() => _rb;

    // ===== Cargo Manifest Payload =====

    [Serializable]
    private struct Payload
    {
        public string itemId;
        public int quantity;
    }

    public string CapturePayloadJson()
    {
        var p = new Payload { itemId = this.itemId, quantity = Mathf.Max(0, this.quantity) };
        return JsonUtility.ToJson(p);
    }

    public void RestorePayloadJson(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return;
        try
        {
            var p = JsonUtility.FromJson<Payload>(payloadJson);
            itemId = p.itemId;
            quantity = p.quantity;
        }
        catch { }
    }
}