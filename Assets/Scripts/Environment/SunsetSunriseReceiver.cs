using UnityEngine;
using UnityEngine.Events;

public class SunriseSunsetReceiver : MonoBehaviour
{
    [Tooltip("Optional: reference to a manager. If null, will use EnvironmentManager.Instance.")]
    [SerializeField] private SunriseSunsetOverlayManager sunriseSunsetManager;
    [SerializeField] private UnityEvent<float> onTintChanged;

    private void Awake()
    {
        // Use serialized reference first
        if (sunriseSunsetManager == null)
        {
            if (EnvironmentManager.Instance == null)
            {
                Debug.LogWarning("SunriseSunsetReceiver: EnvironmentManager instance not found!");
                return;
            }

            sunriseSunsetManager =
                EnvironmentManager.Instance
                    .GetComponent<SunriseSunsetOverlayManager>();
        }

        if (sunriseSunsetManager != null)
        {
            sunriseSunsetManager.OnTintChanged += HandleTintChanged;
            HandleTintChanged(sunriseSunsetManager.Tint01);
        }
    }

    private void OnDestroy()
    {
        if (sunriseSunsetManager != null)
        {
            sunriseSunsetManager.OnTintChanged -= HandleTintChanged;
        }
    }

    protected virtual void HandleTintChanged(float tint01)
    {
        Debug.Log("Inside Receiver: " + tint01);
        onTintChanged?.Invoke(tint01);
    }
}
