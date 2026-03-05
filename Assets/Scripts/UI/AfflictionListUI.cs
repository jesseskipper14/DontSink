using System.Text;
using UnityEngine;
using TMPro;

namespace Survival.Afflictions
{
    [DisallowMultipleComponent]
    public sealed class AfflictionListUI : MonoBehaviour
    {
        [SerializeField] private AfflictionSystem system;
        [SerializeField] private AfflictionCatalog catalog;
        [SerializeField] private TMP_Text label;

        [SerializeField] private int maxRows = 6;

        private readonly StringBuilder _sb = new();

        private void Awake()
        {
            if (system == null) system = FindAnyObjectByType<AfflictionSystem>();
        }

        private void Update()
        {
            if (system == null || label == null) return;

            _sb.Clear();

            var list = system.Current;
            int count = Mathf.Min(maxRows, list.Count);

            for (int i = 0; i < count; i++)
            {
                var a = list[i];
                string name = a.stableId.ToString();
                string tier = string.Empty;

                if (catalog != null && catalog.TryGet(a.stableId, out var def) && def != null)
                {
                    name = def.displayName;
                    tier = def.GetTierLabel(a.severity01);
                }

                if (!string.IsNullOrEmpty(tier))
                    _sb.Append($"{name} ({tier})");
                else
                    _sb.Append($"{name}");

                _sb.Append($"  {Mathf.RoundToInt(a.severity01 * 100f)}%\n");
            }

            label.text = _sb.ToString();
        }
    }
}