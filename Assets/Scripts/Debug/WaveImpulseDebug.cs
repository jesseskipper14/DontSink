using UnityEngine;

public class WaveDebugTools : MonoBehaviour
{
    public WaveField wave;

    [Header("Impulse Settings")]
    public float impulseStrength = 1f;
    public float impulseRadius = 2f;

    [Header("Toggle Debug")]
    public bool enableClickImpulse = true;

    void Update()
    {
        if (!enableClickImpulse || wave == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            worldPos.z = 0f;

            wave.AddImpulse(worldPos.x, impulseStrength, impulseRadius);
            Debug.Log($"Impulse added at x={worldPos.x:F2}");
        }
    }
}
