using UnityEngine;

public sealed class MapOverlayToggleService : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WorldMapOverlayRunner mapRunner;
    [SerializeField] private Transform player; // optional, only for auto-close

    [Header("Debug")]
    [SerializeField] private bool allowHotkeyM = true;
    [SerializeField] private bool respectGameplayInputBlocker = true;

    [Header("Auto-close")]
    [SerializeField] private bool autoCloseWhenFar = false;
    [SerializeField] private float autoCloseDistance = 3.5f;

    private Transform _openSource;

    private void Awake()
    {
        AutoWire();
    }

    private void Update()
    {
        AutoWire();

        bool mapOpen = mapRunner != null && mapRunner.IsWorldMapOpen;
        bool inputBlocked = respectGameplayInputBlocker && GameplayInputBlocker.IsBlocked;

        // Allow M to close the map even while gameplay input is blocked by the overlay.
        // Otherwise the debug toggle becomes "open only", because naturally UI loves tiny betrayals.
        if (allowHotkeyM && Input.GetKeyDown(KeyCode.M) && (mapOpen || !inputBlocked))
            Toggle(null);

        if (autoCloseWhenFar &&
            mapRunner != null &&
            mapRunner.IsWorldMapOpen &&
            _openSource != null &&
            player != null)
        {
            float d = Vector2.Distance(player.position, _openSource.position);
            if (d > autoCloseDistance)
                Close();
        }
    }

    public void Toggle(Transform source)
    {
        AutoWire();

        if (mapRunner == null)
            return;

        if (mapRunner.IsWorldMapOpen)
            Close();
        else
            Open(source);
    }

    public void Open(Transform source)
    {
        AutoWire();

        if (mapRunner == null)
        {
            Debug.LogError("[MapOverlayToggleService] Missing WorldMapOverlayRunner.", this);
            return;
        }

        _openSource = source;

        bool opened = mapRunner.OpenWorldMap();
        if (!opened)
            _openSource = null;
    }

    public void Close()
    {
        AutoWire();

        if (mapRunner == null)
            return;

        mapRunner.CloseWorldMap();
        _openSource = null;
    }

    private void AutoWire()
    {
        if (mapRunner == null)
            mapRunner = FindAnyObjectByType<WorldMapOverlayRunner>(FindObjectsInactive.Include);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                player = p.transform;
        }
    }
}