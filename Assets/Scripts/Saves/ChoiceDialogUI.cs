using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ChoiceDialogUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryButtonText;

    [SerializeField] private Button secondaryButton;
    [SerializeField] private TMP_Text secondaryButtonText;

    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelButtonText;

    private Action _primary;
    private Action _secondary;
    private Action _cancel;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        Hide();

        if (primaryButton != null)
            primaryButton.onClick.AddListener(ChoosePrimary);

        if (secondaryButton != null)
            secondaryButton.onClick.AddListener(ChooseSecondary);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(ChooseCancel);
    }

    public void Show(
        string title,
        string message,
        string primaryLabel,
        Action primary,
        string secondaryLabel = null,
        Action secondary = null,
        string cancelLabel = "Cancel",
        Action cancel = null)
    {
        _primary = primary;
        _secondary = secondary;
        _cancel = cancel;

        if (titleText != null)
            titleText.text = title ?? "";

        if (messageText != null)
            messageText.text = message ?? "";

        SetupButton(primaryButton, primaryButtonText, primaryLabel, primary != null);
        SetupButton(secondaryButton, secondaryButtonText, secondaryLabel, secondary != null);
        SetupButton(cancelButton, cancelButtonText, cancelLabel, true);

        if (root != null)
            root.SetActive(true);

        GameplayInputBlocker.Push(this);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);

        GameplayInputBlocker.Pop(this);


        _primary = null;
        _secondary = null;
        _cancel = null;
    }

    private void SetupButton(Button button, TMP_Text label, string text, bool visible)
    {
        if (button != null)
            button.gameObject.SetActive(visible);

        if (label != null)
            label.text = string.IsNullOrWhiteSpace(text) ? "OK" : text;
    }

    private void ChoosePrimary()
    {
        Action action = _primary;
        Hide();
        action?.Invoke();
    }

    private void ChooseSecondary()
    {
        Action action = _secondary;
        Hide();
        action?.Invoke();
    }

    private void ChooseCancel()
    {
        Action action = _cancel;
        Hide();
        action?.Invoke();
    }

    private void OnDisable()
    {
        GameplayInputBlocker.Pop(this);
    }
}