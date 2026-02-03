using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaveRenderer_Line : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager; // exposed in editor

    private IWaveService wave => waveManager; // interface forwarding

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

        float centerX = Camera.main.transform.position.x;
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
}
