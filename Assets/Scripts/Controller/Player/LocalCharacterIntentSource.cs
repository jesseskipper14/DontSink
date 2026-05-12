using UnityEngine;

/// <summary>
/// Local input adapter. In MP, only the owning client should run this.
/// Later replace with a network-fed implementation.
/// </summary>
public class LocalCharacterIntentSource : MonoBehaviour, ICharacterIntentSource
{
    [Header("Bindings (legacy input manager)")]
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Righting Binding")]
    [SerializeField] private KeyCode uprightKey = KeyCode.W;

    [Header("Swim Bindings")]
    [SerializeField] private KeyCode swimUpKey = KeyCode.Space;
    [SerializeField] private KeyCode diveKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Ladder Bindings")]
    [SerializeField] private KeyCode climbUpKey = KeyCode.W;
    [SerializeField] private KeyCode climbDownKey = KeyCode.S;

    [Header("Focus / Control Bindings")]
    [SerializeField] private int focusMouseButton = 1;      // right mouse
    [SerializeField] private int primaryUseMouseButton = 0; // left mouse
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

    public CharacterIntent Current { get; private set; }

    private bool _jumpPressedLatched;

    void Update()
    {
        if (GameplayInputBlocker.IsBlocked)
        {
            Current = default;
            return;
        }

        float x = Input.GetAxisRaw(horizontalAxis);

        if (Input.GetKeyDown(jumpKey))
            _jumpPressedLatched = true;

        Vector2 aimWorld = Vector2.zero;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 mouse = Input.mousePosition;
            Vector3 world = cam.ScreenToWorldPoint(mouse);
            aimWorld = new Vector2(world.x, world.y);
        }

        bool focusHeld = Input.GetMouseButton(focusMouseButton);
        bool primaryUseHeld = Input.GetMouseButton(primaryUseMouseButton);
        bool cancelHeld = Input.GetKey(cancelKey);

        Current = new CharacterIntent
        {
            MoveX = Mathf.Clamp(x, -1f, 1f),
            JumpPressed = _jumpPressedLatched,
            JumpHeld = Input.GetKey(jumpKey),
            UprightHeld = Input.GetKey(uprightKey),
            SwimUpHeld = Input.GetKey(swimUpKey),
            DiveHeld = Input.GetKey(diveKey),
            SprintHeld = Input.GetKey(sprintKey),

            ClimbUpHeld = Input.GetKey(climbUpKey),
            ClimbDownHeld = Input.GetKey(climbDownKey),

            FocusHeld = focusHeld,
            FocusWorldPoint = aimWorld,

            AimWorldPoint = aimWorld,
            PrimaryUseHeld = primaryUseHeld,
            CancelHeld = cancelHeld
        };
    }

    public void ConsumeJumpPressed()
    {
        _jumpPressedLatched = false;
    }

    private void OnDisable()
    {
        _jumpPressedLatched = false;
        Current = default;
    }

    private void OnEnable()
    {
        _jumpPressedLatched = false;
        Current = default;
    }
}