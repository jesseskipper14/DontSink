//using System.Drawing;
//using UnityEngine;

//[RequireComponent(typeof(Boat))]
//public class BoatFlooding : MonoBehaviour
//{
//    public WaveField wave;

//    Boat boat;

//    void Awake()
//    {
//        boat = GetComponent<Boat>();
//    }

//    public void ApplyFlooding(float dt)
//    {
//        if (wave == null || boat == null) return;

//        foreach (var c in boat.Compartments)
//        {
//            if (!c.isExposedToOcean) continue;
//            if (c.waterVolume >= c.maxVolume) continue;

//            // Sample multiple points along the top edge
//            Vector3[] topPoints = c.GetTopEdgeWorldPoints(10);
//            float maxOceanHeightAboveTop = float.MinValue;

//            foreach (var p in topPoints)
//            {
//                float oceanLevel = wave.SampleHeight(p.x);
//                float aboveTop = oceanLevel - p.y;
//                if (aboveTop > maxOceanHeightAboveTop)
//                    maxOceanHeightAboveTop = aboveTop;
//            }

//            // Only add water if ocean is above at least one point
//            if (maxOceanHeightAboveTop > 0f)
//            {
//                float capacity = c.maxVolume - c.waterVolume;
//                float intake = Mathf.Min(maxOceanHeightAboveTop * c.fillRate * dt, capacity);
//                c.AddWater(intake);
//            }
//        }
//    }
//}
