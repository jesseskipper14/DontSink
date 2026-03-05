using System.Collections.Generic;
using UnityEngine;

namespace Survival.Death
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathSystem : MonoBehaviour
    {
        [SerializeField] private bool useFixedUpdate = true;

        public bool IsDead { get; private set; }
        public DeathInfo? LastDeath { get; private set; }

        private readonly List<IDeathCause> _causes = new();
        private readonly List<IDeathHandler> _handlers = new();

        private void Awake()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            _causes.Clear();
            _handlers.Clear();

            // Unity can fetch MonoBehaviours; we filter by interface ourselves.
            var monos = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;

                if (m is IDeathCause c) _causes.Add(c);
                if (m is IDeathHandler h) _handlers.Add(h);
            }

            _handlers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        private void FixedUpdate()
        {
            if (!useFixedUpdate) return;
            Tick(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (useFixedUpdate) return;
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            if (IsDead) return;

            for (int i = 0; i < _causes.Count; i++)
            {
                if (_causes[i] == null) continue;

                if (_causes[i].Evaluate(dt, out var info))
                {
                    Die(info);
                    return;
                }
            }
        }

        private void Die(in DeathInfo info)
        {
            IsDead = true;
            LastDeath = info;

            for (int i = 0; i < _handlers.Count; i++)
                _handlers[i]?.OnDeath(info);
        }

        public void DebugRespawnNow()
        {
            if (!IsDead) return;

            IsDead = false;
            LastDeath = null;

            for (int i = 0; i < _handlers.Count; i++)
                _handlers[i]?.OnRespawn();
        }
    }
}