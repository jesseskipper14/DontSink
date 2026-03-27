using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InteractPromptActionRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text actionText;
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Image progressFill;

    public void Bind(in PromptAction action)
    {
        if (actionText != null)
            actionText.text = action.Text ?? string.Empty;

        if (progressRoot != null)
            progressRoot.SetActive(action.ShowProgress);

        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(action.Progress01);
    }
}