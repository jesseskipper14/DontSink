using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Boat : MonoBehaviour, IForceBody
{
    // ========================
    // References
    // ========================

    //BoatFlooding flooding;
    [HideInInspector]
    public BuoyancyPolygonForce buoyancyForce;
    private PhysicsGlobals physicsGlobals; // reference to ScriptableObject
    public Rigidbody2D rb { get; private set; }

    // ========================
    // Mass & Inertia
    // ========================

    [Header("Mass")]
    public float baseMass = 1.0f;
    public float mass = 1.0f;
    public List<IMassContribution> massContributions = new List<IMassContribution>();
    [SerializeField]
    private Vector2 baseLocalCenterOfMass = Vector2.zero;

    [Header("Mass Debug")]
    [Tooltip("Draws every active IMassContribution in Scene view so an out-of-bounds COM contributor is immediately visible.")]
    [SerializeField] private bool drawMassContributionGizmos = false;

    [Header("Mystery Oscillation Log")]
    [Tooltip("Logs Boat.AddForce/AddTorque calls plus a once-per-FixedUpdate boat physics summary. Off by default.")]
    [SerializeField] private bool mysteryOscillationLog = false;

    [Tooltip("If true, also logs FixedUpdate summaries even on ticks where Boat.AddForce/AddTorque received nothing.")]
    [SerializeField] private bool mysteryOscillationLogEveryFixedTick = false;

    [Tooltip("Ignore individual force calls below this magnitude. Useful for suppressing tiny buoyancy/noise forces.")]
    [SerializeField, Min(0f)] private float mysteryOscillationMinimumForceMagnitude = 0f;

    [Tooltip("Ignore individual torque calls below this absolute magnitude.")]
    [SerializeField, Min(0f)] private float mysteryOscillationMinimumTorqueMagnitude = 0f;

    private Vector2 _mysteryOscillationAccumulatedForce;
    private float _mysteryOscillationAccumulatedTorque;
    private int _mysteryOscillationForceCallCount;
    private int _mysteryOscillationTorqueCallCount;
    private bool _mysteryOscillationHadLoggedInputThisTick;

    // ========================
    // Geometry
    // ========================

    [Header("Geometry (Authoritative)")]
    [SerializeField] private float width = 1f;
    [SerializeField] private float height = 1f;
    [SerializeField] private float volume = 1f;

    [SerializeField] private Vector2 geometryLocalCenter = Vector2.zero;
    public Vector2 GeometryLocalCenter => geometryLocalCenter;

    // ========================
    // Throttle
    // ========================

    [Header("Throttle")]
    [Range(-1f, 1f)]
    public float throttleInput;
    public float throttleForce = 1f;

    // ========================
    // Compartments
    // ========================

    [Header("Compartments")]
    public List<Compartment> Compartments = new();
    public List<CompartmentConnection> Connections = new List<CompartmentConnection>();

    // ========================
    // Properties
    // ========================

    public Vector2 Position => transform.position;
    public float Width => width;
    public float Height => height;
    public float Volume => volume;
    public float Mass => mass;
    public float MomentOfInertia
    {
        get
        {
            // Width/Height are authoritative BOAT-LOCAL geometry.
            // Rigidbody2D.inertia is a world-physics quantity, so root scale
            // must participate exactly once.
            Vector3 scale = transform.lossyScale;

            float physicalWidth =
                Width *
                Mathf.Abs(scale.x);

            float physicalHeight =
                Height *
                Mathf.Abs(scale.y);

            return
                Mass *
                (physicalWidth * physicalWidth +
                 physicalHeight * physicalHeight) /
                12f;
        }
    }

    [Header("Identity")]
    [SerializeField] private string boatInstanceId; // stable across scenes
    public string BoatInstanceId => boatInstanceId;

    public void SetBoatInstanceId(string id)
    {
        boatInstanceId = id ?? "";
    }

    // ========================
    // Unity Lifecycle
    // ========================

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Boat requires Rigidbody2D");
        }

        // Grab the ScriptableObject from the holder
        if (PhysicsManager.Instance == null)
        {
            Debug.LogError("PhysicsGlobalsHolder is missing in the scene!");
            return;
        }

        physicsGlobals = PhysicsManager.Instance.globals;

        if (physicsGlobals == null)
        {
            Debug.LogError("PhysicsGlobals asset not assigned to the holder!");
        }

        // Find the GenericBuoyancy component on this boat
        buoyancyForce = GetComponent<BuoyancyPolygonForce>();

        if (buoyancyForce != null)
        {
            // Assign the target to this boat (IForceBody)
            buoyancyForce.bodySource = this;

            // Find the authoritative wave in the scene
            WaveField mainWave = FindFirstObjectByType<WaveField>();
            if (mainWave != null)
            {
                buoyancyForce.wave = mainWave;
            }
            else
            {
                Debug.LogError("No WaveField found in scene for boat buoyancy!");
            }
        }
        else
        {
            Debug.LogError("Boat prefab missing GenericBuoyancy component!");
        }

        SanitizeCompartmentsAndConnections();

        IMassContribution[] discoveredMassContributions =
            GetComponentsInChildren<IMassContribution>();

        for (int i = 0;
             i < discoveredMassContributions.Length;
             i++)
        {
            RegisterMassContribution(
                discoveredMassContributions[i]);
        }

        RecomputeMassAndCOM();
    }

    protected virtual void OnEnable()
    {

    }

    protected virtual void Start()
    {

    }

    public void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        BeginMysteryOscillationTick();

        EqualizeAllCompartments(dt);

        //foreach (var c in Compartments)
        //{
        //    Debug.Log($"{c.name} has {c.connections.Count} connections");
        //}

        RecomputeMassAndCOM(); // TO DO ONLY RECOMPUTE WHEN MASS CHANGES

        EndMysteryOscillationTick();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Deprecated old Cargo auto-attach intentionally disabled.
        // Modern dropped items/cargo must enter the boat ownership system through
        // WorldItemDropUtility / BoatOwnedItem / CargoManifest paths.
    }

    // ========================
    // Forces & Physics Helpers
    // ========================

    public void AddForce(Vector2 force)
    {
        if (mysteryOscillationLog)
            LogMysteryOscillationForce(force);

        rb.AddForce(force, ForceMode2D.Force);
    }

    // Add a torque
    public void AddTorque(float torque)
    {
        if (mysteryOscillationLog)
            LogMysteryOscillationTorque(torque);

        rb.AddTorque(torque, ForceMode2D.Force);
    }

    private void BeginMysteryOscillationTick()
    {
        if (!mysteryOscillationLog)
            return;

        _mysteryOscillationAccumulatedForce = Vector2.zero;
        _mysteryOscillationAccumulatedTorque = 0f;
        _mysteryOscillationForceCallCount = 0;
        _mysteryOscillationTorqueCallCount = 0;
        _mysteryOscillationHadLoggedInputThisTick = false;
    }

    private void LogMysteryOscillationForce(Vector2 force)
    {
        float magnitude = force.magnitude;

        _mysteryOscillationAccumulatedForce += force;
        _mysteryOscillationForceCallCount++;

        if (magnitude < mysteryOscillationMinimumForceMagnitude)
            return;

        _mysteryOscillationHadLoggedInputThisTick = true;

        Debug.Log(
            $"[Mystery Oscillation Log:{name}] FORCE " +
            $"tick={Time.frameCount} fixedTime={Time.fixedTime:F4} " +
            $"force=({force.x:F4},{force.y:F4}) mag={magnitude:F4} " +
            $"pos=({rb.position.x:F4},{rb.position.y:F4}) " +
            $"rot={rb.rotation:F3} " +
            $"vel=({rb.linearVelocity.x:F4},{rb.linearVelocity.y:F4}) " +
            $"angVel={rb.angularVelocity:F4} " +
            $"mass={rb.mass:F4} " +
            $"worldCOM=({rb.worldCenterOfMass.x:F4},{rb.worldCenterOfMass.y:F4})",
            this);
    }

    private void LogMysteryOscillationTorque(float torque)
    {
        float magnitude = Mathf.Abs(torque);

        _mysteryOscillationAccumulatedTorque += torque;
        _mysteryOscillationTorqueCallCount++;

        if (magnitude < mysteryOscillationMinimumTorqueMagnitude)
            return;

        _mysteryOscillationHadLoggedInputThisTick = true;

        Debug.Log(
            $"[Mystery Oscillation Log:{name}] TORQUE " +
            $"tick={Time.frameCount} fixedTime={Time.fixedTime:F4} " +
            $"torque={torque:F4} " +
            $"rot={rb.rotation:F3} " +
            $"angVel={rb.angularVelocity:F4} " +
            $"mass={rb.mass:F4} " +
            $"inertia={rb.inertia:F4}",
            this);
    }

    private void EndMysteryOscillationTick()
    {
        if (!mysteryOscillationLog)
            return;

        if (!mysteryOscillationLogEveryFixedTick &&
            !_mysteryOscillationHadLoggedInputThisTick)
        {
            return;
        }

        Vector2 totalForce =
            _mysteryOscillationAccumulatedForce;

        Debug.Log(
            $"[Mystery Oscillation Log:{name}] TICK SUMMARY " +
            $"tick={Time.frameCount} fixedTime={Time.fixedTime:F4} " +
            $"forceCalls={_mysteryOscillationForceCallCount} " +
            $"sumForce=({totalForce.x:F4},{totalForce.y:F4}) " +
            $"sumForceMag={totalForce.magnitude:F4} " +
            $"torqueCalls={_mysteryOscillationTorqueCallCount} " +
            $"sumTorque={_mysteryOscillationAccumulatedTorque:F4} " +
            $"pos=({rb.position.x:F4},{rb.position.y:F4}) " +
            $"rot={rb.rotation:F3} " +
            $"vel=({rb.linearVelocity.x:F4},{rb.linearVelocity.y:F4}) " +
            $"angVel={rb.angularVelocity:F4} " +
            $"mass={rb.mass:F4} " +
            $"localCOM=({rb.centerOfMass.x:F4},{rb.centerOfMass.y:F4}) " +
            $"worldCOM=({rb.worldCenterOfMass.x:F4},{rb.worldCenterOfMass.y:F4}) " +
            $"inertia={rb.inertia:F4}",
            this);
    }

    // ========================
    // Mass & Geometry
    // ========================

    //public void RecomputeMass()
    //{
    //    float compartmentMass = 0f;
    //    float cargoMass = 0f;

    //    foreach (var c in Compartments)
    //        compartmentMass += c.WaterVolume;

    //    foreach (var cargo in CargoItems)
    //        cargoMass += cargo.mass;

    //    mass = baseMass + compartmentMass + cargoMass;
    //    rb.inertia = MomentOfInertia;
    //    rb.mass = mass;
    //}

    public void RecomputeMassAndCOM()
    {
        if (rb == null)
            return;

        float safeBaseMass =
            Mathf.Max(
                0f,
                baseMass);

        float totalMass =
            safeBaseMass;

        Vector2 baseWorldCOM =
            transform.TransformPoint(
                baseLocalCenterOfMass);

        Vector2 weightedWorldSum =
            safeBaseMass *
            baseWorldCOM;

        if (massContributions != null)
        {
            for (int i = 0;
                 i < massContributions.Count;
                 i++)
            {
                IMassContribution contribution =
                    massContributions[i];

                if (!IsLiveMassContribution(
                        contribution))
                {
                    continue;
                }

                float contributionMass =
                    contribution.MassContribution;

                if (contributionMass <= 0f ||
                    float.IsNaN(contributionMass) ||
                    float.IsInfinity(contributionMass))
                {
                    continue;
                }

                Vector2 contributionWorldCOM =
                    contribution.WorldCenterOfMass;

                if (!IsFinite(
                        contributionWorldCOM))
                {
                    continue;
                }

                weightedWorldSum +=
                    contributionMass *
                    contributionWorldCOM;

                totalMass +=
                    contributionMass;
            }
        }

        // A Boat should never realistically have zero mass, but keep the
        // Rigidbody sane even if an author temporarily sets baseMass to zero.
        if (totalMass <= 0.000001f)
        {
            totalMass = 0.000001f;
            weightedWorldSum =
                totalMass *
                baseWorldCOM;
        }

        Vector2 desiredWorldCOM =
            weightedWorldSum /
            totalMass;

        // CRITICAL:
        // InverseTransformPoint already converts world -> local and already
        // accounts for translation, rotation, AND scale.
        //
        // The old code multiplied this result by transform.localScale again,
        // corrupting Rigidbody2D.centerOfMass whenever root scale != (1,1).
        Vector2 localCOMFinal =
            transform.InverseTransformPoint(
                desiredWorldCOM);

        if (!IsFinite(localCOMFinal))
        {
            localCOMFinal =
                baseLocalCenterOfMass;
        }

        mass =
            totalMass;

        rb.mass =
            mass;

        rb.centerOfMass =
            localCOMFinal;

        rb.inertia =
            MomentOfInertia;
    }

    public void RegisterMassContribution(
        IMassContribution c)
    {
        if (!IsLiveMassContribution(c))
            return;

        if (massContributions == null)
        {
            massContributions =
                new List<IMassContribution>();
        }

        if (!massContributions.Contains(c))
        {
            massContributions.Add(c);
        }
    }

    public void UnregisterMassContribution(
        IMassContribution c)
    {
        if (massContributions == null ||
            c == null)
        {
            return;
        }

        // Historical code allowed duplicates. Remove every copy defensively so
        // old runtime/prefab state cannot leave one ghost registration behind.
        while (massContributions.Remove(c))
        {
        }
    }

    private static bool IsLiveMassContribution(
        IMassContribution contribution)
    {
        if (contribution == null)
            return false;

        if (contribution is UnityEngine.Object unityObject &&
            unityObject == null)
        {
            return false;
        }

        return true;
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return
            !float.IsNaN(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) &&
            !float.IsInfinity(value.y);
    }

    public void SetAuthoritativeGeometry(float newWidth, float newHeight, float newVolume)
    {
        SetAuthoritativeGeometry(newWidth, newHeight, newVolume, geometryLocalCenter, false);
    }

    public void SetAuthoritativeGeometry(
        float newWidth,
        float newHeight,
        float newVolume,
        Vector2 newLocalCenter)
    {
        SetAuthoritativeGeometry(newWidth, newHeight, newVolume, newLocalCenter, false);
    }

    public void SetAuthoritativeGeometry(
        float newWidth,
        float newHeight,
        float newVolume,
        Vector2 newLocalCenter,
        bool alsoSetBaseCenterOfMass)
    {
        width = Mathf.Max(0.01f, newWidth);
        height = Mathf.Max(0.01f, newHeight);
        volume = Mathf.Max(0.01f, newVolume);
        geometryLocalCenter = newLocalCenter;

        if (alsoSetBaseCenterOfMass)
            baseLocalCenterOfMass = newLocalCenter;

        if (rb != null)
        {
            rb.inertia = MomentOfInertia;
            RecomputeMassAndCOM();
        }
    }

    public void SetBaseLocalCenterOfMass(Vector2 newLocalCenterOfMass)
    {
        baseLocalCenterOfMass = newLocalCenterOfMass;

        if (rb != null)
        {
            RecomputeMassAndCOM();
        }
    }



    // ========================
    // Compartments
    // ========================

    private void EqualizeAllCompartments(float dt)
    {
        CompartmentNetwork.EqualizeNetwork(Compartments, dt);
    }

    // ========================
    // Gizmos & Debug
    // ========================

    private void OnDrawGizmos()
    {
        //DrawBoatGizmo();
        DrawConnectionGizmo();
        DrawBoatCOMGizmo();

        if (drawMassContributionGizmos)
            DrawMassContributionGizmos();
    }

    private void DrawMassContributionGizmos()
    {
        if (massContributions == null)
            return;

        Vector2 baseWorld =
            transform.TransformPoint(
                baseLocalCenterOfMass);

        Gizmos.color =
            Color.cyan;

        Gizmos.DrawWireSphere(
            baseWorld,
            0.10f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            baseWorld + Vector2.up * 0.12f,
            $"BASE MASS\n{Mathf.Max(0f, baseMass):F2}");
#endif

        for (int i = 0;
             i < massContributions.Count;
             i++)
        {
            IMassContribution contribution =
                massContributions[i];

            if (!IsLiveMassContribution(
                    contribution))
            {
                continue;
            }

            float contributionMass =
                contribution.MassContribution;

            if (contributionMass <= 0f ||
                float.IsNaN(contributionMass) ||
                float.IsInfinity(contributionMass))
            {
                continue;
            }

            Vector2 contributionWorldCOM =
                contribution.WorldCenterOfMass;

            if (!IsFinite(
                    contributionWorldCOM))
            {
                continue;
            }

            Gizmos.color =
                Color.yellow;

            Gizmos.DrawSphere(
                contributionWorldCOM,
                0.075f);

#if UNITY_EDITOR
            string contributorName =
                contribution is Component component
                    ? $"{component.name} [{component.GetType().Name}]"
                    : contribution.GetType().Name;

            UnityEditor.Handles.Label(
                contributionWorldCOM +
                Vector2.up * 0.10f,
                $"{contributorName}\nMass {contributionMass:F2}");
#endif
        }
    }

    private void DrawBoatGizmo()
    {
        Gizmos.color = Color.white;

        Vector2[] localHull =
        {
            new Vector2(-1.5f, -0.5f),
            new Vector2( 1.5f, -0.5f),
            new Vector2( 1.0f,  0.5f),
            new Vector2(-1.0f,  0.5f)
        };

        for (int i = 0; i < localHull.Length; i++)
        {
            Vector2 a = LocalToWorld(localHull[i]);
            Vector2 b = LocalToWorld(localHull[(i + 1) % localHull.Length]);
            Gizmos.DrawLine(a, b);
        }
    }

    private void DrawBoatCOMGizmo()
    {
        if (rb == null) return;

        // World-space center of mass
        Vector2 worldCOM = rb.worldCenterOfMass;

        // Boat origin
        Vector2 boatOrigin = transform.position;

        // Draw COM point
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(worldCOM, 0.08f);

        // Draw line from boat origin to COM
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(boatOrigin, worldCOM);

        // Draw gravity direction at COM
        Gizmos.color = Color.red;
        Vector2 gravityDir = Physics2D.gravity.normalized;
        Gizmos.DrawLine(
            worldCOM,
            worldCOM + gravityDir * 0.5f
        );

        // Label (Scene view only)
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            worldCOM + Vector2.up * 0.1f,
            $"COM\n{rb.centerOfMass}"
        );
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-fit Geometry From Structure")]
    private void EditorAutoFitGeometryFromStructure()
    {
        if (!TryComputeEditorLocalBounds(transform, out Bounds localBounds))
        {
            Debug.LogWarning("[Boat] Could not auto-fit geometry. No usable child renderers/colliders found.", this);
            return;
        }

        float newWidth = Mathf.Max(0.01f, localBounds.size.x);
        float newHeight = Mathf.Max(0.01f, localBounds.size.y);
        float newVolume = Mathf.Max(0.01f, newWidth * newHeight);

        UnityEditor.Undo.RecordObject(this, "Auto-fit Boat Geometry");

        SetAuthoritativeGeometry(
            newWidth,
            newHeight,
            newVolume,
            localBounds.center,
            alsoSetBaseCenterOfMass: true);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log(
            $"[Boat] Auto-fit geometry from structure. width={newWidth:F2}, height={newHeight:F2}, volume={newVolume:F2}, boundsCenter={localBounds.center}",
            this);
    }

    [ContextMenu("Sanitize Compartments And Connections")]
    private void EditorSanitizeCompartmentsAndConnections()
    {
        UnityEditor.Undo.RecordObject(this, "Sanitize Compartments And Connections");
        SanitizeCompartmentsAndConnections();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log(
            $"[Boat] Sanitized Compartments/Connections. Compartments={Compartments.Count}, Connections={Connections.Count}",
            this);
    }

    private static bool TryComputeEditorLocalBounds(Transform boatRoot, out Bounds localBounds)
    {
        localBounds = default;

        bool hasAny = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

        Collider2D[] colliders = boatRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in colliders)
        {
            if (col == null)
                continue;

            if (col.isTrigger)
                continue;

            EncapsulateEditorWorldBoundsAsLocalAabb(boatRoot, col.bounds, ref min, ref max, ref hasAny);
        }

        if (!hasAny)
        {
            Renderer[] renderers = boatRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;

                if (r.GetComponent<CompartmentWaterRenderer>() != null)
                    continue;

                EncapsulateEditorWorldBoundsAsLocalAabb(boatRoot, r.bounds, ref min, ref max, ref hasAny);
            }
        }

        if (!hasAny)
            return false;

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        localBounds = new Bounds(center, size);
        return true;
    }

    private static void EncapsulateEditorWorldBoundsAsLocalAabb(
        Transform root,
        Bounds worldBounds,
        ref Vector3 min,
        ref Vector3 max,
        ref bool hasAny)
    {
        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        Vector3[] corners =
        {
        new Vector3(c.x - e.x, c.y - e.y, c.z),
        new Vector3(c.x - e.x, c.y + e.y, c.z),
        new Vector3(c.x + e.x, c.y - e.y, c.z),
        new Vector3(c.x + e.x, c.y + e.y, c.z),
    };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = root.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
            hasAny = true;
        }
    }
#endif

#if UNITY_EDITOR
    [ContextMenu("Auto-fit Geometry From Visual Renderers")]
    public void EditorAutoFitGeometryFromVisualRenderers()
    {
        if (!TryComputeEditorVisualRendererBounds(transform, out Bounds localBounds))
        {
            Debug.LogWarning("[Boat] Could not auto-fit geometry from visual renderers. No usable renderers found.", this);
            return;
        }

        float newWidth = Mathf.Max(0.01f, localBounds.size.x);
        float newHeight = Mathf.Max(0.01f, localBounds.size.y);
        float newVolume = Mathf.Max(0.01f, newWidth * newHeight);

        UnityEditor.Undo.RecordObject(this, "Auto-fit Boat Geometry From Visual Renderers");

        SetAuthoritativeGeometry(
            newWidth,
            newHeight,
            newVolume,
            localBounds.center,
            alsoSetBaseCenterOfMass: true);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

        Debug.Log(
            $"[Boat] Auto-fit geometry from visual renderers. " +
            $"width={newWidth:F2}, height={newHeight:F2}, volume={newVolume:F2}, center={localBounds.center}",
            this);
    }

    private static bool TryComputeEditorVisualRendererBounds(Transform boatRoot, out Bounds localBounds)
    {
        localBounds = default;

        bool hasAny = false;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

        Renderer[] renderers = boatRoot.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            if (ShouldIgnoreRendererForBoatGeometry(r))
                continue;

            EncapsulateEditorWorldBoundsAsLocalAabb(
                boatRoot,
                r.bounds,
                ref min,
                ref max,
                ref hasAny);
        }

        if (!hasAny)
            return false;

        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    private static bool ShouldIgnoreRendererForBoatGeometry(Renderer r)
    {
        if (r == null)
            return true;

        // Water/flood rendering should not define the hull.
        if (r.GetComponent<CompartmentWaterRenderer>() != null)
            return true;

        string n = r.gameObject.name.ToLowerInvariant();

        // Expand this list as needed when some decorative/debug thing pollutes bounds.
        if (n.Contains("water"))
            return true;

        if (n.Contains("debug"))
            return true;

        if (n.Contains("prompt"))
            return true;

        if (n.Contains("preview"))
            return true;

        if (n.Contains("gizmo"))
            return true;

        return false;
    }
#endif

    private void DrawConnectionGizmo()
    {
        if (Connections == null)
            return;

        foreach (var conn in Connections)
        {
            if (conn != null)
                conn.DrawGizmos();
        }
    }

    public Vector2 LocalToWorld(Vector2 local)
    {
        float rad = transform.eulerAngles.z * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);

        Vector2 rotated = new Vector2(
            local.x * c - local.y * s,
            local.x * s + local.y * c
        );

        return (Vector2)transform.position + rotated;
    }

    private void SanitizeCompartmentsAndConnections()
    {
        if (Compartments == null)
            Compartments = new List<Compartment>();
        else
            Compartments = Compartments
                .Where(c => c != null)
                .Distinct()
                .ToList();

        if (Connections == null)
            Connections = new List<CompartmentConnection>();
        else
            Connections = Connections
                .Where(c => c != null)
                .Distinct()
                .ToList();

        // Rebuild per-compartment connection backrefs from scratch.
        for (int i = 0; i < Compartments.Count; i++)
        {
            Compartment c = Compartments[i];
            if (c != null)
                c.connections.Clear();
        }

        for (int i = 0; i < Connections.Count; i++)
        {
            CompartmentConnection conn = Connections[i];
            if (conn == null)
                continue;

            if (conn.A != null && !conn.A.connections.Contains(conn))
                conn.A.connections.Add(conn);

            if (conn.B != null && !conn.B.connections.Contains(conn))
                conn.B.connections.Add(conn);
        }
    }
}

