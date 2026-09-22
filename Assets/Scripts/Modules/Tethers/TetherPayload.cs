using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem))]
[RequireComponent(typeof(Rigidbody2D))]
public class TetherPayload : MonoBehaviour
{
    private const string TetherPayloadLayerName = "TetherPayload";

    [Header("Gameplay Authority")]
    [Tooltip(
        "Tether payload physics are shared/host-authoritative. On non-authoritative peers, " +
        "the payload Rigidbody remains simulated for queries/colliders but is forced Kinematic " +
        "so local gravity/solver forces cannot create a second independent payload simulation.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Tether")]
    [Tooltip("Point on this physical item where the tether attaches. Falls back to this transform.")]
    [SerializeField] private Transform tetherAnchor;

    [Header("Collision Scope")]
    [Tooltip(
        "Optional selective collision scope. If present, ONLY colliders authored in " +
        "that scope are moved onto the TetherPayload layer while deployed. " +
        "If absent, legacy behavior remains: every child Collider2D is switched.")]
    [SerializeField] private TetherPayloadCollisionScope collisionScope;

    [Header("Runtime Debug")]
    [SerializeField] private bool usingTetherPayloadLayer;
    [SerializeField] private int tetherPayloadLayerIndex = -1;
    [SerializeField] private int switchedColliderObjects;

    private WorldItem _worldItem;
    private Rigidbody2D _rb;
    private ForceSystem _forceSystem;
    private TetherPayloadDock _activeDock;

    private bool _authorityBodySuppressed;
    private RigidbodyType2D _bodyTypeBeforeAuthoritySuppression = RigidbodyType2D.Dynamic;

    private readonly Dictionary<GameObject, int> _originalColliderLayers =
        new Dictionary<GameObject, int>();

    public Transform TetherAnchor =>
        tetherAnchor != null
            ? tetherAnchor
            : transform;

    public WorldItem WorldItem => _worldItem;
    public Rigidbody2D Rigidbody => _rb;
    public TetherPayloadDock ActiveDock => _activeDock;

    public bool UsingTetherPayloadLayer => usingTetherPayloadLayer;
    public int TetherPayloadLayerIndex => tetherPayloadLayerIndex;
    public int SwitchedColliderObjects => switchedColliderObjects;
    public bool HasGameplayAuthority =>
        GameplayAuthority.CanRun(gameplayAuthorityMode);


    /// <summary>
    /// Dock ownership is payload-local, never global. A payload may be claimed by
    /// only one dock at a time, which keeps simultaneous multiplayer/boat docking
    /// operations independent.
    /// </summary>
    internal void SetActiveDock(TetherPayloadDock dock)
    {
        _activeDock = dock;
    }

    internal void ClearActiveDock(TetherPayloadDock dock)
    {
        if (ReferenceEquals(_activeDock, dock))
            _activeDock = null;
    }

    protected virtual void Awake()
    {
        CacheRefs();
        ConfigureForceSystemAuthorityGate();
        RefreshGameplayAuthorityBodyState();
    }

    protected virtual void FixedUpdate()
    {
        RefreshGameplayAuthorityBodyState();
    }

    protected void CacheRefs()
    {
        if (_worldItem == null)
            _worldItem = GetComponent<WorldItem>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_forceSystem == null)
            _forceSystem = GetComponent<ForceSystem>();

        if (collisionScope == null)
        {
            collisionScope =
                GetComponent<TetherPayloadCollisionScope>() ??
                GetComponentInChildren<TetherPayloadCollisionScope>(true);
        }
    }

    /// <summary>
    /// While tethered, move the payload's EXTERNAL tether collision geometry
    /// onto the dedicated TetherPayload physics layer.
    ///
    /// Backward compatibility:
    /// - no TetherPayloadCollisionScope = legacy "all child colliders" behavior;
    /// - scope present = ONLY colliders explicitly selected by that scope.
    ///
    /// When disabled, restore each switched collider object's original layer.
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

        CacheRefs();

        List<Collider2D> colliders =
            new List<Collider2D>();

        if (collisionScope != null)
        {
            collisionScope.CollectTetherCollisionColliders(
                colliders);

            if (colliders.Count == 0)
            {
                Debug.LogError(
                    $"[TetherPayload:{name}] Selective TetherPayloadCollisionScope is present " +
                    "but contains no usable colliders. Refusing to fall back to all child colliders.",
                    this);

                return false;
            }
        }
        else
        {
            Collider2D[] legacyColliders =
                GetComponentsInChildren<Collider2D>(true);

            if (legacyColliders != null)
                colliders.AddRange(legacyColliders);
        }

        _originalColliderLayers.Clear();

        for (int i = 0; i < colliders.Count; i++)
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

    private void ConfigureForceSystemAuthorityGate()
    {
        if (_forceSystem == null)
            return;

        _forceSystem.ConfigureGameplayAuthorityGate(
            true,
            gameplayAuthorityMode);
    }

    private void RefreshGameplayAuthorityBodyState()
    {
        CacheRefs();

        if (_rb == null)
            return;

        if (!HasGameplayAuthority)
        {
            if (!_authorityBodySuppressed)
            {
                _bodyTypeBeforeAuthoritySuppression =
                    _rb.bodyType;

                _authorityBodySuppressed =
                    true;
            }

            if (_rb.bodyType != RigidbodyType2D.Kinematic)
                _rb.bodyType = RigidbodyType2D.Kinematic;

            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            return;
        }

        if (!_authorityBodySuppressed)
            return;

        _rb.bodyType =
            _bodyTypeBeforeAuthoritySuppression;

        _authorityBodySuppressed =
            false;

        if (_rb.simulated)
            _rb.WakeUp();
    }

    protected virtual void OnDestroy()
    {
        // Mostly defensive. Recall destroys the object anyway, but restoring here
        // keeps this component safe for future detach/cut-loose flows.
        RestoreOriginalColliderLayers();
    }
}
