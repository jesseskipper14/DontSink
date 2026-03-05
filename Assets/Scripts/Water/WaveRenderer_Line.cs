using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaveRenderer_Line : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    private IWaveService wave => waveManager;

    [Header("Centering")]
    [Tooltip("What to center the wave line around. If null, will fall back to Camera.main.")]
    [SerializeField] private Transform centerTarget;

    [Header("Rendering")]
    public int points = 200;
    public float width = 100f;

    LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    void LateUpdate()
    {
        if (wave == null) return;
        if (!TryGetCenterX(out float centerX)) return;

        float startX = centerX - width * 0.5f;
        float dx = width / (points - 1);

        lr.positionCount = points;

        for (int i = 0; i < points; i++)
        {
            float x = startX + i * dx;
            float y = wave.SampleHeightAtWorldXWrapped(x);
            lr.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    bool TryGetCenterX(out float centerX)
    {
        if (centerTarget != null)
        {
            centerX = centerTarget.position.x;
            return true;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            centerX = cam.transform.position.x;
            return true;
        }

        centerX = 0f;
        return false;
    }
}