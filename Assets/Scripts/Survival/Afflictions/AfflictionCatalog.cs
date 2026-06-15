using System.Collections.Generic;
using UnityEngine;

namespace Survival.Afflictions
{
    [CreateAssetMenu(menuName = "Survival/Affliction Catalog")]
    public sealed class AfflictionCatalog : ScriptableObject
    {
        public List<AfflictionDefinition> definitions = new();

        private Dictionary<string, AfflictionDefinition> _byId;

        public bool TryGet(string stableId, out AfflictionDefinition def)
        {
            Ensure();
            return _byId.TryGetValue(stableId, out def);
        }

        private void Ensure()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, AfflictionDefinition>();

            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d == null || string.IsNullOrWhiteSpace(d.stableId)) continue;
                _byId[d.stableId] = d;
            }
        }
    }
}