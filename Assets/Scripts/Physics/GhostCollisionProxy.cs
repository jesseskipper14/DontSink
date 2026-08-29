using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generic one-way collision shell for a moving Rigidbody2D container.
///
/// The authoritative body moves normally.
/// This component creates a SEPARATE kinematic Rigidbody2D that mirrors selected
/// structural colliders. Dynamic occupants can collide with the ghost shell
/// without sending collision impulses back into the authoritative body.
///
/// Known culturally as Ghost Boat when used on a Boat.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class GhostCollisionProxy : MonoBehaviour
{
    [Header("Authoritative Body")]
    [Tooltip("The real moving Rigidbody2D. If blank, resolves from this GameObject.")]
    [SerializeField] private Rigidbody2D followBody;

    [Header("Collision Sources")]
    [Tooltip("Only ordinary non-trigger Collider2Ds below these roots that are attached to Follow Body are mirrored. Colliders driven by effectors (for example one-way HatchLedge platforms) are intentionally left real and are not ghosted.")]
    [SerializeField] private Transform[] sourceRoots;

    [Header("Ghost Layer")]
    [Tooltip("Runtime proxy colliders are placed on this Unity physics layer.")]
    [SerializeField] private string ghostLayerName = "GhostCollision";

    [Header("Contact Material")]
    [Tooltip("Optional material for all ghost colliders. If blank, each source collider's material is copied.")]
    [SerializeField] private PhysicsMaterial2D overrideMaterial;

    [Header("Runtime Sync")]
    [Tooltip("Keep source collider enabled state, local transform, and simple geometry synchronized. Leave on for hatches/moving structure.")]
    [SerializeField] private bool syncSourceChanges = true;

    [Tooltip("Keep the generated runtime ghost visible in the Hierarchy for debugging.")]
    [SerializeField] private bool showRuntimeProxyInHierarchy = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private GameObject _runtimeRoot;
    private Rigidbody2D _proxyBody;
    private int _ghostLayer = -1;

    private readonly List<ProxyColliderBinding> _bindings = new();
    private readonly List<Collider2D> _sourceColliders = new();
    private readonly List<Collider2D> _proxyColliders = new();

    private static readonly List<GhostCollisionProxy> _activeProxies = new();

    // Physics2D.IgnoreCollision is not reference-counted and survives ordinary
    // layer-mask changes. Track ONLY the real-source ignore pairs created by this
    // system so they can be restored when a subject changes owner, unboards, or
    // a proxy is disabled/rebuilt.
    private static readonly List<ManagedRealSourceIgnore>
        _managedRealSourceIgnores = new();

    public static event Action ActiveProxySetChanged;

    public Rigidbody2D FollowBody => followBody;
    public Rigidbody2D ProxyBody => _proxyBody;
    public GameObject RuntimeRoot => _runtimeRoot;
    public bool IsBuilt => _runtimeRoot != null && _proxyBody != null && _proxyColliders.Count > 0;

    public IReadOnlyList<Collider2D> SourceColliders => _sourceColliders;
    public IReadOnlyList<Collider2D> ProxyColliders => _proxyColliders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activeProxies.Clear();
        _managedRealSourceIgnores.Clear();
        ActiveProxySetChanged = null;
    }

    private void Reset()
    {
        ResolveFollowBody();
    }

    private void Awake()
    {
        ResolveFollowBody();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (BuildRuntimeProxy())
        {
            RegisterActiveProxy(this);
            ActiveProxySetChanged?.Invoke();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        RestoreManagedRealSourceIgnoresForProxy(
            this);

        bool changed =
            UnregisterActiveProxy(this);

        DestroyRuntimeProxy();

        if (changed)
            ActiveProxySetChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        RestoreManagedRealSourceIgnoresForProxy(
            this);

        bool changed =
            UnregisterActiveProxy(this);

        DestroyRuntimeProxy();

        if (changed)
            ActiveProxySetChanged?.Invoke();
    }

    private void FixedUpdate()
    {
        if (!IsBuilt || followBody == null)
            return;

        SyncProxyBody();

        if (syncSourceChanges)
            SyncBindings();
    }

    [ContextMenu("Rebuild Runtime Ghost Proxy")]
    public void RebuildRuntimeProxy()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                $"[GhostCollisionProxy:{name}] Runtime proxy is built only in Play Mode.",
                this);
            return;
        }

        RestoreManagedRealSourceIgnoresForProxy(
            this);

        bool wasActive =
            UnregisterActiveProxy(this);

        DestroyRuntimeProxy();

        bool rebuilt =
            BuildRuntimeProxy();

        if (rebuilt)
            RegisterActiveProxy(this);

        if (wasActive || rebuilt)
            ActiveProxySetChanged?.Invoke();
    }

    private bool BuildRuntimeProxy()
    {
        ResolveFollowBody();

        if (followBody == null)
        {
            Debug.LogError(
                $"[GhostCollisionProxy:{name}] No authoritative Rigidbody2D assigned/found.",
                this);
            return false;
        }

        _ghostLayer =
            LayerMask.NameToLayer(
                ghostLayerName);

        if (_ghostLayer < 0)
        {
            Debug.LogError(
                $"[GhostCollisionProxy:{name}] Unity layer '{ghostLayerName}' does not exist. " +
                "Create it before entering Play Mode.",
                this);
            return false;
        }

        GatherSourceColliders();

        if (_sourceColliders.Count == 0)
        {
            Debug.LogWarning(
                $"[GhostCollisionProxy:{name}] No usable non-trigger source colliders found. " +
                "Assign structural source roots such as _Hull and _Deck.",
                this);
            return false;
        }

        _runtimeRoot =
            new GameObject(
                $"__GhostCollisionProxy_{name}");

        if (!showRuntimeProxyInHierarchy)
        {
            _runtimeRoot.hideFlags =
                HideFlags.HideInHierarchy;
        }

        if (gameObject.scene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(
                _runtimeRoot,
                gameObject.scene);
        }

        _runtimeRoot.layer =
            _ghostLayer;

        _runtimeRoot.transform.position =
            followBody.position;

        _runtimeRoot.transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                followBody.rotation);

        _runtimeRoot.transform.localScale =
            Vector3.one;

        _proxyBody =
            _runtimeRoot.AddComponent<Rigidbody2D>();

        _proxyBody.bodyType =
            RigidbodyType2D.Kinematic;

        _proxyBody.simulated =
            true;

        _proxyBody.gravityScale =
            0f;

        _proxyBody.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;

        for (int i = 0;
             i < _sourceColliders.Count;
             i++)
        {
            Collider2D source =
                _sourceColliders[i];

            if (source == null)
                continue;

            GameObject child =
                new GameObject(
                    $"Ghost_{source.gameObject.name}_{source.GetType().Name}");

            child.layer =
                _ghostLayer;

            child.transform.SetParent(
                _runtimeRoot.transform,
                false);

            ProxyColliderBinding binding =
                CreateBinding(
                    source,
                    child);

            if (binding == null)
            {
                Destroy(child);
                continue;
            }

            _bindings.Add(
                binding);

            _proxyColliders.Add(
                binding.Proxy);

            SyncBinding(
                binding);
        }

        if (_proxyColliders.Count == 0)
        {
            Debug.LogError(
                $"[GhostCollisionProxy:{name}] Source colliders were found, but none used supported Collider2D types.",
                this);

            DestroyRuntimeProxy();
            return false;
        }

        IgnoreGhostAgainstAuthoritativeBody();
        SyncProxyBody();

        Log(
            $"Built ghost proxy | sourceColliders={_sourceColliders.Count} " +
            $"proxyColliders={_proxyColliders.Count}");

        return true;
    }

    private void DestroyRuntimeProxy()
    {
        _bindings.Clear();
        _sourceColliders.Clear();
        _proxyColliders.Clear();
        _proxyBody = null;

        if (_runtimeRoot == null)
            return;

        GameObject doomed =
            _runtimeRoot;

        _runtimeRoot = null;

        if (Application.isPlaying)
            Destroy(doomed);
        else
            DestroyImmediate(doomed);
    }

    private void ResolveFollowBody()
    {
        if (followBody == null)
            followBody = GetComponent<Rigidbody2D>();
    }

    private void GatherSourceColliders()
    {
        _sourceColliders.Clear();

        if (followBody == null)
            return;

        if (sourceRoots == null ||
            sourceRoots.Length == 0)
        {
            return;
        }

        HashSet<Collider2D> unique =
            new HashSet<Collider2D>();

        for (int r = 0;
             r < sourceRoots.Length;
             r++)
        {
            Transform root =
                sourceRoots[r];

            if (root == null)
                continue;

            Collider2D[] colliders =
                root.GetComponentsInChildren<Collider2D>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider2D candidate =
                    colliders[i];

                if (candidate == null ||
                    candidate.isTrigger)
                {
                    continue;
                }

                // Effector-driven colliders carry semantic collision behavior
                // (one-way platforms, etc.) that a raw cloned Collider2D cannot
                // reproduce safely. Leave those real instead of creating a solid
                // spectral copy. HatchLedge is the first concrete use case.
                if (candidate.usedByEffector)
                {
                    Log(
                        $"Skipping effector-driven source collider " +
                        $"'{candidate.name}' ({candidate.GetType().Name}).");

                    continue;
                }

                // Do not accidentally ghost an independent child Rigidbody2D.
                // Structural colliders without their own body resolve to the
                // authoritative parent Rigidbody2D through attachedRigidbody.
                if (candidate.attachedRigidbody !=
                    followBody)
                {
                    continue;
                }

                if (unique.Add(candidate))
                {
                    _sourceColliders.Add(
                        candidate);
                }
            }
        }
    }

    private void SyncProxyBody()
    {
        if (_proxyBody == null ||
            followBody == null)
        {
            return;
        }

        // Hard pose correction prevents drift.
        _proxyBody.position =
            followBody.position;

        _proxyBody.rotation =
            followBody.rotation;

        // Then let the kinematic proxy move through the upcoming physics step
        // with the real body's current motion rather than sitting one frame behind.
        _proxyBody.linearVelocity =
            followBody.linearVelocity;

        _proxyBody.angularVelocity =
            followBody.angularVelocity;
    }

    private void SyncBindings()
    {
        for (int i = _bindings.Count - 1;
             i >= 0;
             i--)
        {
            ProxyColliderBinding binding =
                _bindings[i];

            if (binding == null ||
                binding.Source == null ||
                binding.Proxy == null ||
                binding.ProxyTransform == null)
            {
                if (binding != null &&
                    binding.ProxyTransform != null)
                {
                    Destroy(
                        binding.ProxyTransform.gameObject);
                }

                _bindings.RemoveAt(i);
                continue;
            }

            SyncBinding(
                binding);
        }
    }

    private void SyncBinding(
        ProxyColliderBinding binding)
    {
        if (binding == null ||
            binding.Source == null ||
            binding.Proxy == null ||
            binding.ProxyTransform == null ||
            followBody == null)
        {
            return;
        }

        Transform bodyTransform =
            followBody.transform;

        Transform sourceTransform =
            binding.Source.transform;

        Quaternion bodyInverse =
            Quaternion.Inverse(
                bodyTransform.rotation);

        Vector3 worldDelta =
            sourceTransform.position -
            bodyTransform.position;

        // Ghost root intentionally has scale 1. Store world-size local geometry
        // beneath it so the proxy matches even if a future vehicle root is scaled.
        binding.ProxyTransform.localPosition =
            bodyInverse *
            worldDelta;

        binding.ProxyTransform.localRotation =
            bodyInverse *
            sourceTransform.rotation;

        Vector3 sourceWorldScale =
            sourceTransform.lossyScale;

        binding.ProxyTransform.localScale =
            new Vector3(
                sourceWorldScale.x,
                sourceWorldScale.y,
                1f);

        binding.Proxy.enabled =
            binding.Source.enabled &&
            binding.Source.gameObject.activeInHierarchy;

        binding.Proxy.sharedMaterial =
            overrideMaterial != null
                ? overrideMaterial
                : binding.Source.sharedMaterial;

        SyncColliderShape(
            binding.Source,
            binding.Proxy);
    }

    private ProxyColliderBinding CreateBinding(
        Collider2D source,
        GameObject targetObject)
    {
        Collider2D proxy = null;

        if (source is BoxCollider2D)
            proxy = targetObject.AddComponent<BoxCollider2D>();
        else if (source is CircleCollider2D)
            proxy = targetObject.AddComponent<CircleCollider2D>();
        else if (source is CapsuleCollider2D)
            proxy = targetObject.AddComponent<CapsuleCollider2D>();
        else if (source is PolygonCollider2D)
            proxy = targetObject.AddComponent<PolygonCollider2D>();
        else if (source is EdgeCollider2D)
            proxy = targetObject.AddComponent<EdgeCollider2D>();
        else
        {
            Debug.LogWarning(
                $"[GhostCollisionProxy:{name}] Unsupported source collider type " +
                $"'{source.GetType().Name}' on '{source.name}'. Skipping it.",
                source);
            return null;
        }

        proxy.isTrigger =
            false;

        return new ProxyColliderBinding
        {
            Source = source,
            Proxy = proxy,
            ProxyTransform = targetObject.transform
        };
    }

    private static void SyncColliderShape(
        Collider2D source,
        Collider2D proxy)
    {
        if (source is BoxCollider2D sourceBox &&
            proxy is BoxCollider2D proxyBox)
        {
            proxyBox.size =
                sourceBox.size;

            proxyBox.offset =
                sourceBox.offset;

            proxyBox.edgeRadius =
                sourceBox.edgeRadius;

            return;
        }

        if (source is CircleCollider2D sourceCircle &&
            proxy is CircleCollider2D proxyCircle)
        {
            proxyCircle.radius =
                sourceCircle.radius;

            proxyCircle.offset =
                sourceCircle.offset;

            return;
        }

        if (source is CapsuleCollider2D sourceCapsule &&
            proxy is CapsuleCollider2D proxyCapsule)
        {
            proxyCapsule.size =
                sourceCapsule.size;

            proxyCapsule.offset =
                sourceCapsule.offset;

            proxyCapsule.direction =
                sourceCapsule.direction;

            return;
        }

        if (source is PolygonCollider2D sourcePolygon &&
            proxy is PolygonCollider2D proxyPolygon)
        {
            proxyPolygon.offset =
                sourcePolygon.offset;

            proxyPolygon.pathCount =
                sourcePolygon.pathCount;

            for (int i = 0;
                 i < sourcePolygon.pathCount;
                 i++)
            {
                proxyPolygon.SetPath(
                    i,
                    sourcePolygon.GetPath(i));
            }

            return;
        }

        if (source is EdgeCollider2D sourceEdge &&
            proxy is EdgeCollider2D proxyEdge)
        {
            proxyEdge.offset =
                sourceEdge.offset;

            proxyEdge.points =
                sourceEdge.points;

            proxyEdge.edgeRadius =
                sourceEdge.edgeRadius;
        }
    }

    private void IgnoreGhostAgainstAuthoritativeBody()
    {
        if (followBody == null ||
            _proxyColliders.Count == 0)
        {
            return;
        }

        Collider2D[] realBodyColliders =
            followBody.GetComponentsInChildren<Collider2D>(
                true);

        for (int g = 0;
             g < _proxyColliders.Count;
             g++)
        {
            Collider2D ghost =
                _proxyColliders[g];

            if (ghost == null)
                continue;

            for (int r = 0;
                 r < realBodyColliders.Length;
                 r++)
            {
                Collider2D real =
                    realBodyColliders[r];

                if (real == null ||
                    real.attachedRigidbody != followBody)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(
                    ghost,
                    real,
                    true);
            }
        }
    }

    /// <summary>
    /// Generic ownership hook.
    ///
    /// The supplied subject colliders collide with exactly one GhostCollisionProxy
    /// and ignore every other active ghost. If allowedProxy is null, they ignore
    /// every ghost.
    ///
    /// For the allowed proxy, the subject also ignores that proxy's real source
    /// containment colliders. This is the actual one-way physics handoff:
    /// real wall OFF, ghost wall ON.
    /// </summary>
    public static void ConfigureExclusiveCollisions(
        IReadOnlyList<Collider2D> subjectColliders,
        GhostCollisionProxy allowedProxy)
    {
        PruneActiveProxies();
        PruneManagedRealSourceIgnores();

        if (subjectColliders == null)
            return;

        // First restore any real-source ignores that THIS system previously
        // installed for these subjects. We then apply the new desired state.
        // This is what makes Ghost -> fallback -> Ghost transitions reversible.
        RestoreManagedRealSourceIgnoresForSubjects(
            subjectColliders);

        for (int p = 0;
             p < _activeProxies.Count;
             p++)
        {
            GhostCollisionProxy proxy =
                _activeProxies[p];

            if (proxy == null ||
                !proxy.IsBuilt)
            {
                continue;
            }

            bool allowGhost =
                ReferenceEquals(
                    proxy,
                    allowedProxy);

            for (int s = 0;
                 s < subjectColliders.Count;
                 s++)
            {
                Collider2D subject =
                    subjectColliders[s];

                if (subject == null ||
                    subject.isTrigger)
                {
                    continue;
                }

                for (int g = 0;
                     g < proxy._proxyColliders.Count;
                     g++)
                {
                    Collider2D ghost =
                        proxy._proxyColliders[g];

                    if (ghost == null)
                        continue;

                    Physics2D.IgnoreCollision(
                        subject,
                        ghost,
                        !allowGhost);
                }

                if (!allowGhost)
                    continue;

                for (int r = 0;
                     r < proxy._sourceColliders.Count;
                     r++)
                {
                    Collider2D realSource =
                        proxy._sourceColliders[r];

                    if (realSource == null)
                        continue;

                    Physics2D.IgnoreCollision(
                        subject,
                        realSource,
                        true);

                    _managedRealSourceIgnores.Add(
                        new ManagedRealSourceIgnore
                        {
                            Proxy = proxy,
                            Subject = subject,
                            RealSource = realSource
                        });
                }
            }
        }
    }

    private static void RestoreManagedRealSourceIgnoresForSubjects(
        IReadOnlyList<Collider2D> subjects)
    {
        if (subjects == null ||
            _managedRealSourceIgnores.Count == 0)
        {
            return;
        }

        for (int i = _managedRealSourceIgnores.Count - 1;
             i >= 0;
             i--)
        {
            ManagedRealSourceIgnore pair =
                _managedRealSourceIgnores[i];

            if (pair == null ||
                pair.Subject == null ||
                pair.RealSource == null)
            {
                _managedRealSourceIgnores.RemoveAt(i);
                continue;
            }

            bool subjectMatches =
                false;

            for (int s = 0;
                 s < subjects.Count;
                 s++)
            {
                if (ReferenceEquals(
                        subjects[s],
                        pair.Subject))
                {
                    subjectMatches = true;
                    break;
                }
            }

            if (!subjectMatches)
                continue;

            Physics2D.IgnoreCollision(
                pair.Subject,
                pair.RealSource,
                false);

            _managedRealSourceIgnores.RemoveAt(i);
        }
    }

    private static void RestoreManagedRealSourceIgnoresForProxy(
        GhostCollisionProxy proxy)
    {
        if (proxy == null ||
            _managedRealSourceIgnores.Count == 0)
        {
            return;
        }

        for (int i = _managedRealSourceIgnores.Count - 1;
             i >= 0;
             i--)
        {
            ManagedRealSourceIgnore pair =
                _managedRealSourceIgnores[i];

            if (pair == null)
            {
                _managedRealSourceIgnores.RemoveAt(i);
                continue;
            }

            if (!ReferenceEquals(
                    pair.Proxy,
                    proxy))
            {
                continue;
            }

            if (pair.Subject != null &&
                pair.RealSource != null)
            {
                Physics2D.IgnoreCollision(
                    pair.Subject,
                    pair.RealSource,
                    false);
            }

            _managedRealSourceIgnores.RemoveAt(i);
        }
    }

    private static void PruneManagedRealSourceIgnores()
    {
        for (int i = _managedRealSourceIgnores.Count - 1;
             i >= 0;
             i--)
        {
            ManagedRealSourceIgnore pair =
                _managedRealSourceIgnores[i];

            if (pair == null ||
                pair.Proxy == null ||
                pair.Subject == null ||
                pair.RealSource == null)
            {
                _managedRealSourceIgnores.RemoveAt(i);
            }
        }
    }

    private static void RegisterActiveProxy(
        GhostCollisionProxy proxy)
    {
        if (proxy == null)
            return;

        PruneActiveProxies();

        if (!_activeProxies.Contains(proxy))
            _activeProxies.Add(proxy);
    }

    private static bool UnregisterActiveProxy(
        GhostCollisionProxy proxy)
    {
        bool removed =
            false;

        for (int i = _activeProxies.Count - 1;
             i >= 0;
             i--)
        {
            GhostCollisionProxy current =
                _activeProxies[i];

            if (current == null ||
                ReferenceEquals(
                    current,
                    proxy))
            {
                _activeProxies.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    private static void PruneActiveProxies()
    {
        for (int i = _activeProxies.Count - 1;
             i >= 0;
             i--)
        {
            if (_activeProxies[i] == null)
                _activeProxies.RemoveAt(i);
        }
    }

    private void Log(
        string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[GhostCollisionProxy:{name}] {message}",
            this);
    }

    private sealed class ManagedRealSourceIgnore
    {
        public GhostCollisionProxy Proxy;
        public Collider2D Subject;
        public Collider2D RealSource;
    }

    private sealed class ProxyColliderBinding
    {
        public Collider2D Source;
        public Collider2D Proxy;
        public Transform ProxyTransform;
    }
}