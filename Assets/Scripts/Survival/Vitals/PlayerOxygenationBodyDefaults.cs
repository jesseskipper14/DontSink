using UnityEngine;

namespace Survival.Vitals
{
    [DisallowMultipleComponent]
    public sealed class PlayerOxygenationBodyDefaults : MonoBehaviour, IOxygenationBodyRead
    {
        [Range(0f, 1f)] public float lungEffectiveness01 = 1f;
        [Range(0f, 1f)] public float perfusion01 = 1f;
        [Range(0f, 1f)] public float bloodQuality01 = 1f;
        [Min(0.1f)] public float demandMul = 1f;

        public float LungEffectiveness01 => lungEffectiveness01;
        public float Perfusion01 => perfusion01;
        public float BloodQuality01 => bloodQuality01;
        public float DemandMul => demandMul;
    }
}