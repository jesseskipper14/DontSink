using UnityEngine;

/// <summary>
/// Local input adapter for interaction. In MP, only owning client runs this.
/// </summary>
[DefaultExecutionOrder(-50)]
public class LocalInteractionIntentSource : MonoBehaviour, IInteractionIntentSource
{
    [Header("Bindings (legacy input manager)")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode pickupKey = KeyCode.F;
    [SerializeField] private KeyCode toggleKey = KeyCode.T;
    [SerializeField] private KeyCode unsecureKey = KeyCode.X;
    [SerializeField] private KeyCode linkKey = KeyCode.L;

    [Header("Mouse Interact")]
    [SerializeField] private bool enableDoubleClickInteract = true;
    [SerializeField, Min(0.05f)] private float doubleClickMaxInterval = 0.28f;
    [SerializeField, Min(0f)] private float doubleClickMaxScreenDistance = 18f;

    [Header("Aim")]
    [Tooltip("If true, uses mouse position as aim world point. If false, AimWorld will be Vector2.zero.")]
    [SerializeField] private bool useMouseAim = true;

    [Header("Gameplay Input Blocking")]
    [SerializeField] private bool respectGameplayInputBlocker = true;

    private InteractionIntent _current;
    public InteractionIntent Current
    {
        get
        {
            var intent = _current;
            // Prompts query in LateUpdate, after camera follow. Reproject the mouse
            // with that camera pose without sampling button pulses a second time.
            if (intent.HasAimWorld && useMouseAim)
            {
                Camera camera = CameraManager.CameraForActor(this);
                if (camera != null) intent.AimWorld = camera.ScreenToWorldPoint(Input.mousePosition);
            }
            return intent;
        }
        private set => _current = value;
    }

    /// <summary>
    /// Presentation-only binding label for local UI. Gameplay should consume
    /// InteractionIntent rather than reading this physical key directly.
    /// </summary>
    public string InteractBindingLabel =>
        interactKey == KeyCode.None
            ? "Interact"
            : interactKey.ToString();

    private float _lastClickTime = -999f;
    private Vector2 _lastClickScreenPos;

    private void Update()
    {
        if (!CameraManager.HasGameplayInput(this) ||
            (respectGameplayInputBlocker && GameplayInputBlocker.IsBlocked) ||
            InteractionInputBlocker.IsBlocked)
        {
            ClearIntentAndResetClickState();
            return;
        }

        Vector2 aimWorld = Vector2.zero;
        bool hasAimWorld = false;

        Camera camera = CameraManager.CameraForActor(this);
        if (useMouseAim && camera != null)
        {
            Vector3 m = Input.mousePosition;
            aimWorld = camera.ScreenToWorldPoint(m);
            hasAimWorld = true;
        }

        bool interactPressed = Input.GetKeyDown(interactKey);
        bool interactHeld = Input.GetKey(interactKey);
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
            InteractHeld = interactHeld,
            InteractDoublePressed = doublePressed,

            PickupPressed = Input.GetKeyDown(pickupKey),
            PickupHeld = Input.GetKey(pickupKey),
            PickupReleased = Input.GetKeyUp(pickupKey),

            TogglePressed = Input.GetKeyDown(toggleKey),
            UnsecurePressed = Input.GetKeyDown(unsecureKey),
            LinkPressed = Input.GetKeyDown(linkKey),

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
