using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Boat boat;          // The boat to follow

    [Header("Follow Settings")]
    public Vector2 offset = new Vector2(0f, -2f);  // Move boat lower on screen
    public float followSpeed = 5f;                 // How fast camera catches up

    [Header("Rotation")]
    public bool enableRotation = true;
    public float rotationSmooth = 2f; // lerp speed for rotation
    public float tiltFactor = 0.3f;   // how much the camera leans opposite the boat's pitch

    private float currentTilt = 0f;
    private Transform camTransform;

    void Awake()
    {
        camTransform = transform;
    }

    void LateUpdate()
    {
        if (boat == null) return;

        // Keep camera Z position intact
        float cameraZ = camTransform.position.z;

        // Desired camera position = target position + offset
        Vector3 desiredPosition = new Vector3(
            boat.transform.position.x + offset.x,
            boat.transform.position.y + offset.y,
            cameraZ
        );

        // --- Position ---
        camTransform.position = desiredPosition;

        // --- Rotation ---
        if (enableRotation)
        {
            float targetRotation = boat.rb.rotation * Mathf.Rad2Deg;

            // Apply small tilt opposite to boat pitch
            float tilt = -targetRotation * tiltFactor;
            currentTilt = Mathf.Lerp(currentTilt, tilt, Time.deltaTime * rotationSmooth);

            camTransform.rotation =
                Quaternion.Euler(0f, 0f, targetRotation + currentTilt);
        }
        else
        {
            // Hard reset to neutral rotation
            currentTilt = 0f;
            camTransform.rotation = Quaternion.identity;
        }
    }
}
