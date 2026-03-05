using UnityEngine;

namespace Survival.Death
{
    [DisallowMultipleComponent]
    public sealed class DeathSpectatorCamHandler : MonoBehaviour, IDeathHandler
    {
        public int Priority => 10;

        [Header("Cameras")]
        [SerializeField] private GameObject gameplayCameraRoot;
        [SerializeField] private GameObject spectatorCameraRoot;

        private void Awake()
        {
            if (spectatorCameraRoot) spectatorCameraRoot.SetActive(false);
        }

        public void OnDeath(in DeathInfo info)
        {
            if (gameplayCameraRoot) gameplayCameraRoot.SetActive(false);
            if (spectatorCameraRoot) spectatorCameraRoot.SetActive(true);
        }

        public void OnRespawn()
        {
            if (spectatorCameraRoot) spectatorCameraRoot.SetActive(false);
            if (gameplayCameraRoot) gameplayCameraRoot.SetActive(true);
        }
    }
}