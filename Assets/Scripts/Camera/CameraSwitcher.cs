using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    public Camera mainCamera;
    public Camera internalCamera;
    public Transform waveSystem; // assign your WaveSystem root object here

    public float mainCameraWaveZ = 0f;
    public float internalCameraWaveZ = 10f;

    void Update()
    {
        // CameraManager already routes C for managed gameplay cameras.
        if (CameraManager.ForCamera(mainCamera) != null) return;
        // Press "C" to toggle cameras
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleCameras();
        }
    }

    // Switch to internal camera
    public void SwitchToInternal()
    {
        var owner = CameraManager.ForCamera(internalCamera);
        if (owner != null) { owner.ActivateCamera(internalCamera); return; }
        mainCamera.enabled = false;
        internalCamera.enabled = true;

        // Move ocean behind the compartments for internal view
        if (waveSystem != null)
        {
            Vector3 pos = waveSystem.localPosition;
            waveSystem.localPosition = new Vector3(pos.x, pos.y, internalCameraWaveZ);
        }
    }

    // Switch to main camera
    public void SwitchToMain()
    {
        var owner = CameraManager.ForCamera(mainCamera);
        if (owner != null) { owner.ActivateCamera(mainCamera); return; }
        internalCamera.enabled = false;
        mainCamera.enabled = true;

        // Move ocean in front for main camera view
        if (waveSystem != null)
        {
            Vector3 pos = waveSystem.localPosition;
            waveSystem.localPosition = new Vector3(pos.x, pos.y, mainCameraWaveZ);
        }
    }

    // Toggle between cameras
    public void ToggleCameras()
    {
        if (internalCamera.enabled)
            SwitchToMain();
        else
            SwitchToInternal();
    }
}
