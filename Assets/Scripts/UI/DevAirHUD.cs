using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DevAirHUD : MonoBehaviour
{
    [SerializeField] private PlayerAirState air;
    [SerializeField] private Slider airSlider;
    [SerializeField] private Text stateText;
    [SerializeField] private Text debugText;

    void Awake()
    {
        if (!air) air = FindFirstObjectByType<PlayerAirState>();
    }

    void Update()
    {
        if (!air) return;

        if (airSlider) airSlider.value = air.Air01;
        if (stateText) stateText.text = air.CurrentState.ToString();

        if (debugText)
        {
            debugText.text =
                $"Air: {air.airCurrent:0}/{air.MaxAir:0} ({air.Air01 * 100f:0}%)\n" +
                $"Underwater: {air.IsUnderwater}";
        }
    }
}