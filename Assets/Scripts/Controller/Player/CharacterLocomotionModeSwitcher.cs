using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterLocomotionModeSwitcher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerSubmersionState submersion;

    [Header("Providers")]
    [SerializeField] private CharacterMoveForce landMove;
    [SerializeField] private CharacterSwimForce swimMove;

    [Header("Policy")]
    [Tooltip("If true, disables upright-torque while swimming.")]

    private bool _wasSwimming;

    void Awake()
    {
        if (!submersion) submersion = GetComponent<PlayerSubmersionState>();

        if (!landMove) landMove = GetComponent<CharacterMoveForce>();
        if (!swimMove) swimMove = GetComponent<CharacterSwimForce>();

        if (submersion == null)
            Debug.LogError("CharacterLocomotionModeSwitcher requires PlayerSubmersionState.");

        if (landMove == null)
            Debug.LogError("CharacterLocomotionModeSwitcher couldn't find CharacterMoveForce.");

        if (swimMove == null)
            Debug.LogError("CharacterLocomotionModeSwitcher couldn't find CharacterSwimForce.");

        // Start consistent
        ApplyMode(submersion != null && submersion.SubmergedEnoughToSwim);
    }

    void FixedUpdate()
    {
        if (submersion == null) return;

        bool swimming = submersion.SubmergedEnoughToSwim;
        if (swimming == _wasSwimming) return;

        ApplyMode(swimming);
    }

    private void ApplyMode(bool swimming)
    {
        _wasSwimming = swimming;

        if (landMove != null) landMove.SetEnabled(!swimming);
        if (swimMove != null) swimMove.SetEnabled(swimming);
    }
}