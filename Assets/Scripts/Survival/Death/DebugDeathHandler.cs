using UnityEngine;

namespace Survival.Death
{
    [DisallowMultipleComponent]
    public sealed class DebugDeathHandler : MonoBehaviour, IDeathHandler
    {
        public int Priority => 0;

        public void OnDeath(in DeathInfo info)
        {
            Debug.Log($"PLAYER DIED: {info.causeId} | {info.message}");
            // Later: enable spectator cam, UI, etc.
        }

        public void OnRespawn()
        {
            Debug.Log("PLAYER RESPAWNED");
        }
    }
}