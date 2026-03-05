using UnityEngine;
using TMPro;
using Survival.Vitals;

[DisallowMultipleComponent]
public sealed class SpO2DebugUI : MonoBehaviour
{
    [SerializeField] private PlayerOxygenationState oxygenation;
    [SerializeField] private TMP_Text label;

    [SerializeField] private bool showExposure = true;

    private void Awake()
    {
        if (oxygenation == null) oxygenation = FindAnyObjectByType<PlayerOxygenationState>();
    }

    private void Update()
    {
        if (oxygenation == null || label == null) return;

        if (showExposure)
        {
            label.text =
                $"SpO2: {oxygenation.SpO2Percent}%\n" +
                $"< {Mathf.RoundToInt(oxygenation.lowThresh * 100)}%: {oxygenation.SecondsBelowLow:0.0}s\n" +
                $"< {Mathf.RoundToInt(oxygenation.criticalThresh * 100)}%: {oxygenation.SecondsBelowCritical:0.0}s";
        }
        else
        {
            label.text = $"SpO2: {oxygenation.SpO2Percent}%";
        }
    }
}