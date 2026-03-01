using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DevExertionEnergyHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerExertionEnergyState model;

    [Header("UI")]
    [SerializeField] private Slider energySlider;
    [SerializeField] private Slider exertionSlider;
    [SerializeField] private Text stateText;
    [SerializeField] private Text energyStateText;
    [SerializeField] private Text debugText; // optional extra line

    void Awake()
    {
        if (!model) model = FindFirstObjectByType<PlayerExertionEnergyState>();
    }

    void Update()
    {
        if (model == null) return;

        if (energySlider) energySlider.value = model.Energy01;
        if (exertionSlider) exertionSlider.value = model.exertion01;

        if (stateText)
            stateText.text = model.CurrentState.ToString();
        if (energyStateText)
            energyStateText.text = model.CurrentEnergyState.ToString();
        if (debugText)
        {
            debugText.text =
                $"Energy: {(model.Energy01 * 100f):0}% | Exertion: {(model.exertion01 * 100f):0}%\n" +
                $"MoveAuth: {(model.MoveAuthority * 100f):0}% | SprintAuth: {(model.SprintAuthority * 100f):0}% | SwimUpAuth: {(model.SwimUpAuthority * 100f):0}%";
        }
    }
}