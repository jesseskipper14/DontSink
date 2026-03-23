using UnityEngine;

public sealed class LoadoutContainerOverlayHotkey : MonoBehaviour
{
    [SerializeField] private LoadoutContainerOverlayUI overlay;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            overlay.ToggleAll();
    }
}