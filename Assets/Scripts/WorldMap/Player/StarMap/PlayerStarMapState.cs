using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldMap.Player.StarMap
{
    /// <summary>
    /// Player-owned star map state: knowledge about routes keyed by RouteKey string.
    /// This is player truth, not world truth.
    /// </summary>
    [Serializable]
    public sealed class PlayerStarMapState
    {
        [Serializable]
        private struct Entry
        {
            public string routeKey;               // RouteKey.Make(fromStableId, toStableId)
            public RouteKnowledgeRecord record;   // knowledge data
        }

        [SerializeField] private List<Entry> _entries = new();

        // Non-serialized runtime cache for fast lookups.
        [NonSerialized] private Dictionary<string, RouteKnowledgeRecord> _lookup;
        [NonSerialized] private bool _lookupBuilt;

        /// <summary>
        /// Enumerate all stored entries (mostly for debugging/serialization tooling).
        /// </summary>
        public IReadOnlyList<(string routeKey, RouteKnowledgeRecord record)> GetAll()
        {
            var list = new List<(string, RouteKnowledgeRecord)>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
                list.Add((_entries[i].routeKey, _entries[i].record));
            return list;
        }

        public bool TryGet(string routeKey, out RouteKnowledgeRecord record)
        {
            EnsureLookup();
            return _lookup.TryGetValue(routeKey, out record);
        }

        public RouteKnowledgeRecord GetOrCreate(string routeKey)
        {
            if (string.IsNullOrWhiteSpace(routeKey))
                throw new ArgumentException("routeKey is null/empty.", nameof(routeKey));

            EnsureLookup();

            if (_lookup.TryGetValue(routeKey, out var existing))
                return existing;

            var rec = new RouteKnowledgeRecord();
            _entries.Add(new Entry { routeKey = routeKey, record = rec });
            _lookup[routeKey] = rec;
            return rec;
        }

        public void Remove(string routeKey)
        {
            if (string.IsNullOrWhiteSpace(routeKey))
                return;

            // Remove from list
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].routeKey == routeKey)
                {
                    _entries.RemoveAt(i);
                    break;
                }
            }

            // Remove from cache if built
            if (_lookupBuilt)
                _lookup.Remove(routeKey);
        }

        public void Clear()
        {
            _entries.Clear();
            if (_lookupBuilt)
                _lookup.Clear();
        }

        /// <summary>
        /// Call this after deserialization if you need lookups immediately.
        /// Otherwise it's built lazily.
        /// </summary>
        public void RebuildLookup()
        {
            _lookup = new Dictionary<string, RouteKnowledgeRecord>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (string.IsNullOrWhiteSpace(e.routeKey))
                    continue;

                // Prefer last writer if duplicates exist (defensive against old/bad saves).
                _lookup[e.routeKey] = e.record ?? new RouteKnowledgeRecord();
            }
            _lookupBuilt = true;
        }

        private void EnsureLookup()
        {
            if (_lookupBuilt)
                return;

            RebuildLookup();
        }
    }
}
