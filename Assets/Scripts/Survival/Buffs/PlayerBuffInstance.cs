using UnityEngine;

namespace Survival.Buffs
{
    public struct PlayerBuffInstance
    {
        public PlayerBuffDefinition definition;
        public string sourceId;

        [Range(0f, 1f)]
        public float severity01;
    }
}