using System;
using Survival.Vitals;

namespace Survival.Afflictions
{
    [Serializable]
    public struct AfflictionInstance
    {
        public StableId stableId;
        public float severity01;    // 0..1
        public int stacks;          // optional
        public float secondsLeft;   // optional (-1 for infinite)
    }
}