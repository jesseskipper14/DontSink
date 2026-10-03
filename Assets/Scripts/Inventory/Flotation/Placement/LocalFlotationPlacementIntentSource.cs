using UnityEngine;

/// <summary>
/// Local singleplayer input adapter for deployable flotation placement.
///
/// The local placement key is intentionally confined to this class. Gameplay code receives semantic
/// placement intents only, leaving a future network client free to replace
/// this local hop without teaching authority APIs about keyboard keys.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalFlotationPlacementIntentSource : MonoBehaviour, IContextHintProvider
{
    [SerializeField] private FlotationPlacementController controller;

    [Header("Bindings (legacy input manager)")]
    [SerializeField] private KeyCode beginPlacementKey = KeyCode.B;

    [Tooltip("One-time migration marker for the original Ctrl+P-hostile default binding.")]
    [SerializeField, HideInInspector] private bool migratedLegacyPlacementBinding;
    [SerializeField] private int commitMouseButton = 0;
    [SerializeField] private int cancelMouseButton = 1;

    [Header("Context Hint")]
    [SerializeField] private int contextHintPriority = 100;

    [Header("Gameplay Input Blocking")]
    [SerializeField] private bool respectGameplayInputBlocker = true;

    [Header("Debug")]
    [SerializeField] private bool logIntentResults;

    public string BeginPlacementBindingLabel =>
        beginPlacementKey == KeyCode.None
            ? "Place"
            : beginPlacementKey.ToString();

    public int ContextHintPriority =>
        controller != null && controller.IsPlacementActive
            ? contextHintPriority + 200
            : contextHintPriority;

    private void Reset()
    {
        beginPlacementKey = KeyCode.B;
        migratedLegacyPlacementBinding = true;
        ResolveController();
    }

    private void OnValidate()
    {
        MigrateLegacyPlacementBinding();
    }

    private void Awake()
    {
        MigrateLegacyPlacementBinding();
        ResolveController();
    }

    private void OnEnable()
    {
        ContextHintOverlay.Register(this);
    }

    private void OnDisable()
    {
        ContextHintOverlay.Unregister(this);
        controller?.CancelLocalPlacement();
    }

    private void Update()
    {
        if (controller == null)
        {
            ResolveController();
            return;
        }

        if (!CameraManager.HasGameplayInput(this) ||
            respectGameplayInputBlocker && GameplayInputBlocker.IsBlocked)
        {
            controller.CancelLocalPlacement();
            return;
        }

        Vector2 aimWorld = default;
        bool hasAimWorld = false;

        Camera camera = CameraManager.CameraForActor(this);
        if (camera != null)
        {
            Vector3 mouse = Input.mousePosition;
            aimWorld = camera.ScreenToWorldPoint(mouse);
            hasAimWorld = true;
        }

        controller.SetLocalAimPreview(aimWorld, hasAimWorld);

        if (Input.GetKeyDown(beginPlacementKey))
        {
            Send(
                FlotationPlacementIntentRequest.Create(
                    FlotationPlacementIntentKind.TogglePlacement,
                    aimWorld,
                    hasAimWorld));

            return;
        }

        if (!controller.IsPlacementActive)
            return;

        if (cancelMouseButton >= 0 && Input.GetMouseButtonDown(cancelMouseButton))
        {
            Send(
                FlotationPlacementIntentRequest.Create(
                    FlotationPlacementIntentKind.CancelPlacement,
                    aimWorld,
                    hasAimWorld));

            return;
        }

        if (commitMouseButton >= 0 &&
            Input.GetMouseButtonDown(commitMouseButton))
        {
            Send(
                FlotationPlacementIntentRequest.Create(
                    FlotationPlacementIntentKind.CommitPlacement,
                    aimWorld,
                    hasAimWorld));
        }
    }

    public bool TryGetContextHint(
        out string text,
        out Transform worldAnchor)
    {
        ResolveController();

        text = null;
        worldAnchor = controller != null ? controller.transform : transform;

        if (!CameraManager.HasGameplayInput(this) || controller == null ||
            (respectGameplayInputBlocker && GameplayInputBlocker.IsBlocked))
        {
            return false;
        }

        if (controller.IsPlacementActive)
        {
            text = "LMB ATTACH   •   RMB / ESC CANCEL";
            return true;
        }

        if (!controller.HasSelectedDeployableFlotationItem)
            return false;

        string binding = BeginPlacementBindingLabel.ToUpperInvariant();
        text = $"PRESS {binding} TO ATTACH";
        return true;
    }

    private void Send(FlotationPlacementIntentRequest request)
    {
        bool ok = controller.TryApplyIntent(request, out string message);

        if (logIntentResults)
        {
            Debug.Log(
                $"[FlotationPlacementIntent:{name}] {request.Kind} -> " +
                $"{(ok ? "ACCEPTED" : "REJECTED")} | {message}",
                this);
        }
    }


    private void MigrateLegacyPlacementBinding()
    {
        if (migratedLegacyPlacementBinding)
            return;

        // Pass 02 originally used P. Holding Ctrl to dive while pressing P can
        // trigger Unity Editor shortcuts, which is a truly inspired way for a
        // flotation tool to stop Play Mode. Existing serialized components get
        // migrated once; any later user-selected binding is left alone.
        if (beginPlacementKey == KeyCode.P)
            beginPlacementKey = KeyCode.B;

        migratedLegacyPlacementBinding = true;
    }

    private void ResolveController()
    {
        if (controller != null)
            return;

        controller = GetComponent<FlotationPlacementController>();

        if (controller == null)
            controller = GetComponentInChildren<FlotationPlacementController>(true);

        if (controller == null)
            controller = GetComponentInParent<FlotationPlacementController>();
    }
}
