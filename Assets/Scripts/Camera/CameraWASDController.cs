using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraWASDController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 15f;
    public float fastMultiplier = 3f;

    [Header("Zoom (optional)")]
    public bool enableZoom = true;
    public float zoomSpeed = 10f;
    public float minOrthoSize = 2f;
    public float maxOrthoSize = 50f;

    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
    }

    private void Update()
    {
        HandleMovement();
        if (enableZoom)
            HandleZoom();
    }

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A / D
        float v = Input.GetAxisRaw("Vertical");   // W / S

        if (h == 0f && v == 0f) return;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastMultiplier;

        Vector3 delta = new Vector3(h, v, 0f).normalized * speed * Time.deltaTime;
        transform.position += delta;
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        float size = _cam.orthographicSize;
        size -= scroll * zoomSpeed * Time.deltaTime * 10f;
        _cam.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
    }
}
