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

    public CharacterIntent Current { get; private set; }

    void Update()
    {
        float x = Input.GetAxisRaw(horizontalAxis);

        Current = new CharacterIntent
        {
            MoveX = Mathf.Clamp(x, -1f, 1f),
            JumpPressed = Input.GetKeyDown(jumpKey),
            JumpHeld = Input.GetKey(jumpKey),
        };
    }
}
