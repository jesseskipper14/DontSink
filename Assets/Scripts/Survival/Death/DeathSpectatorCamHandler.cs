using UnityEngine;

namespace Survival.Death
{
    [DisallowMultipleComponent]
    public sealed class DeathSpectatorCamHandler : MonoBehaviour, IDeathHandler
    {
        public int Priority => 10;

        [Header("Legacy Camera Roots (unused)")]
        [Tooltip("Retained for serialized compatibility. The actor's CameraManager owns presentation instead.")]
        [SerializeField] private GameObject gameplayCameraRoot;
        [SerializeField] private GameObject spectatorCameraRoot;

        public void OnDeath(in DeathInfo info)
        {
            var owner = CameraManager.ForActor(this);
            if (owner != null) owner.BeginSpectator();
        }

        public void OnRespawn()
        {
            var owner = CameraManager.ForActor(this);
            if (owner != null) owner.EndSpectator();
        }
    }
}
