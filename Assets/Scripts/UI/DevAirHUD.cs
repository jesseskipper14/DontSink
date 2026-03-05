using UnityEngine;
using UnityEngine.UI;
using Survival.Vitals;

[DisallowMultipleComponent]
public sealed class DevAirHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerAirState air;
    [SerializeField] private Slider airSlider;
    [SerializeField] private Image airFillImage;
    [SerializeField] private Text stateText;
    [SerializeField] private Text debugText;

    [Header("Color")]
    [SerializeField] private Color goodColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] private Color badColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Range(0f, 1f)]
    [SerializeField] private float qualityWeight = 0.75f;

    private void Awake()
    {
        if (!air) air = FindFirstObjectByType<PlayerAirState>();

        if (!airFillImage && airSlider)
        {
            var fill = airSlider.fillRect;
            if (fill) airFillImage = fill.GetComponent<Image>();
        }
    }

    private void Update()
    {
        if (!air) return;

        float qty = Mathf.Clamp01(air.Air01);
        float qual = Mathf.Clamp01(air.LungGasQuality01);

        if (airSlider) airSlider.value = qty;
        if (stateText) stateText.text = air.CurrentState.ToString();

        if (airFillImage)
        {
            float badness =
                (1f - qual) * qualityWeight +
                (1f - qty) * (1f - qualityWeight);

            airFillImage.color = Color.Lerp(goodColor, badColor, Mathf.Clamp01(badness));
        }

        if (debugText)
        {
            debugText.text =
                $"AirQty: {air.airCurrent:0}/{air.MaxAir:0} ({qty * 100f:0}%)\n" +
                $"LungQual: {qual * 100f:0}%\n" +
                $"CanOxy: {air.CanOxygenate}\n" +
                $"Underwater: {air.IsUnderwater}\n" +
                $"EnvO2Qual: {air.OxygenQuality01 * 100f:0}%";
        }
    }
}