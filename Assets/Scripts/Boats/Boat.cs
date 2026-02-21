using System;
using System.Collections.Generic;
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

    // ========================
    // Geometry
    // ========================

    [Header("Geometry (Authoritative)")]
    [SerializeField] private float width = 1f;
    [SerializeField] private float height = 1f;
    [SerializeField] private float volume = 1f;

    // ========================
    // Throttle
    // ========================

    [Header("Throttle")]
    [Range(-1f, 1f)]
    public float throttleInput;
    public float throttleForce = 1f;

    // ========================
    // Cargo
    // ========================

    [Header("Cargo")]
    public List<Cargo> CargoItems = new List<Cargo>();

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
    public float MomentOfInertia =>
        Mass * (Width * Width + Height * Height) / 12f;

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

        foreach (var conn in Connections)
        {
            if (conn.A != null && !conn.A.connections.Contains(conn))
                conn.A.connections.Add(conn);

            if (conn.B != null && !conn.B.connections.Contains(conn))
                conn.B.connections.Add(conn);
        }

        massContributions.AddRange(GetComponentsInChildren<IMassContribution>());

        CargoItems.Clear();
        CargoItems.AddRange(GetComponentsInChildren<Cargo>());

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

        EqualizeAllCompartments(dt);

        //foreach (var c in Compartments)
        //{
        //    Debug.Log($"{c.name} has {c.connections.Count} connections");
        //}

        RecomputeMassAndCOM(); // TO DO ONLY RECOMPUTE WHEN MASS CHANGES
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Cargo cargo = collision.gameObject.GetComponent<Cargo>();
        if (cargo != null && !cargo.isAttachedToBoat)
        {
            AttachCargo(cargo);
        }
    }

    // ========================
    // Forces & Physics Helpers
    // ========================

    public void AddForce(Vector2 force)
    {
        rb.AddForce(force, ForceMode2D.Force);
    }

    // Add a torque
    public void AddTorque(float torque)
    {
        rb.AddTorque(torque, ForceMode2D.Force);
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
        float totalMass = baseMass;
        Vector2 weightedWorldSum = baseMass * transform.TransformPoint(baseLocalCenterOfMass);

        foreach (var c in massContributions)
        {
            float m = c.MassContribution;
            if (m <= 0f) continue;

            Vector2 worldCOM = c.WorldCenterOfMass;

            weightedWorldSum += m * worldCOM;

            totalMass += m;
        }

        Vector2 worldCOMFinal = weightedWorldSum / totalMass;

        Vector2 localCOMFinal = Vector2.Scale(transform.InverseTransformPoint(worldCOMFinal), transform.localScale);

        if (float.IsNaN(localCOMFinal.x) || float.IsNaN(localCOMFinal.y)) // protect against unity
        {
            localCOMFinal = Vector2.zero;
        }

        mass = totalMass;
        rb.mass = mass;
        rb.centerOfMass = localCOMFinal;
        rb.inertia = MomentOfInertia;
    }

    public void RegisterMassContribution(IMassContribution c)
    {
        massContributions.Add(c);
    }

    public void UnregisterMassContribution(IMassContribution c)
    {
        massContributions.Remove(c);
    }



    // ========================
    // Compartments
    // ========================

    private void EqualizeAllCompartments(float dt)
    {
        CompartmentNetwork.EqualizeNetwork(Compartments, dt);
    }

    // ========================
    // Cargo
    // ========================

    public void RegisterCargo(Cargo cargo)
    {
        if (!CargoItems.Contains(cargo))
            CargoItems.Add(cargo);
    }

    public void AttachCargo(Cargo cargo)
    {
        cargo.isAttachedToBoat = true;
        cargo.attachedBoat = this;

        cargo.localPositionOnBoat =
            transform.InverseTransformPoint(cargo.transform.position);

        cargo.transform.SetParent(transform);

        var col = cargo.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        RegisterCargo(cargo);

        //Debug.Log($"[Cargo] Attached cargo ({cargo.mass}kg)");
    }

    // ========================
    // Gizmos & Debug
    // ========================

    private void OnDrawGizmos()
    {
        //DrawBoatGizmo();
        DrawConnectionGizmo();
        DrawBoatCOMGizmo();
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
}

