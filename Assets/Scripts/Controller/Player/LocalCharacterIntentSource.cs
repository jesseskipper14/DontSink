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

    public CharacterIntent Current { get; private set; }

    private bool _jumpPressedLatched;

    void Update()
    {
        float x = Input.GetAxisRaw(horizontalAxis);

        if (Input.GetKeyDown(jumpKey))
            _jumpPressedLatched = true;

        bool focusHeld = Input.GetMouseButton(1); // right click

        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("[FocusDebug] Right mouse DOWN registered by LocalCharacterIntentSource.", this);
        }

        if (focusHeld)
        {
            Debug.Log("[FocusDebug] Right mouse HELD.", this);
        }

        Vector2 focusWorld = Vector2.zero;
        if (focusHeld)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 mouse = Input.mousePosition;
                Vector3 world = cam.ScreenToWorldPoint(mouse);
                focusWorld = new Vector2(world.x, world.y);
            }
        }

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
            FocusWorldPoint = focusWorld
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