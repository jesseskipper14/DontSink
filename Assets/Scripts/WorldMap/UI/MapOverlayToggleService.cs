using UnityEngine;

public sealed class MapOverlayToggleService : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MapOverlayController overlay;
    [SerializeField] private Transform player; // optional, only for auto-close

    [Header("Debug")]
    [SerializeField] private bool allowHotkeyM = true;

    [Header("Auto-close")]
    [SerializeField] private bool autoCloseWhenFar = false;
    [SerializeField] private float autoCloseDistance = 3.5f;

    private Transform _openSource;

    private void Awake()
    {
        if (overlay == null) overlay = FindAnyObjectByType<MapOverlayController>();
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    private void Update()
    {
        if (allowHotkeyM && Input.GetKeyDown(KeyCode.M))
            Toggle(null);

        if (autoCloseWhenFar && overlay != null && overlay.IsVisible && _openSource != null && player != null)
        {
            float d = Vector2.Distance(player.position, _openSource.position);
            if (d > autoCloseDistance)
                Close();
        }
    }

    public void Toggle(Transform source)
    {
        if (overlay == null) return;

        if (overlay.IsVisible) Close();
        else Open(source);
    }

    public void Open(Transform source)
    {
        if (overlay == null) return;
        _openSource = source;
        overlay.SetVisible(true);
    }

    public void Close()
    {
        if (overlay == null) return;
        overlay.SetVisible(false);
        _openSource = null;
    }
}
