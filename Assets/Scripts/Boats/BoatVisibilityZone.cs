using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatVisibilityZone : MonoBehaviour
{
    [SerializeField] private BoatVisibilityMode mode = BoatVisibilityMode.BoardedInterior;
    [SerializeField] private BoatVisualStateController controller;

    [Header("Priority")]
    [SerializeField] private int priority = 0;

    [Header("Debug")]
    [SerializeField] private bool logEvents = false;

    public BoatVisibilityMode Mode => mode;
    public int Priority => priority;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        controller = GetComponentInParent<BoatVisualStateController>();
        priority = GetDefaultPriority(mode);
    }

    private void OnValidate()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponentInParent<BoatVisualStateController>();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        if (logEvents)
        {
            Debug.Log(
                $"[BoatVisibilityZone:{name}] Awake mode={mode}, priority={priority}, " +
                $"controller={(controller != null ? controller.name : "NULL")}, collider={(col != null ? col.name : "NULL")}, isTrigger={(col != null && col.isTrigger)}",
                this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerBoardingState boarding = other.GetComponentInParent<PlayerBoardingState>();

        if (logEvents)
        {
            Debug.Log(
                $"[BoatVisibilityZone:{name}] ENTER other={other.name}, otherRoot={other.transform.root.name}, " +
                $"boarding={(boarding != null ? boarding.name : "NULL")}, " +
                $"isBoarded={(boarding != null && boarding.IsBoarded)}, " +
                $"currentBoat={(boarding != null && boarding.CurrentBoatRoot != null ? boarding.CurrentBoatRoot.name : "NULL")}, " +
                $"mode={mode}, priority={priority}",
                this);
        }

        if (boarding == null)
        {
            LogReject("No PlayerBoardingState found in parent.", other);
            return;
        }

        if (!boarding.IsBoarded)
        {
            LogReject("Player is not boarded.", other);
            return;
        }

        if (controller == null)
        {
            LogReject("No BoatVisualStateController assigned/found.", other);
            return;
        }

        controller.NotifyPlayerEnteredZone(boarding, this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerBoardingState boarding = other.GetComponentInParent<PlayerBoardingState>();

        if (logEvents)
        {
            Debug.Log(
                $"[BoatVisibilityZone:{name}] EXIT other={other.name}, otherRoot={other.transform.root.name}, " +
                $"boarding={(boarding != null ? boarding.name : "NULL")}, " +
                $"isBoarded={(boarding != null && boarding.IsBoarded)}, " +
                $"currentBoat={(boarding != null && boarding.CurrentBoatRoot != null ? boarding.CurrentBoatRoot.name : "NULL")}, " +
                $"mode={mode}, priority={priority}",
                this);
        }

        if (boarding == null)
            return;

        if (controller == null)
            return;

        controller.NotifyPlayerExitedZone(boarding, this);
    }

    private void LogReject(string reason, Collider2D other)
    {
        if (!logEvents)
            return;

        Debug.LogWarning(
            $"[BoatVisibilityZone:{name}] REJECT other={other.name}: {reason}",
            this);
    }

    public static int GetDefaultPriority(BoatVisibilityMode mode)
    {
        return mode switch
        {
            BoatVisibilityMode.BoardedInterior => 100,
            BoatVisibilityMode.Transition => 50,
            BoatVisibilityMode.BoardedExteriorDeck => 10,
            BoatVisibilityMode.UnboardedExterior => 0,
            _ => 0
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Set Default Priority For Mode")]
    private void EditorSetDefaultPriority()
    {
        priority = GetDefaultPriority(mode);
    }

    public void EditorConfigure(
    BoatVisibilityMode newMode,
    int newPriority,
    BoatVisualStateController newController)
    {
        mode = newMode;
        priority = newPriority;
        controller = newController;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}