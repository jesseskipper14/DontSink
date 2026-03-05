using Survival.Vitals;
using System.Collections.Generic;
using UnityEngine;

namespace Survival.Afflictions
{
    [DisallowMultipleComponent]
    public sealed class AfflictionSystem : MonoBehaviour, IAfflictionRead
    {
        [SerializeField] private bool useFixedUpdate = true;

        public IReadOnlyList<AfflictionInstance> Current => _current;

        private readonly List<IAfflictionProducer> _producers = new();
        private readonly List<AfflictionInstance> _current = new();

        private readonly Dictionary<StableId, float> _severityById = new();

        private void Awake()
        {
            GetComponentsInChildren(true, _producers);
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
            _current.Clear();
            _severityById.Clear();

            for (int i = 0; i < _producers.Count; i++)
                _producers[i].Produce(_current, dt);

            // Build lookup (keep max severity if duplicates)
            for (int i = 0; i < _current.Count; i++)
            {
                var a = _current[i];
                if (string.IsNullOrEmpty(a.stableId.value)) continue;

                if (_severityById.TryGetValue(a.stableId, out float prev))
                    _severityById[a.stableId] = Mathf.Max(prev, a.severity01);
                else
                    _severityById[a.stableId] = a.severity01;
            }

            _current.Sort((a, b) => b.severity01.CompareTo(a.severity01));
        }

        public bool Has(StableId id)
        {
            if (string.IsNullOrEmpty(id.value)) return false;
            return _severityById.TryGetValue(id, out float sev) && sev > 0f;
        }

        public bool TryGetSeverity01(StableId id, out float severity01)
        {
            severity01 = 0f;
            if (string.IsNullOrEmpty(id.value)) return false;

            if (_severityById.TryGetValue(id, out float sev))
            {
                severity01 = Mathf.Clamp01(sev);
                return true;
            }
            return false;
        }

        public void ClearNow()
        {
            _current.Clear();
        }
    }
}