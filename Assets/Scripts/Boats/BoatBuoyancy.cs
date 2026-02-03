//using UnityEngine;

//[RequireComponent(typeof(Boat))]
//public class BoatBuoyancy : MonoBehaviour
//{
//    public WaveField wave;

//    [Header("Buoyancy")]
//    public bool debugBuoyancy = false;

//    Boat boat;
//    private PhysicsGlobals globals; // reference to ScriptableObject
//    public BoatBuoyancy(PhysicsGlobals globals)
//    {
//        this.globals = globals;

//        // Example usage
//        float drag = globals.WaterHorizontalDrag;
//    }

//    void Awake()
//    {
//        boat = GetComponent<Boat>();
//    }

//    public void ApplyBuoyancy(float dt)
//    {
//        if (wave == null || boat == null) return;

//        int sliceCount = Mathf.Clamp(Mathf.CeilToInt(boat.hullWidth), 20, 50);
//        float sliceWidth = boat.hullWidth / sliceCount;

//        float accumulatedTorque = 0f;

//        for (int i = 0; i < sliceCount; i++)
//        {
//            float localX = -boat.hullWidth * 0.5f + sliceWidth * (i + 0.5f);
//            Vector2 sliceWorldPos = boat.LocalToWorld(new Vector2(localX, 0f));

//            float waveY = wave.SampleHeight(sliceWorldPos.x);

//            float boatBottomWorldY =
//                boat.transform.position.y - boat.hullHeight * 0.5f;

//            float submerged01 =
//                Mathf.Clamp01((waveY - boatBottomWorldY) / boat.hullHeight);

//            if (submerged01 <= 0f)
//                continue;

//            float sliceVolume =
//                submerged01 * (boat.hullVolume / sliceCount);

//            float sliceForce =
//                sliceVolume *
//                globals.WaterDensity *
//                globals.Gravity;

//            //boat.AddBuoyancyForce(sliceForce, localX);
//            accumulatedTorque += sliceForce * localX;

//            if (debugBuoyancy)
//            {
//                Debug.Log(
//                    $"[Buoyancy] X={sliceWorldPos.x:F2} " +
//                    $"WaveY={waveY:F2} Sub={submerged01:F2} F={sliceForce:F2}"
//                );
//            }
//        }

//        // rf0124 boat.ComputeTorque(accumulatedTorque);
//    }

//    public float ComputeSubmergedFraction()
//    {
//        if (wave == null || boat == null)
//            return 0f;

//        int sliceCount = Mathf.Clamp(Mathf.CeilToInt(boat.hullWidth), 20, 50);
//        float sliceWidth = boat.hullWidth / sliceCount;

//        float submergedSum = 0f;

//        for (int i = 0; i < sliceCount; i++)
//        {
//            float localX = -boat.hullWidth * 0.5f + sliceWidth * (i + 0.5f);
//            Vector2 sliceWorldPos = boat.LocalToWorld(new Vector2(localX, 0f));

//            float waveY = wave.SampleHeight(sliceWorldPos.x);
//            float boatBottomWorldY =
//                boat.transform.position.y - boat.hullHeight * 0.5f;

//            float submerged01 =
//                Mathf.Clamp01((waveY - boatBottomWorldY) / boat.hullHeight);

//            submergedSum += submerged01;
//        }

//        // Average across slices
//        return submergedSum / sliceCount;
//    }

//}
