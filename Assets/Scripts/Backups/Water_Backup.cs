//using System.Collections.Generic;
//using UnityEngine;

//[RequireComponent(typeof(LineRenderer))]
//public class Water : MonoBehaviour
//{
//    [Header("Wave Settings")]
//    public int points = 200;
//    public float width = 100f;
//    public float amplitude = 0.1f;
//    public float frequency = 0.3f;
//    public float speed = 0.5f;

//    [Header("Buoyancy Settings")]
//    public float damping = 1f;

//    [HideInInspector] public float[] heights;
//    [HideInInspector] public float waveOriginX = 0f; // NEW

//    private LineRenderer lineRenderer;
//    public Boat boat;

//    // ========================
//    // Debug
//    // ========================
//    [SerializeField] private bool debugWaveSampling = true;
//    private float debugTimer = 0f;

//    [Header("Debug")]
//    public bool debugBuoyancy = true;

//    private void Awake()
//    {
//        lineRenderer = GetComponent<LineRenderer>();
//        lineRenderer.positionCount = points;
//        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//        lineRenderer.material.color = Color.blue;
//        lineRenderer.sortingOrder = 0;
//        lineRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

//        heights = new float[points];
//        if (boat != null)
//            waveOriginX = transform.position.x;
//    }

//    public float waveScroll = 0f;
//    public float movementWaveScroll = 0f;

//    private void FixedUpdate()
//    {
//        if (points <= 1 || boat == null) return;

//        float dt = Time.fixedDeltaTime;

//        // --- Wave time evolution (independent of water motion) ---
//        waveScroll += dt * speed * Mathf.PI * frequency; // ω t

//        float dx = width / (points - 1);
//        float leftEdgeX = waveOriginX - width * 0.5f;

//        // --- Automatic patch repositioning based on boat position ---
//        float boatOffset = boat.position.x - transform.position.x;
//        if (Mathf.Abs(boatOffset) > width * 0.25f)
//        {
//            float shift = Mathf.Floor(boatOffset / (width * 0.25f)) * (width * 0.25f);
//            transform.position += new Vector3(shift, 0f, 0f);
//            waveOriginX += shift; // maintain visual continuity
//            leftEdgeX = waveOriginX - width * 0.5f;
//        }

//        // --- Update LineRenderer & heights (visuals) ---
//        for (int i = 0; i < points; i++)
//        {
//            float localX = i * dx;
//            float worldX = leftEdgeX + localX;

//            // VISUAL wave: relative to water origin
//            float waveY = amplitude * Mathf.Sin(Mathf.PI * frequency * (worldX - waveOriginX) + waveScroll);
//            heights[i] = waveY;

//            Vector3 linePos = new Vector3(leftEdgeX + localX, transform.position.y + waveY, 0f);
//            lineRenderer.SetPosition(i, linePos);
//        }

//        debugTimer += Time.fixedDeltaTime;
//        if (debugTimer >= 1f)
//        {
//            debugTimer = 0f;
//            DebugWaveSampling();
//        }

//        // --- Physics wave at boat (world-space) ---
//        float waveYAtBoat = SampleHeightPhysics(boat.position.x);
//        boat.TrackWave(waveYAtBoat);

//        // --- Apply buoyancy / flooding physics ---
//        float dtPhysics = Time.fixedDeltaTime;
//        ApplyBuoyancy(boat, dtPhysics);
//        ApplyFlooding(boat, dtPhysics);
//    }

//    // ========================
//    // Wave sampling functions
//    // ========================

//    // Physics: use world coordinates
//    public float SampleHeightPhysics(float worldX)
//    {
//        return amplitude * Mathf.Sin(Mathf.PI * frequency * worldX + waveScroll) + transform.position.y;
//    }

//    // Visual: water-relative coordinates
//    public float SampleHeightVisual(float worldX)
//    {
//        float relativeX = worldX - waveOriginX;
//        return amplitude * Mathf.Sin(Mathf.PI * frequency * relativeX + waveScroll) + transform.position.y;
//    }

//    // ========================
//    // Flooding & Buoyancy logic (unchanged)
//    // ========================
//    public void ApplyFlooding(Boat boat, float dt)
//    {
//        foreach (var c in boat.Compartments)
//        {
//            if (!c.isExposedToOcean) continue;

//            float oceanLevel = SampleHeightPhysics(c.transform.position.x);
//            float exposureY = c.OceanExposureWorldY;

//            float waterDepth = oceanLevel - exposureY;
//            if (waterDepth <= 0f) continue;

//            float capacity = c.maxVolume - c.waterVolume;
//            if (capacity <= 0f) continue;

//            float intake = Mathf.Min(waterDepth * c.fillRate * dt, capacity);
//            c.AddWater(intake);
//        }
//    }

//    public void ApplyBuoyancy(Boat boat, float dt)
//    {
//        if (boat == null) return;

//        int sliceCount = Mathf.Clamp(Mathf.CeilToInt(boat.hullWidth / 1f), 20, 50);
//        float sliceWidth = boat.hullWidth / sliceCount;
//        float accumulatedTorque = 0f;

//        for (int i = 0; i < sliceCount; i++)
//        {
//            float localX = -boat.hullWidth * 0.5f + sliceWidth * (i + 0.5f);
//            Vector2 sliceWorldPos = boat.LocalToWorld(new Vector2(localX, 0f));
//            bool debugThisSlice = debugBuoyancy && i == sliceCount / 2; // center slice debug

//            float waveY = SampleHeightPhysics(sliceWorldPos.x);

//            // Boat bottom references
//            float boatBottomLocalY = -boat.hullHeight * 0.5f;
//            float boatBottomWorldY = boat.transform.position.y + boatBottomLocalY;

//            if (debugThisSlice)
//            {
//                Debug.Log(
//                    $"[Buoyancy RefFrame]\n" +
//                    $" waveY (world): {waveY:F3}\n" +
//                    $" boatBottomLocalY: {boatBottomLocalY:F3}\n" +
//                    $" boatBottomWorldY: {boatBottomWorldY:F3}\n" +
//                    $" boatPosY: {boat.transform.position.y:F3}"
//                );
//            }

//            // Submersion fraction
//            float submerged01 = Mathf.Clamp01((waveY - boatBottomWorldY) / boat.hullHeight);
//            float submergedDepth = waveY - boatBottomLocalY; // for debug

//            if (debugThisSlice)
//            {
//                Debug.Log(
//                    $"[Buoyancy Submerge]\n" +
//                    $" submergedDepth(raw): {submergedDepth:F3}\n" +
//                    $" hullHeight: {boat.hullHeight:F3}\n" +
//                    $" submerged01: {submerged01:F3}"
//                );
//            }

//            float sliceVolume = submerged01 * (boat.hullVolume / sliceCount);

//            if (debugThisSlice)
//            {
//                Debug.Log(
//                    $"[Buoyancy Slice]\n" +
//                    $" sliceVolume: {sliceVolume:F4}\n" +
//                    $" hullVolume: {boat.hullVolume:F3}"
//                );
//            }

//            // Extra mass from compartments (skip if empty)
//            float extraMass = 0f;
//            foreach (var c in boat.Compartments)
//            {
//                if (c == null) continue;
//                float compMinX = c.transform.localPosition.x - c.size.x * 0.5f;
//                float compMaxX = c.transform.localPosition.x + c.size.x * 0.5f;
//                if (localX >= compMinX && localX <= compMaxX)
//                    extraMass += c.waterVolume * PhysicsGlobal.WaterDensity;
//            }

//            float sliceForce = sliceVolume * PhysicsGlobal.WaterDensity * PhysicsGlobal.Gravity - extraMass * PhysicsGlobal.Gravity;

//            // Trapped air
//            float trappedAirForce = 0f;
//            foreach (var c in boat.Compartments)
//            {
//                if (c == null || c.airVolume <= 0f || c.canReleaseAir) continue;
//                float compressionRatio = Mathf.Clamp01((c.waterVolume - 0.3f * c.maxVolume) / (0.5f * c.maxVolume));
//                trappedAirForce += c.airVolume * PhysicsGlobal.WaterDensity * PhysicsGlobal.Gravity * compressionRatio;
//            }
//            sliceForce += trappedAirForce;

//            accumulatedTorque += sliceForce * localX;

//            // Apply force if submerged
//            if (submerged01 > 0f) boat.AddBuoyancyForce(sliceForce, localX);

//            // --- NEW: catch suspicious forces ---
//            if (debugThisSlice)
//            {
//                if (submerged01 <= 0f && sliceForce > 0f)
//                {
//                    Debug.LogError(
//                        $"[Buoyancy ERROR] Positive force while submerged01 == 0\n" +
//                        $" sliceForce: {sliceForce:F3}"
//                    );
//                }

//                // NEW: log if visual vs physics mismatch
//                float visualY = boat.transform.position.y; // you could expand this with mesh sample
//                float delta = waveY - visualY;
//                Debug.Log(
//                    $"[Buoyancy Delta Check]\n" +
//                    $" waveY: {waveY:F3} | boatVisualY: {visualY:F3} | Δ={delta:F3}"
//                );
//            }
//        }

//        if (debugBuoyancy)
//        {
//            Debug.Log($"[Buoyancy Slice Table] Boat Y={boat.transform.position.y:F3}, HullHeight={boat.hullHeight:F3}");
//            int sliceCount2 = Mathf.Clamp(Mathf.CeilToInt(boat.hullWidth / 1f), 20, 50);
//            float sliceWidth2 = boat.hullWidth / sliceCount2;

//            for (int i = 0; i < sliceCount2; i++)
//            {
//                float localX = -boat.hullWidth * 0.5f + sliceWidth2 * (i + 0.5f);
//                Vector2 sliceWorldPos = boat.LocalToWorld(new Vector2(localX, 0f));
//                float waveY = SampleHeightPhysics(sliceWorldPos.x);

//                float boatBottomLocalY = -boat.hullHeight * 0.5f;
//                float boatBottomWorldY = boat.transform.position.y + boatBottomLocalY;
//                float submerged01 = Mathf.Clamp01((waveY - boatBottomWorldY) / boat.hullHeight);
//                float sliceVolume = submerged01 * (boat.hullVolume / sliceCount2);
//                float sliceForce = sliceVolume * PhysicsGlobal.WaterDensity * PhysicsGlobal.Gravity;

//                Debug.Log(
//                    $"Slice {i,2} | X={sliceWorldPos.x:F3} | WaveY={waveY:F3} | BoatBottom={boatBottomWorldY:F3} " +
//                    $"| Submerged01={submerged01:F3} | SliceVolume={sliceVolume:F3} | SliceForce={sliceForce:F3}"
//                );
//            }
//        }

//        boat.ComputeTorque(accumulatedTorque);
//    }


//    void DebugWaveSampling()
//    {
//        if (!debugWaveSampling || boat == null)
//            return;

//        float worldX = boat.position.x;

//        // Physics sample (used by buoyancy)
//        float sampledHeight = SampleHeightPhysics(worldX);

//        // Direct wave equation (what renderer uses)
//        float waveY =
//            amplitude *
//            Mathf.Sin(worldX * Mathf.PI * frequency + movementWaveScroll) +
//            transform.position.y;

//        float delta = sampledHeight - waveY;

//        Debug.Log(
//            $"[Water Debug] X={worldX:F2} | " +
//            $"SampleHeight={sampledHeight:F3} | " +
//            $"WaveEq={waveY:F3} | " +
//            $"Δ={delta:F4}"
//        );
//    }
//}


