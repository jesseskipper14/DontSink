using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Actor-local transient session. Renderer suppression exists only inside its camera render.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class BoatObservationPresentationController : MonoBehaviour, IInteractionTargetFilter, IEscapeClosable
{
    [Tooltip("Optional explicit local camera for a future multi-view bootstrap. Otherwise uses this actor's CameraManager camera.")]
    [SerializeField] private Camera viewCamera;
    private ObservationTelescopeInteractable telescope;
    private Camera sessionCamera;
    private CameraManager manager;
    private bool usesManagerCamera;
    private int sessionBindingVersion;
    private CameraWASDController freeCamera;
    private bool freeCameraWasEnabled;
    private float priorSize, priorFov, zoom, nextClearanceCheck;
    private Vector2 priorPan;
    private int actionFrame;
    private IInteractionIntentSource intent;
    private Boat sessionBoat;
    private EscapeCloseRegistry escapeRegistry;
    private bool isExiting;
    private float observationBlend, fadeDuration;
    private LayerMask additionalHiddenLayers;
    private readonly HashSet<int> additionalHiddenSortingLayers = new HashSet<int>();
    private readonly BoatObservationRenderFade renderFade = new BoatObservationRenderFade();
    private static readonly List<BoatObservationPresentationController> skySessions = new List<BoatObservationPresentationController>();
    private Vector3 skyOrigin, skyActorOrigin;
    private Quaternion skyRotation;

    /// <summary>Local fixed sky frame lets RMB pan uncover offscreen stars instead of dragging them with the camera.</summary>
    internal static Vector3 ProjectSkyViewport(Camera camera, Vector2 viewport, float depth)
    {
        Vector3 live = camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
        foreach (var session in skySessions)
        {
            if (session == null || !session.IsActive || session.sessionCamera != camera || !camera.orthographic) continue;
            Vector3 origin = session.skyOrigin + session.transform.position - session.skyActorOrigin;
            Vector3 fixedPoint = origin + session.skyRotation * new Vector3(
                (viewport.x - .5f) * 2f * session.priorSize * camera.aspect,
                (viewport.y - .5f) * 2f * session.priorSize, depth);
            return session.isExiting ? Vector3.Lerp(live, fixedPoint,
                Mathf.SmoothStep(0f, 1f, session.observationBlend)) : fixedPoint;
        }
        return live;
    }
    public int EscapePriority => 1000;
    public bool IsEscapeOpen => IsActive && !isExiting;
    private bool HasLocalAuthority(out bool hasMarker)
    {
        hasMarker = false;
        foreach (MonoBehaviour component in GetComponentsInParent<MonoBehaviour>(true))
            if (component is ILocalPlayerAuthority local)
            {
                hasMarker = true;
                if (!local.IsLocal) return false;
            }
        return true;
    }
    private readonly Dictionary<Renderer, bool> renderSnapshot = new Dictionary<Renderer, bool>();
    private readonly HashSet<Renderer> renderers = new HashSet<Renderer>();
    private readonly HashSet<CanvasRenderer> canvasRenderers = new HashSet<CanvasRenderer>();
    private readonly Dictionary<CanvasRenderer, bool> canvasSnapshot = new Dictionary<CanvasRenderer, bool>();
    public bool IsObserving(ObservationTelescopeInteractable target) => IsEscapeOpen && telescope == target;
    public bool IsActive => sessionCamera != null;

    public bool TryEnter(ObservationTelescopeInteractable target)
    {
        if (IsActive || !isActiveAndEnabled || target == null || !target.Equipment.IsDeployed ||
            !target.IsValidUser(gameObject)) return false;
        PlayerBoardingState actor = GetComponentInParent<PlayerBoardingState>();
        manager = CameraManager.ForActor(this);
        if (manager == null || !manager.CanProvideGameplayInput) return false;
        if (!HasLocalAuthority(out bool hasLocalMarker)) return false;
        usesManagerCamera = viewCamera == null;
        if (usesManagerCamera && (manager == null || manager.ViewingPlayer != actor)) return false;
        if (!usesManagerCamera && CameraManager.ForCamera(viewCamera) != manager) return false;
        Camera camera = usesManagerCamera ? manager.ActiveCamera : viewCamera;
        if (camera == null || !camera.isActiveAndEnabled) return false;
        // One transient owner per camera; distinct cameras/actors may observe independently.
        foreach (var other in FindObjectsByType<BoatObservationPresentationController>(FindObjectsSortMode.None))
            if (other != this && other.IsActive && other.sessionCamera == camera) return false;
        if (!target.Clearance.HasClearance())
        {
            GameMessageService.PostWarning(SkyClearanceRequirement.BlockedMessage);
            return false;
        }
        FinishObservation();
        telescope = target;
        sessionBoat = target.Equipment.OwningBoat;
        isExiting = false;
        observationBlend = 0f;
        fadeDuration = target.FadeDuration;
        additionalHiddenLayers = target.AdditionalHiddenLayers;
        additionalHiddenSortingLayers.Clear();
        if (target.AdditionalHiddenSortingLayers != null)
            foreach (string layerName in target.AdditionalHiddenSortingLayers)
                foreach (SortingLayer layer in SortingLayer.layers)
                    if (!string.IsNullOrEmpty(layerName) && layer.name == layerName)
                        additionalHiddenSortingLayers.Add(layer.id);
        sessionCamera = camera;
        sessionBindingVersion = manager.BindingVersion;
        priorSize = camera.orthographicSize;
        priorFov = camera.fieldOfView;
        skyOrigin = camera.transform.position;
        skyRotation = camera.transform.rotation;
        skyActorOrigin = transform.position;
        skySessions.Add(this);
        priorPan = manager != null && usesManagerCamera ? manager.FocusPanOffset : Vector2.zero;
        zoom = target.MinimumZoom;
        freeCamera = camera.GetComponent<CameraWASDController>();
        if (freeCamera != null) { freeCameraWasEnabled = freeCamera.enabled; freeCamera.enabled = false; }
        intent = GetComponent<IInteractionIntentSource>();
        actionFrame = Time.frameCount;
        nextClearanceCheck = Time.unscaledTime + target.Clearance.RecheckInterval;
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
        Camera.onPreCull += BeginBuiltinCamera;
        Camera.onPostRender += EndBuiltinCamera;
        escapeRegistry = EscapeCloseRegistry.GetOrFind();
        if (escapeRegistry != null) escapeRegistry.Register(this);
        ApplyZoom();
        return true;
    }

    private void LateUpdate()
    {
        if (ReferenceEquals(sessionCamera, null)) return;
        if (sessionCamera == null || !sessionCamera.isActiveAndEnabled || sessionBoat == null)
        { FinishObservation(); return; }
        if (manager == null || !manager.CanProvideGameplayInput ||
            manager.BindingVersion != sessionBindingVersion)
        { FinishObservation(); return; }
        if (isExiting)
        {
            AdvanceTransition(Time.unscaledDeltaTime);
            return;
        }
        if (telescope == null || sessionCamera == null || !sessionCamera.isActiveAndEnabled ||
            !telescope.Equipment.IsDeployed || !telescope.IsValidUser(gameObject) ||
            !HasLocalAuthority(out _) || manager == null || !manager.CanProvideGameplayInput ||
            GameplayInputBlocker.IsBlocked || InteractionInputBlocker.IsBlocked ||
            (usesManagerCamera && (manager == null || manager.ActiveCamera != sessionCamera ||
                manager.ViewingPlayer != GetComponentInParent<PlayerBoardingState>())))
        { ExitObservation(); return; }
        if (Time.frameCount != actionFrame &&
            (intent != null && intent.Current.InteractPressed))
        { ExitObservation(); return; }
        if (Time.unscaledTime >= nextClearanceCheck)
        {
            nextClearanceCheck = Time.unscaledTime + telescope.Clearance.RecheckInterval;
            if (!telescope.Clearance.HasClearance())
            {
                ExitObservation();
                GameMessageService.PostWarning(SkyClearanceRequirement.BlockedMessage);
                return;
            }
        }
        zoom = Mathf.Clamp(zoom + Input.mouseScrollDelta.y * telescope.ZoomSpeed,
            telescope.MinimumZoom, telescope.MaximumZoom);
        ApplyZoom();
        AdvanceTransition(Time.unscaledDeltaTime);
    }

    private void AdvanceTransition(float deltaTime)
    {
        observationBlend = Mathf.MoveTowards(observationBlend, isExiting ? 0f : 1f,
            Mathf.Max(0f, deltaTime) / fadeDuration);
        ApplyZoom();
        if (isExiting && observationBlend <= 0f) FinishObservation();
    }

    private void ApplyZoom()
    {
        float effectiveZoom = Mathf.Lerp(1f, zoom, Mathf.SmoothStep(0f, 1f, observationBlend));
        if (sessionCamera.orthographic) sessionCamera.orthographicSize = priorSize / effectiveZoom;
        else sessionCamera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(priorFov * Mathf.Deg2Rad * 0.5f) / effectiveZoom) * Mathf.Rad2Deg;
    }

    public void ExitObservation()
    {
        if (!IsActive || isExiting) return;
        isExiting = true;
        if (escapeRegistry != null) escapeRegistry.Unregister(this);
        escapeRegistry = null;
        actionFrame = Time.frameCount;
        if (observationBlend <= 0f) FinishObservation();
    }

    public bool CloseFromEscape()
    {
        if (!IsEscapeOpen) return false;
        ExitObservation();
        return true;
    }

    private void FinishObservation()
    {
        skySessions.Remove(this);
        RestoreRenderers();
        if (escapeRegistry != null) escapeRegistry.Unregister(this);
        escapeRegistry = null;
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        Camera.onPreCull -= BeginBuiltinCamera;
        Camera.onPostRender -= EndBuiltinCamera;
        if (sessionCamera != null && manager != null && manager.IsLocal &&
            manager.BindingVersion == sessionBindingVersion)
        {
            sessionCamera.orthographicSize = priorSize;
            sessionCamera.fieldOfView = priorFov;
            if (manager != null && usesManagerCamera) manager.RestoreFocusPanOffset(priorPan);
        }
        if (freeCamera != null) freeCamera.enabled = freeCameraWasEnabled;
        telescope = null;
        sessionCamera = null;
        freeCamera = null;
        sessionBoat = null;
        additionalHiddenSortingLayers.Clear();
        isExiting = false;
        observationBlend = 0f;
        renderers.Clear();
        canvasRenderers.Clear();
        actionFrame = Time.frameCount; // Do not re-enter/exit twice on the same E press.
    }

    public bool AllowsInteractionTarget(MonoBehaviour targetOwner, in InteractContext context)
    {
        // E exit is handled actor-locally even when the hidden telescope is offscreen.
        return !IsActive && Time.frameCount != actionFrame;
    }

    private void AddRenderers(Component root)
    {
        if (root == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderers.Add(renderer);
        foreach (CanvasRenderer renderer in root.GetComponentsInChildren<CanvasRenderer>(true))
        {
            Canvas canvas = renderer.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) canvasRenderers.Add(renderer);
        }
    }
    private void BeginCamera(ScriptableRenderContext context, Camera camera) { BeginRender(camera); }
    private void EndCamera(ScriptableRenderContext context, Camera camera) { if (camera == sessionCamera) RestoreRenderers(); }
    private void BeginBuiltinCamera(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) BeginRender(camera); }
    private void EndBuiltinCamera(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null && camera == sessionCamera) RestoreRenderers(); }

    private void BeginRender(Camera camera)
    {
        RestoreRenderers(); // Never carry a suppression scope into another camera.
        if (!IsActive || camera != sessionCamera) return;
        Boat boat = sessionBoat;
        if (boat == null) return;
        renderers.Clear();
        canvasRenderers.Clear();
        AddRenderers(boat);
        AddRenderers(telescope);
        AddRenderers(GetComponentInParent<PlayerBoardingState>());
        BoatItemRegistry registry = boat.GetComponent<BoatItemRegistry>();
        if (registry != null)
            foreach (BoatOwnedItem item in registry.Items)
                if (item != null && item.OwningBoat == boat) AddRenderers(item);
        foreach (var actor in FindObjectsByType<PlayerBoardingState>(FindObjectsSortMode.None))
            if (actor.IsBoarded && actor.CurrentBoatRoot == boat.transform) AddRenderers(actor);
        if (additionalHiddenLayers.value != 0 || additionalHiddenSortingLayers.Count != 0)
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if ((additionalHiddenLayers.value & (1 << renderer.gameObject.layer)) != 0 ||
                    additionalHiddenSortingLayers.Contains(renderer.sortingLayerID)) renderers.Add(renderer);
            foreach (CanvasRenderer renderer in FindObjectsByType<CanvasRenderer>(FindObjectsSortMode.None))
            {
                Canvas canvas = renderer.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace &&
                    ((additionalHiddenLayers.value & (1 << renderer.gameObject.layer)) != 0 ||
                    additionalHiddenSortingLayers.Contains(canvas.sortingLayerID))) canvasRenderers.Add(renderer);
            }
        }
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderSnapshot[renderer] = renderer.forceRenderingOff;
            if (observationBlend >= 1f) renderer.forceRenderingOff = true;
            else renderFade.Apply(renderer, 1f - Mathf.SmoothStep(0f, 1f, observationBlend));
        }
        foreach (CanvasRenderer renderer in canvasRenderers)
        {
            if (renderer == null) continue;
            canvasSnapshot[renderer] = renderer.cull;
            if (observationBlend >= 1f) renderer.cull = true;
            else renderFade.Apply(renderer, 1f - Mathf.SmoothStep(0f, 1f, observationBlend));
        }
    }
    private void RestoreRenderers()
    {
        renderFade.Restore();
        foreach (var pair in renderSnapshot)
            if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        renderSnapshot.Clear();
        foreach (var pair in canvasSnapshot)
            if (pair.Key != null) pair.Key.cull = pair.Value;
        canvasSnapshot.Clear();
    }
    private void OnDisable() { FinishObservation(); }
    private void OnDestroy() { FinishObservation(); }
}
