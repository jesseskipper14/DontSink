///*
// * summary of Boat.cs:

//Physics state: Tracks position, rotation (radians), velocity, and angularVelocity.

//Mass & hull: Base mass, optional internal water mass, total mass, and moment of inertia. Hull defined by hullWidth, hullHeight, and hullVolume (computed as width × height).

//Throttle & trim: Forward/reverse engine (throttleInput) and boat tilt (trimInput) control linear and angular motion.

//Drag: Simple linear drag applied to velocity.

//Visualization: Draws trapezoid in Scene via Gizmos and adds a SpriteRenderer for testing.

//Physics update (FixedUpdate):

//Applies gravity, throttle, drag.

//Updates rotation using trim input.

//Does not update transform.position (screen-stable).

//Helpers: LocalToWorld for gizmo drawing; ComputeHullVolume and RecomputeMass manage hull properties.

//Essentially: 2D boat physics with simplified linear and angular motion, hull volume for buoyancy, and visual debug support.
// */

//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.SocialPlatforms;

//public class Boat_Backup : MonoBehaviour, IForceBody
//{
//    // ========================
//    // References
//    // ========================

//    BoatBuoyancy buoyancy;
//    BoatFlooding flooding;
//    [HideInInspector]
//    public GenericBuoyancy gBuoyancy;

//    // ========================
//    // Physics State
//    // ========================

//    [Header("Physics State")]
//    public float rotation;          // Radians

//    public Vector2 velocity;        // Linear velocity
//    public float angularVelocity;   // Rotational velocity

//    [HideInInspector]
//    public float smoothedRotation;

//    // ========================
//    // Force Accumulation (per FixedUpdate)
//    // ========================

//    [Header("Accumulated Forces")]
//    public float accumulatedVerticalForce;
//    public float accumulatedTorque;

//    // ========================
//    // Mass & Inertia
//    // ========================

//    [Header("Mass")]
//    public float baseMass = 1.0f;
//    public float mass = 1.0f;
//    public float momentOfInertia = 10f;

//    // ========================
//    // Hull
//    // ========================

//    [Header("Hull")]
//    public float hullHeight = 3.0f;
//    public float hullWidth = 10.0f;

//    [HideInInspector]
//    public float hullVolume;

//    // ========================
//    // Throttle
//    // ========================

//    [Header("Throttle")]
//    [Range(-1f, 1f)]
//    public float throttleInput;
//    public float throttleForce = 1f;

//    // ========================
//    // Trim / Weight Shift
//    // ========================

//    [Header("Trim")]
//    [Range(-1f, 1f)]
//    public float trimInput;

//    public float maxTrimAngle = 15f * Mathf.Deg2Rad;
//    public float trimSpeed = 2f;

//    // ========================
//    // Cargo
//    // ========================

//    [Header("Cargo")]
//    public List<Cargo> CargoItems = new List<Cargo>();

//    // ========================
//    // Compartments
//    // ========================

//    public Compartment[] Compartments
//    {
//        get { return GetComponentsInChildren<Compartment>(); }
//    }

//    // ========================
//    // Properties
//    // ========================

//    public float width => hullWidth;
//    public float height => hullHeight;
//    public float volume => hullVolume;
//    public bool debugBuoyancy => buoyancy?.debugBuoyancy ?? false;
//    public float Mass => mass;
//    public Vector2 Position => transform.position;

//    // ========================
//    // Debug
//    // ========================


//    // ========================
//    // Unity Lifecycle
//    // ========================

//    protected virtual void Awake()
//    {
//        // Find the GenericBuoyancy component on this boat
//        gBuoyancy = GetComponent<GenericBuoyancy>();

//        if (gBuoyancy != null)
//        {
//            // Assign the target to this boat (IForceBody)
//            gBuoyancy.bodySource = this;

//            // Find the authoritative wave in the scene
//            WaveField mainWave = FindFirstObjectByType<WaveField>();
//            if (mainWave != null)
//            {
//                gBuoyancy.wave = mainWave;
//            }
//            else
//            {
//                Debug.LogError("No WaveField found in scene for boat buoyancy!");
//            }
//        }
//        else
//        {
//            Debug.LogError("Boat prefab missing GenericBuoyancy component!");
//        }

//        buoyancy = GetComponent<BoatBuoyancy>();
//        flooding = GetComponent<BoatFlooding>();

//        rotation = transform.eulerAngles.z * Mathf.Deg2Rad;

//        velocity = Vector2.zero;
//        angularVelocity = 0f;

//        CargoItems.Clear();
//        CargoItems.AddRange(GetComponentsInChildren<Cargo>());

//        ComputeHullVolume();
//        RecomputeMass();
//    }

//    protected virtual void OnEnable()
//    {
//        //// Temporary visual representation
//        //var sr = gameObject.AddComponent<SpriteRenderer>();
//        //sr.sprite = Sprite.Create(
//        //    Texture2D.whiteTexture,
//        //    new Rect(0, 0, 1, 1),
//        //    new Vector2(0.5f, 0.5f)
//        //);

//        //sr.color = Color.red;
//        //sr.drawMode = SpriteDrawMode.Sliced;
//        //sr.size = new Vector2(hullWidth, hullHeight);
//    }

//    protected virtual void Start()
//    {

//    }

//    protected virtual void FixedUpdate()
//    {
//        if (float.IsNaN(velocity.y) || float.IsInfinity(velocity.y))
//        {
//            Debug.LogError("Velocity corrupted — resetting");
//            velocity.y = 0f;
//        }

//        float dt = Time.fixedDeltaTime;
//        ResetForces();

//        // --- Integrate position (WORLD SPACE) ---
//        transform.position += (Vector3)(velocity * dt);

//        // --- Integrate forces ---
//        ApplyForces(dt);

//        // --- Integrate rotation ---
//        rotation += angularVelocity * dt;

//        // --- Push to transform (screen-stable boat) ---
//        transform.rotation = Quaternion.Euler(
//            0f,
//            0f,
//            rotation * Mathf.Rad2Deg
//        );

//        // --- Post-step maintenance ---
//        RecomputeMass();
//        EqualizeAllCompartments(dt);
//    }

//    private void Update()
//    {

//    }

//    private void OnDrawGizmos()
//    {
//        DrawBoatGizmo();
//    }

//    private void OnCollisionEnter2D(Collision2D collision)
//    {
//        Cargo cargo = collision.gameObject.GetComponent<Cargo>();
//        if (cargo != null && !cargo.isAttachedToBoat)
//        {
//            AttachCargo(cargo);
//        }
//    }

//    // ========================
//    // Forces & Physics Helpers
//    // ========================

//    public void ResetForces()
//    {
//        accumulatedVerticalForce = 0f;
//        accumulatedTorque = 0f;
//    }

//    // Add a force (accumulates for FixedUpdate)
//    public void AddForce(Vector2 force)
//    {
//        // Only vertical for now, but could be generalized
//        accumulatedVerticalForce += force.y;
//    }

//    // Add a torque (accumulates for FixedUpdate)
//    public void AddTorque(float torque)
//    {
//        accumulatedTorque += torque;
//    }

//    // Return submerged fraction (for drag / other effects)
//    public float GetSubmergedFraction()
//    {
//        return buoyancy?.ComputeSubmergedFraction() ?? 0f;
//    }

//    public void AddGravity()
//    {
//        // Base hull gravity (centered)
//        accumulatedVerticalForce -= baseMass * PhysicsGlobal.Gravity;

//        // Compartment gravity (with torque)
//        foreach (var c in Compartments)
//        {
//            if (c.Mass <= 0f)
//                continue;

//            float force = c.Mass * PhysicsGlobal.Gravity;
//            float localX = c.transform.localPosition.x;

//            accumulatedVerticalForce -= force;
//            accumulatedTorque -= force * localX;
//        }

//        // Cargo gravity (already correct)
//        foreach (var cargo in CargoItems)
//        {
//            float force = cargo.mass * PhysicsGlobal.Gravity;
//            float localX = cargo.transform.localPosition.x;

//            accumulatedVerticalForce -= force;
//            accumulatedTorque -= force * localX;
//        }

//    }

//    public void AddDrag(float dt)
//    {
//        float submergedFraction = 0f;

//        // Determine how much of the boat is underwater
//        if (buoyancy != null)
//        {
//            submergedFraction = buoyancy.ComputeSubmergedFraction();
//        }

//        if (submergedFraction <= 0f)
//            return; // Boat is fully out of water → no drag

//        // Quadratic vertical drag (scaled by submerged fraction)
//        float verticalDrag =
//            -Mathf.Sign(velocity.y) *
//            PhysicsGlobal.WaterVerticalDrag *
//            velocity.y *
//            velocity.y / mass;

//        velocity.y += verticalDrag * dt * submergedFraction;

//        // Linear horizontal damping (scaled by submerged fraction)
//        velocity *= 1f - PhysicsGlobal.WaterHorizontalDrag * dt * submergedFraction;
//    }

//    public void ApplyForces(float dt)
//    {
//        // --- Buoyancy ---
//        if (buoyancy != null)
//            buoyancy.ApplyBuoyancy(dt);

//        if (flooding != null)
//            flooding.ApplyFlooding(dt);

//        // --- Torque ---
//        //TrimTorque(dt);

//        // --- Gravity ---
//        AddGravity();

//        // --- Drag --- 
//        AddDrag(dt);

//        // --- Righting Torque ---
//        AddRightingTorque();

//        // Actual effects on the boat
//        // --- Throttle ---
//        AddThrottle(dt);
//        velocity.y += (accumulatedVerticalForce / mass) * dt;
//        // Angular acceleration
//        angularVelocity += ((accumulatedTorque) / momentOfInertia) * dt;
//        // Angular damping
//        angularVelocity *= Mathf.Exp(-PhysicsGlobal.AngularDamping * dt);
//    }

//    public void AddBuoyancyForce(float force, float localX)
//    {
//        accumulatedVerticalForce += force;
//    }

//    public void ComputeTorque(float torque)
//    {
//        accumulatedTorque += torque;
//    }
//    public void AddRightingTorque()
//    {
//        // How strongly the boat wants to be upright
//        float stiffness = PhysicsGlobal.RightingStiffness * mass * hullWidth;

//        // Opposes current rotation
//        float rightingTorque = -rotation * stiffness;

//        accumulatedTorque += rightingTorque;
//    }
//    public void AddThrottle(float dt)
//    {
//        // --- Forward direction ---
//        Vector2 forward = new Vector2(
//            Mathf.Cos(rotation),
//            Mathf.Sin(rotation)
//        );
//        // --- Throttle ---
//        Vector2 force = forward * throttleInput * throttleForce;
//        velocity += force / mass * dt;
//    }


//    // ========================
//    // Mass & Geometry
//    // ========================

//    public void RecomputeMass()
//    {
//        float compartmentMass = 0f;
//        float cargoMass = 0f;

//        foreach (var c in Compartments)
//            compartmentMass += c.Mass;

//        foreach (var cargo in CargoItems)
//            cargoMass += cargo.mass;

//        mass = baseMass + compartmentMass + cargoMass;
//        momentOfInertia = mass * hullWidth * hullHeight * 0.1f;
//    }

//    private void ComputeHullVolume()
//    {
//        hullVolume = hullWidth * hullHeight;
//    }

//    // ========================
//    // Compartments
//    // ========================

//    private void EqualizeAllCompartments(float dt)
//    {
//        CompartmentNetwork.UpdateAirEscape(this);
//        HashSet<Compartment> processed = new HashSet<Compartment>();

//        foreach (var c in Compartments)
//        {
//            if (processed.Contains(c))
//                continue;

//            CompartmentNetwork.EqualizeNetwork(c, dt);

//            Queue<Compartment> toVisit = new Queue<Compartment>();
//            HashSet<Compartment> network = new HashSet<Compartment>();

//            toVisit.Enqueue(c);

//            while (toVisit.Count > 0)
//            {
//                var comp = toVisit.Dequeue();
//                if (network.Contains(comp))
//                    continue;

//                network.Add(comp);

//                foreach (var conn in comp.connections)
//                {
//                    if (!conn.isOpen)
//                        continue;

//                    var neighbor = conn.A == comp ? conn.B : conn.A;
//                    if (!network.Contains(neighbor))
//                        toVisit.Enqueue(neighbor);
//                }
//            }

//            foreach (var comp in network)
//                processed.Add(comp);
//        }
//    }

//    // ========================
//    // Cargo
//    // ========================

//    public void RegisterCargo(Cargo cargo)
//    {
//        if (!CargoItems.Contains(cargo))
//            CargoItems.Add(cargo);
//    }

//    public void AttachCargo(Cargo cargo)
//    {
//        cargo.isAttachedToBoat = true;
//        cargo.attachedBoat = this;

//        cargo.localPositionOnBoat =
//            transform.InverseTransformPoint(cargo.transform.position);

//        cargo.transform.SetParent(transform);

//        var col = cargo.GetComponent<Collider2D>();
//        if (col != null)
//            col.enabled = false;

//        RegisterCargo(cargo);

//        //Debug.Log($"[Cargo] Attached cargo ({cargo.mass}kg)");
//    }

//    // ========================
//    // Gizmos & Debug
//    // ========================

//    private void DrawBoatGizmo()
//    {
//        Gizmos.color = Color.white;

//        Vector2[] localHull =
//        {
//            new Vector2(-1.5f, -0.5f),
//            new Vector2( 1.5f, -0.5f),
//            new Vector2( 1.0f,  0.5f),
//            new Vector2(-1.0f,  0.5f)
//        };

//        for (int i = 0; i < localHull.Length; i++)
//        {
//            Vector2 a = LocalToWorld(localHull[i]);
//            Vector2 b = LocalToWorld(localHull[(i + 1) % localHull.Length]);
//            Gizmos.DrawLine(a, b);
//        }
//    }

//    public Vector2 LocalToWorld(Vector2 local)
//    {
//        float rad = transform.eulerAngles.z * Mathf.Deg2Rad;
//        float c = Mathf.Cos(rad);
//        float s = Mathf.Sin(rad);

//        Vector2 rotated = new Vector2(
//            local.x * c - local.y * s,
//            local.x * s + local.y * c
//        );

//        return (Vector2)transform.position + rotated;
//    }
//}

