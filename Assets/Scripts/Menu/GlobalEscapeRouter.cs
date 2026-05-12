using UnityEngine;

[DisallowMultipleComponent]
public sealed class GlobalEscapeRouter : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode escapeKey = KeyCode.Escape;

    [Header("Refs")]
    [SerializeField] private EscapeMenuUI escapeMenu;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private void Awake()
    {
        if (escapeMenu == null)
            escapeMenu = FindAnyObjectByType<EscapeMenuUI>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(escapeKey))
            return;

        EscapeCloseRegistry registry = EscapeCloseRegistry.GetOrFind();

        if (registry != null && registry.TryCloseTopmost())
        {
            Log("Escape closed topmost overlay.");
            return;
        }

        if (escapeMenu != null)
        {
            escapeMenu.Open();
            Log("Escape opened escape menu.");
        }
        else
        {
            Log("Escape pressed, but no EscapeMenuUI assigned/found.");
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[GlobalEscapeRouter:{name}] {msg}", this);
    }
}