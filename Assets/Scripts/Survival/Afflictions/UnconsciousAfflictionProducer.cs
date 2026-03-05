//using System.Collections.Generic;
//using Survival.Vitals;

// DEPRECATED

//namespace Survival.Afflictions
//{
//    public sealed class UnconsciousAfflictionProducer : UnityEngine.MonoBehaviour, IAfflictionProducer
//    {
//        [UnityEngine.SerializeField] private string afflictionId = "aff.unconscious";
//        public string ProducerId => "core.aff.unconscious";

//        public void Produce(List<AfflictionInstance> outList, float dt)
//        {
//            if (!vitals.isUnconscious) return;

//            outList.Add(new AfflictionInstance
//            {
//                stableId = afflictionId,
//                severity01 = 1f,
//                stacks = 0,
//                secondsLeft = -1f
//            });
//        }
//    }
//}