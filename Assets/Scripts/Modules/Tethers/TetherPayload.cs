using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem))]
[RequireComponent(typeof(Rigidbody2D))]
public class TetherPayload : MonoBehaviour
{
    private const string TetherPayloadLayerName = "TetherPayload";

    [Header("Tether")]
    [Tooltip("Point on this physical item where the tether attaches. Falls back to this transform.")]
    [SerializeField] private Transform tetherAnchor;

    [Header("Runtime Debug")]
    [SerializeField] private bool usingTetherPayloadLayer;
    [SerializeField] private int tetherPayloadLayerIndex = -1;
    [SerializeField] private int switchedColliderObjects;

    private WorldItem _worldItem;
    private Rigidbody2D _rb;

    private readonly Dictionary<GameObject, int> _originalColliderLayers =
        new Dictionary<GameObject, int>();

    public Transform TetherAnchor =>
        tetherAnchor != null
            ? tetherAnchor
            : transform;

    public WorldItem WorldItem => _worldItem;
    public Rigidbody2D Rigidbody => _rb;

    public bool UsingTetherPayloadLayer => usingTetherPayloadLayer;
    public int TetherPayloadLayerIndex => tetherPayloadLayerIndex;
    public int SwitchedColliderObjects => switchedColliderObjects;

    protected virtual void Awake()
    {
        CacheRefs();
    }

    protected void CacheRefs()
    {
        if (_worldItem == null)
            _worldItem = GetComponent<WorldItem>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// While tethered, move every Collider2D-bearing GameObject on this payload
    /// onto the dedicated TetherPayload physics layer.
    ///
    /// When disabled, restore each collider object's original layer.
    /// The Physics 2D Layer Collision Matrix is therefore authoritative for
    /// what tethered payloads can collide with.
    /// </summary>
    public bool SetTetherCollisionLayerActive(bool active)
    {
        if (active)
            return ApplyTetherPayloadLayer();

        RestoreOriginalColliderLayers();
        return true;
    }

    private bool ApplyTetherPayloadLayer()
    {
        if (usingTetherPayloadLayer)
            return true;

        int targetLayer =
            LayerMask.NameToLayer(TetherPayloadLayerName);

        if (targetLayer < 0)
        {
            Debug.LogError(
                $"[TetherPayload:{name}] Physics layer '{TetherPayloadLayerName}' does not exist. " +
                "Create it before deploying tether payloads.",
                this);

            return false;
        }

        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>(true);

        _originalColliderLayers.Clear();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider =
                colliders[i];

            if (collider == null)
                continue;

            GameObject colliderObject =
                collider.gameObject;

            if (!_originalColliderLayers.ContainsKey(colliderObject))
            {
                _originalColliderLayers.Add(
                    colliderObject,
                    colliderObject.layer);
            }

            colliderObject.layer =
                targetLayer;
        }

        tetherPayloadLayerIndex =
            targetLayer;

        switchedColliderObjects =
            _originalColliderLayers.Count;

        usingTetherPayloadLayer =
            true;

        return true;
    }

    private void RestoreOriginalColliderLayers()
    {
        if (!usingTetherPayloadLayer)
            return;

        foreach (KeyValuePair<GameObject, int> pair in _originalColliderLayers)
        {
            if (pair.Key != null)
                pair.Key.layer = pair.Value;
        }

        _originalColliderLayers.Clear();

        usingTetherPayloadLayer =
            false;

        tetherPayloadLayerIndex =
            -1;

        switchedColliderObjects =
            0;
    }

    protected virtual void OnDestroy()
    {
        // Mostly defensive. Recall destroys the object anyway, but restoring here
        // keeps this component safe for future detach/cut-loose flows.
        RestoreOriginalColliderLayers();
    }
}
