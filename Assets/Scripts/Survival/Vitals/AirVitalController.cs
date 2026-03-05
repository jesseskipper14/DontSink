//using UnityEngine;

// DEPRECATED

//namespace Survival.Vitals
//{
//    /// <summary>
//    /// Converts AirState to oxygen supply. Keeps vitals decoupled from air implementation.
//    /// </summary>
//    [DisallowMultipleComponent]
//    public sealed class AirVitalContributor : MonoBehaviour, IVitalContributor
//    {
//        [SerializeField] private MonoBehaviour airSource; // must implement IAirRead

//        public StableId ContributorId => "core.air";

//        private IAirRead Air => airSource as IAirRead;

//        private void Reset()
//        {
//            airSource = GetComponentInChildren<MonoBehaviour>();
//        }

//        public void Contribute(ref VitalContext ctx, float dt)
//        {
//            if (Air == null)
//                return;

//            // Extremely simple v1: if you can breathe, supply is your air fraction.
//            // Later: poison gas reduces this, snorkel changes IsBreathing rules, etc.
//            ctx.oxygenSupply01 *= Air.CanOxygenate ? Air.Air01 : 0f;          // quantity available
//            ctx.oxygenSupply01 *= Mathf.Clamp01(Air.OxygenQuality01);         // quality of that air
//        }
//    }
//}