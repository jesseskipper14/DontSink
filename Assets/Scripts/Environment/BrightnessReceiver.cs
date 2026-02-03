using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(MonoBehaviour))]
public class BrightnessReceiver : MonoBehaviour
{
    [Tooltip("Optional: reference to a manager. If null, will use EnvironmentManager.Instance.Brightness.")]
    [SerializeField] private GlobalBrightnessManager brightnessManager;
    [SerializeField] private UnityEvent<float> onBrightnessChanged;

    private void Awake()
    {
        // Use the serialized manager if assigned, otherwise fallback to singleton
        if (brightnessManager == null)
        {
            if (EnvironmentManager.Instance == null)
            {
                Debug.LogWarning("BrightnessReceiver: EnvironmentManager instance not found!");
                return;
            }
            brightnessManager = EnvironmentManager.Instance.Brightness as GlobalBrightnessManager;
        }

        if (brightnessManager != null)
        {
            brightnessManager.OnBrightnessChanged += HandleBrightnessChanged;

            // Apply initial value immediately
            HandleBrightnessChanged(brightnessManager.Brightness01);
        }
    }

    private void OnDestroy()
    {
        if (brightnessManager != null)
        {
            brightnessManager.OnBrightnessChanged -= HandleBrightnessChanged;
        }
    }

    /// <summary>
    /// Override this method in a derived class, or assign via a lambda in inspector
    /// to respond to brightness changes.
    /// </summary>
    /// <param name="brightness">Current brightness [0..1]</param>
    protected virtual void HandleBrightnessChanged(float brightness)
    {
        onBrightnessChanged?.Invoke(brightness);
        //Debug.Log($"{gameObject.name} received brightness update: {brightness}");
    }
}
