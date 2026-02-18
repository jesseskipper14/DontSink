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

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [Min(0f)][SerializeField] private float followSmooth = 12f; // 0 = snap
    [SerializeField] private bool followActiveCameraOnly = true;

    private Camera activeCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ActivateCamera(mainCamera);
    }

    public void SetFollowTarget(Transform t) => followTarget = t;

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

        // Follow either the active camera only, or all cameras (usually active only).
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
        Vector3 desired = followTarget.position + followOffset;

        if (followSmooth <= 0.0001f)
        {
            camXform.position = desired;
            return;
        }

        // exponential damping (frame-rate independent-ish)
        float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        camXform.position = Vector3.Lerp(camXform.position, desired, t);
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
}
