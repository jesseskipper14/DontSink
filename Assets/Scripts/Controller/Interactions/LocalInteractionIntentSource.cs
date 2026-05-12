using UnityEngine;

/// <summary>
/// Local input adapter for interaction. In MP, only owning client runs this.
/// </summary>
public class LocalInteractionIntentSource : MonoBehaviour, IInteractionIntentSource
{
    [Header("Bindings (legacy input manager)")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private KeyCode toggleKey = KeyCode.T;

    [Header("Mouse Interact")]
    [SerializeField] private bool enableDoubleClickInteract = true;
    [SerializeField, Min(0.05f)] private float doubleClickMaxInterval = 0.28f;
    [SerializeField, Min(0f)] private float doubleClickMaxScreenDistance = 18f;

    [Header("Aim")]
    [Tooltip("If true, uses mouse position as aim world point. If false, AimWorld will be Vector2.zero.")]
    [SerializeField] private bool useMouseAim = true;

    [Header("Gameplay Input Blocking")]
    [SerializeField] private bool respectGameplayInputBlocker = true;

    public InteractionIntent Current { get; private set; }

    private float _lastClickTime = -999f;
    private Vector2 _lastClickScreenPos;

    private void Update()
    {
        if (respectGameplayInputBlocker && GameplayInputBlocker.IsBlocked)
        {
            ClearIntentAndResetClickState();
            return;
        }

        Vector2 aimWorld = Vector2.zero;
        bool hasAimWorld = false;

        if (useMouseAim && Camera.main != null)
        {
            Vector3 m = Input.mousePosition;
            aimWorld = Camera.main.ScreenToWorldPoint(m);
            hasAimWorld = true;
        }

        bool interactPressed = Input.GetKeyDown(interactKey);
        bool doublePressed = false;

        if (enableDoubleClickInteract && Input.GetMouseButtonDown(0))
        {
            Vector2 screenPos = Input.mousePosition;
            float now = Time.unscaledTime;

            bool closeInTime = now - _lastClickTime <= doubleClickMaxInterval;
            bool closeOnScreen =
                _lastClickTime > -998f &&
                Vector2.Distance(screenPos, _lastClickScreenPos) <= doubleClickMaxScreenDistance;

            if (closeInTime && closeOnScreen)
                doublePressed = true;

            _lastClickTime = now;
            _lastClickScreenPos = screenPos;
        }

        Current = new InteractionIntent
        {
            InteractPressed = interactPressed || doublePressed,
            InteractDoublePressed = doublePressed,

            PickupPressed = Input.GetKeyDown(pickupKey),
            PickupHeld = Input.GetKey(pickupKey),
            PickupReleased = Input.GetKeyUp(pickupKey),

            TogglePressed = Input.GetKeyDown(toggleKey),

            AimWorld = aimWorld,
            HasAimWorld = hasAimWorld
        };
    }

    private void ClearIntentAndResetClickState()
    {
        Current = default;

        // Important: do not let UI clicks count as half of a future double-click
        // after the menu closes. Yes, that bug would absolutely happen.
        _lastClickTime = -999f;
        _lastClickScreenPos = default;
    }
}