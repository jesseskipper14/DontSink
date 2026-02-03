using UnityEngine;

/// <summary>
/// Local input adapter for interaction. In MP, only owning client runs this.
/// </summary>
public class LocalInteractionIntentSource : MonoBehaviour, IInteractionIntentSource
{
    [Header("Bindings (legacy input manager)")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Aim")]
    [Tooltip("If true, uses mouse position as aim world point. If false, AimWorld will be Vector2.zero.")]
    [SerializeField] private bool useMouseAim = true;

    public InteractionIntent Current { get; private set; }

    void Update()
    {
        Vector2 aimWorld = Vector2.zero;
        if (useMouseAim && Camera.main != null)
        {
            Vector3 m = Input.mousePosition;
            aimWorld = Camera.main.ScreenToWorldPoint(m);
        }

        Current = new InteractionIntent
        {
            InteractPressed = Input.GetKeyDown(interactKey),
            AimWorld = aimWorld
        };
    }
}
