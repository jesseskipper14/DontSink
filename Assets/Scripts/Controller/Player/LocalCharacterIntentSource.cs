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
    [SerializeField] private KeyCode swimUpKey = KeyCode.Space;     // reuse Space by default
    [SerializeField] private KeyCode diveKey = KeyCode.LeftControl;           // or LeftControl
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    public CharacterIntent Current { get; private set; }

    // Latches JumpPressed until it is consumed by the motor/controller (usually in FixedUpdate).
    private bool _jumpPressedLatched;

    void Update()
    {
        float x = Input.GetAxisRaw(horizontalAxis);

        // Latch: once true, stays true until consumed.
        if (Input.GetKeyDown(jumpKey))
            _jumpPressedLatched = true;

        Current = new CharacterIntent
        {
            MoveX = Mathf.Clamp(x, -1f, 1f),
            JumpPressed = _jumpPressedLatched,
            JumpHeld = Input.GetKey(jumpKey),
            UprightHeld = Input.GetKey(uprightKey),
            SwimUpHeld = Input.GetKey(swimUpKey),
            DiveHeld = Input.GetKey(diveKey),
            SprintHeld = Input.GetKey(sprintKey)
        };
    }

    /// <summary>
    /// Call this from your motor/controller AFTER you read JumpPressed in FixedUpdate.
    /// </summary>
    public void ConsumeJumpPressed()
    {
        _jumpPressedLatched = false;
    }
}