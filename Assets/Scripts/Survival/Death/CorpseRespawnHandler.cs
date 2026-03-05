using UnityEngine;
using Survival.Afflictions;
using Survival.Vitals;

namespace Survival.Death
{
    [DisallowMultipleComponent]
    public sealed class CorpseRespawnHandler : MonoBehaviour, IDeathHandler
    {
        public int Priority => 0;

        [Header("Corpse")]
        [SerializeField] private GameObject corpsePrefab;
        [SerializeField] private Transform corpseSpawnPointOverride; // optional

        [Header("Respawn")]
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private KeyCode respawnKey = KeyCode.R;

        [Header("Disable While Dead")]
        [Tooltip("These are the 'living' systems that must stop when dead.")]
        [SerializeField] private Behaviour[] livingBehaviours;

        [Header("Camera Mode")]
        [Tooltip("Enable while dead (spectator/freecam).")]
        [SerializeField] private Behaviour[] deadCamBehaviours;
        [Tooltip("Enable while alive (follow camera, normal controls).")]
        [SerializeField] private Behaviour[] aliveCamBehaviours;

        [Header("Refs (auto if null)")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private PlayerAirState air;
        [SerializeField] private PlayerOxygenationState oxygenation;
        [SerializeField] private AfflictionSystem afflictions;
        [SerializeField] private PlayerExertionEnergyState exertionEnergy;

        private bool _isDead;

        private void Awake()
        {
            if (!body) body = GetComponentInParent<Rigidbody2D>();
            if (!air) air = GetComponentInChildren<PlayerAirState>(true);
            if (!oxygenation) oxygenation = GetComponentInChildren<PlayerOxygenationState>(true);
            if (!afflictions) afflictions = GetComponentInChildren<AfflictionSystem>(true);
            if (!exertionEnergy) exertionEnergy = GetComponentInChildren<PlayerExertionEnergyState>(true);

            // Make sure dead cam is off by default
            SetEnabled(deadCamBehaviours, false);
            SetEnabled(aliveCamBehaviours, true);
        }

        private Vector3 _deathPos;
        private Quaternion _deathRot;
        private bool _hasDeathPose;

        public void OnDeath(in DeathInfo info)
        {
            if (_isDead) return;
            _isDead = true;

            // Record pose EXACTLY at death
            var t = transform.root; // or your body transform if that’s the “pawn”
            _deathPos = t.position;
            _deathRot = t.rotation;
            _hasDeathPose = true;

            // Stop physics motion immediately
            if (body)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            // Kill living systems (no "recovering" from drowning)
            SetEnabled(livingBehaviours, false);

            // Camera to spectator
            SetEnabled(aliveCamBehaviours, false);
            SetEnabled(deadCamBehaviours, true);
        }

        public void OnRespawn()
        {
            // We do not use this for now. Respawn is player-driven (keypress/UI).
        }

        private void Update()
        {
            if (!_isDead) return;

            if (Input.GetKeyDown(respawnKey))
                RespawnNewLife();
        }

        private void RespawnNewLife()
        {
            // Teleport pawn (same instance)
            if (respawnPoint)
                transform.root.position = respawnPoint.position;

            if (body)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            // Reset vitals/afflictions so this is truly a new life
            if (air != null)
            {
                // If you add air.ResetState(), call it here.
                air.lungGasQuality01 = 1f;
                air.airCurrent = air.MaxAir > 0f ? air.MaxAir : air.airCurrent;
                air.IsUnderwater = false;
            }

            if (oxygenation != null)
                oxygenation.ResetState(1f);

            if (afflictions != null)
                afflictions.ClearNow();

            if (exertionEnergy != null)
                exertionEnergy.ResetState();

            // Restore systems
            SetEnabled(livingBehaviours, true);

            // Restore camera
            SetEnabled(deadCamBehaviours, false);
            SetEnabled(aliveCamBehaviours, true);

            // Spawn corpse NOW (pawn is being “removed”)
            if (_hasDeathPose)
                SpawnCorpseAt(_deathPos, _deathRot);

            _isDead = false;
            _hasDeathPose = false;
        }

        private void SpawnCorpseAt(Vector3 pos, Quaternion rot)
        {
            if (!corpsePrefab) return;
            Instantiate(corpsePrefab, pos, rot);
        }

        private static void SetEnabled(Behaviour[] list, bool enabled)
        {
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
                if (list[i]) list[i].enabled = enabled;
        }
    }
}