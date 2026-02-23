using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class DockingActionPanel : MonoBehaviour
{
    [Header("Root Object (the panel GameObject that should appear/disappear)")]
    [SerializeField] private GameObject rootObject;

    [Header("Widgets")]
    [SerializeField] private Button dockButton;

    [Header("Optional Label (either is fine)")]
    [SerializeField] private TMPro.TMP_Text tmpLabel;
    [SerializeField] private Text legacyLabel;

    private Action _onDock;

    private void Reset()
    {
        // Default: treat THIS object as the root panel unless you override it
        rootObject = gameObject;

        dockButton = GetComponentInChildren<Button>(true);
        tmpLabel = GetComponentInChildren<TMPro.TMP_Text>(true);
        legacyLabel = GetComponentInChildren<Text>(true);
    }

    private void Awake()
    {
        if (dockButton != null)
            dockButton.onClick.AddListener(() => _onDock?.Invoke());

        Hide(); // IMPORTANT: start hidden for real
    }

    public void Show(string message, Action onDock)
    {
        _onDock = onDock;

        if (tmpLabel != null) tmpLabel.text = message;
        if (legacyLabel != null) legacyLabel.text = message;

        SetVisible(true);
    }

    public void Hide()
    {
        _onDock = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (rootObject == null)
            rootObject = gameObject;

        rootObject.SetActive(visible);
    }
}