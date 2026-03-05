//using System.Collections.Generic;
//using UnityEngine;
//using Survival.Vitals;

// DEPRECATED

//namespace Survival.Afflictions
//{
//    [DisallowMultipleComponent]
//    public sealed class LowOxygenAfflictionProducer : MonoBehaviour, IAfflictionProducer
//    {
//        [Header("IDs")]
//        [SerializeField] private string afflictionId = "aff.low_oxygen";

//        [Header("Mapping")]
//        [SerializeField] private float startDebt = 0.10f;    // below this: invisible
//        [SerializeField] private float fullDebt = 0.75f;     // at/above this: severity=1

//        public string ProducerId => "core.aff.low_oxygen";

//        public void Produce(List<AfflictionInstance> outList, float dt)
//        {
//            if (debt < startDebt)
//                return;

//            float sev = Mathf.InverseLerp(startDebt, fullDebt, debt);
//            sev = Mathf.Clamp01(sev);

//            outList.Add(new AfflictionInstance
//            {
//                stableId = afflictionId,
//                severity01 = sev,
//                stacks = 0,
//                secondsLeft = -1f
//            });
//        }
//    }
//}