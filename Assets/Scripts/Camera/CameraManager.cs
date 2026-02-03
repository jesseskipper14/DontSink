using UnityEngine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    // --- Singleton ---
    public static CameraManager Instance { get; private set; }

    [Header("Cameras")]
    public Camera mainCamera;
    public Camera internalCamera;
    public List<Camera> otherCameras = new List<Camera>();

    [Header("Wave System")]
    public Transform waveSystem;
    public float mainCameraWaveZ = 0f;
    public float internalCameraWaveZ = 10f;

    private Camera activeCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Start with main camera active
        ActivateCamera(mainCamera);
    }

    // Activate a specific camera
    public void ActivateCamera(Camera cam)
    {
        if (cam == null) return;

        // Disable all cameras
        foreach (var c in GetAllCameras())
            c.enabled = false;

        // Enable the selected camera
        cam.enabled = true;
        activeCamera = cam;

        // Adjust wave system Z if needed
        if (waveSystem != null)
        {
            if (cam == internalCamera)
                SetWaveZ(internalCameraWaveZ);
            else
                SetWaveZ(mainCameraWaveZ);
        }
    }

    // Toggle to the next camera in your list
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
        List<Camera> list = new List<Camera> { mainCamera, internalCamera };
        list.AddRange(otherCameras);
        return list;
    }

    private void SetWaveZ(float z)
    {
        Vector3 pos = waveSystem.localPosition;
        waveSystem.localPosition = new Vector3(pos.x, pos.y, z);
    }

    // Example key input (optional)
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            ToggleNextCamera();
    }
}
