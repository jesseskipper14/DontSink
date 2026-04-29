using UnityEngine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera internalCamera;
    public List<Camera> otherCameras = new List<Camera>();

    [Header("Wave System")]
    public Transform waveSystem;
    public float mainCameraWaveZ = 0f;
    public float internalCameraWaveZ = 10f;

    [Header("Defaults")]
    [Tooltip("Zoom level to reset to when this controller activates.")]
    public float defaultOrthoSize = 10f;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [Min(0f)][SerializeField] private float followSmooth = 12f;
    [SerializeField] private bool followActiveCameraOnly = true;

    [Header("Focus Soft Pan")]
    [Tooltip("Optional intent source. If left empty, this will search the follow target and its parents.")]
    [SerializeField] private MonoBehaviour intentSourceComponent;

    [Tooltip("Enables soft camera panning while focus/right-click is held.")]
    [SerializeField] private bool focusSoftPanEnabled = true;

    [Tooltip("How much of the player-to-cursor offset is applied to the camera.")]
    [Min(0f)][SerializeField] private float focusPanStrength = 0.35f;

    [Tooltip("Maximum world-space camera offset from focus panning.")]
    [Min(0f)][SerializeField] private float focusPanMaxOffset = 3f;

    [Tooltip("How quickly the soft pan offset catches up.")]
    [Min(0f)][SerializeField] private float focusPanSmooth = 10f;

    private Camera activeCamera;
    private ICharacterIntentSource _intentSource;
    private Vector2 _focusPanOffset;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ResolveIntentSource();

        ActivateCamera(mainCamera);

        if (mainCamera != null)
            mainCamera.orthographic = true;

        if (defaultOrthoSize <= 0f && mainCamera != null)
            defaultOrthoSize = mainCamera.orthographicSize;

        ResetZoom();
    }

    private void OnEnable()
    {
        ResetZoom();
    }

    public void SetFollowTarget(Transform t)
    {
        followTarget = t;
        ResolveIntentSource();
    }

    public void SetFocusPanOverride(float strength, float maxOffset, float smooth)
    {
        focusPanStrength = Mathf.Max(0f, strength);
        focusPanMaxOffset = Mathf.Max(0f, maxOffset);
        focusPanSmooth = Mathf.Max(0f, smooth);
    }

    public void SetFocusPanEnabled(bool enabled)
    {
        focusSoftPanEnabled = enabled;

        if (!enabled)
            _focusPanOffset = Vector2.zero;
    }

    public void ActivateCamera(Camera cam)
    {
        if (cam == null) return;

        foreach (var c in GetAllCameras())
            if (c != null) c.enabled = false;

        cam.enabled = true;
        activeCamera = cam;

        if (waveSystem != null)
        {
            if (cam == internalCamera) SetWaveZ(internalCameraWaveZ);
            else SetWaveZ(mainCameraWaveZ);
        }
    }

    public void ToggleNextCamera()
    {
        var cameras = GetAllCameras();
        if (cameras.Count == 0) return;

        int index = cameras.IndexOf(activeCamera);
        index = (index + 1) % cameras.Count;

        ActivateCamera(cameras[index]);
    }

    private List<Camera> GetAllCameras()
    {
        var list = new List<Camera>();
        if (mainCamera != null) list.Add(mainCamera);
        if (internalCamera != null) list.Add(internalCamera);
        for (int i = 0; i < otherCameras.Count; i++)
            if (otherCameras[i] != null) list.Add(otherCameras[i]);
        return list;
    }

    private void LateUpdate()
    {
        if (followTarget == null) return;

        UpdateFocusPanOffset();

        if (followActiveCameraOnly)
        {
            if (activeCamera != null)
                Follow(activeCamera.transform);
        }
        else
        {
            foreach (var c in GetAllCameras())
                Follow(c.transform);
        }
    }

    private void Follow(Transform camXform)
    {
        Vector3 desired =
            followTarget.position +
            followOffset +
            new Vector3(_focusPanOffset.x, _focusPanOffset.y, 0f);

        if (followSmooth <= 0.0001f)
        {
            camXform.position = desired;
            return;
        }

        float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        camXform.position = Vector3.Lerp(camXform.position, desired, t);
    }

    private void UpdateFocusPanOffset()
    {
        Vector2 targetOffset = Vector2.zero;

        if (focusSoftPanEnabled)
        {
            if (_intentSource == null)
                ResolveIntentSource();

            if (_intentSource != null)
            {
                CharacterIntent intent = _intentSource.Current;

                if (intent.FocusHeld)
                {
                    Vector2 fromPlayerToFocus =
                        intent.FocusWorldPoint - (Vector2)followTarget.position;

                    targetOffset = fromPlayerToFocus * focusPanStrength;

                    if (targetOffset.magnitude > focusPanMaxOffset)
                        targetOffset = targetOffset.normalized * focusPanMaxOffset;
                }
            }
        }

        if (focusPanSmooth <= 0.0001f)
        {
            _focusPanOffset = targetOffset;
            return;
        }

        float t = 1f - Mathf.Exp(-focusPanSmooth * Time.deltaTime);
        _focusPanOffset = Vector2.Lerp(_focusPanOffset, targetOffset, t);
    }

    private void ResolveIntentSource()
    {
        _intentSource = intentSourceComponent as ICharacterIntentSource;

        if (_intentSource != null)
            return;

        if (followTarget == null)
            return;

        foreach (MonoBehaviour mb in followTarget.GetComponentsInParent<MonoBehaviour>())
        {
            if (mb is ICharacterIntentSource source)
            {
                _intentSource = source;
                intentSourceComponent = mb;
                return;
            }
        }

        foreach (MonoBehaviour mb in followTarget.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb is ICharacterIntentSource source)
            {
                _intentSource = source;
                intentSourceComponent = mb;
                return;
            }
        }
    }

    private void SetWaveZ(float z)
    {
        Vector3 pos = waveSystem.localPosition;
        waveSystem.localPosition = new Vector3(pos.x, pos.y, z);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            ToggleNextCamera();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void ResetZoom()
    {
        if (mainCamera)
            mainCamera.orthographicSize = defaultOrthoSize;
    }
}