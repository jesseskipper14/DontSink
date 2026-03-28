using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InteractPromptActionRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Image progressFill;

    private bool _pulse;
    private Color _baseColor = Color.white;

    public void Bind(in PromptAction action)
    {
        if (actionText != null)
        {
            actionText.text = action.Text ?? string.Empty;
            _baseColor = action.TextColor ?? Color.white;
            actionText.color = _baseColor;
        }

        _pulse = action.Pulse;

        if (progressRoot != null)
            progressRoot.SetActive(action.ShowProgress);

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(action.Progress01);
    }

    private void Update()
    {
        if (!_pulse || actionText == null)
            return;

        float a = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
        Color c = _baseColor;
        c.a = a;
        actionText.color = c;
    }
}