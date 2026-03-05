//using System;

//// DEPRECATED

//namespace Survival.Vitals
//{
//    /// <summary>
//    /// Aggregated "physiology inputs" built from contributors each frame (or fixed tick).
//    /// Think: supply/transport/perfusion/demand and recovery modifiers.
//    /// </summary>
//    [Serializable]
//    public sealed class VitalContext
//    {
//        // Oxygen path
//        public float oxygenSupply01 = 1f;       // lungs/environment (air, poison gas later)
//        public float oxygenTransport01 = 1f;    // blood quality / CO poisoning later
//        public float perfusion01 = 1f;          // circulation / blood volume later

//        // Demand path (fever, exertion, panic later)
//        public float brainDemandMul = 1f;

//        // Recovery modifiers (infection can slow recovery)
//        public float debtRecoveryMul = 1f;

//        // Safety clamps
//        public void Clamp01()
//        {
//            oxygenSupply01 = Clamp01(oxygenSupply01);
//            oxygenTransport01 = Clamp01(oxygenTransport01);
//            perfusion01 = Clamp01(perfusion01);
//            brainDemandMul = MathF.Max(0.1f, brainDemandMul);
//            debtRecoveryMul = MathF.Max(0f, debtRecoveryMul);
//        }

//        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
//    }
//}