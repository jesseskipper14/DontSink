using System.Collections.Generic;
using UnityEngine;

namespace Survival.Buffs
{
    [DisallowMultipleComponent]
    public sealed class PlayerBuffSystem : MonoBehaviour, IPlayerBuffRead
    {
        [SerializeField] private bool useFixedUpdate = true;

        public IReadOnlyList<PlayerBuffInstance> Current => _current;
        public int Version { get; private set; }

        private readonly List<IPlayerBuffProducer> _producers = new();
        private readonly List<PlayerBuffInstance> _current = new();
        private readonly Dictionary<string, float> _severityById = new();

        private void Awake()
        {
            RebuildProducers();
        }

        public void RebuildProducers()
        {
            _producers.Clear();
            GetComponentsInChildren(true, _producers);
            Version++;
        }

        private void FixedUpdate()
        {
            if (!useFixedUpdate)
                return;

            Tick(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (useFixedUpdate)
                return;

            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            _current.Clear();
            _severityById.Clear();

            for (int i = 0; i < _producers.Count; i++)
                _producers[i]?.Produce(_current, dt);

            for (int i = 0; i < _current.Count; i++)
            {
                PlayerBuffInstance buff = _current[i];

                if (buff.definition == null)
                    continue;

                string stableId = buff.definition.StableId;
                if (string.IsNullOrWhiteSpace(stableId))
                    continue;

                float severity = Mathf.Clamp01(buff.severity01);

                if (_severityById.TryGetValue(stableId, out float prev))
                    _severityById[stableId] = Mathf.Max(prev, severity);
                else
                    _severityById[stableId] = severity;
            }

            _current.Sort((a, b) => b.severity01.CompareTo(a.severity01));
            Version++;
        }

        public bool Has(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;

            return _severityById.TryGetValue(stableId, out float severity) &&
                   severity > 0f;
        }

        public bool TryGetSeverity01(string stableId, out float severity01)
        {
            severity01 = 0f;

            if (string.IsNullOrWhiteSpace(stableId))
                return false;

            if (_severityById.TryGetValue(stableId, out float severity))
            {
                severity01 = Mathf.Clamp01(severity);
                return true;
            }

            return false;
        }

        public void ClearNow()
        {
            _current.Clear();
            _severityById.Clear();
            Version++;
        }
    }
}