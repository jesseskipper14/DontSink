using UnityEngine;

public struct BoatControlIntent
{
    /// <summary>
    /// Requested throttle-lever movement this frame.
    /// -1 = reduce throttle, 0 = leave it alone, +1 = increase throttle.
    /// This is intent, NOT the persistent throttle position.
    /// </summary>
    public float ThrottleAdjust;

    /// <summary>
    /// Requested rudder/helm movement this frame.
    /// -1 = port, 0 = leave it alone, +1 = starboard.
    /// This is intent, NOT the persistent rudder angle.
    /// </summary>
    public float RudderAdjust;

    /// <summary>Rising edge request to leave the helm.</summary>
    public bool ExitPressed;

    public static BoatControlIntent Neutral => default;
}

public interface IBoatControlIntentSource
{
    BoatControlIntent Current { get; }
}

[DisallowMultipleComponent]
public class LocalBoatControlIntentSource : MonoBehaviour, IBoatControlIntentSource
{
    [Header("Bindings (local input only)")]
    [SerializeField] private KeyCode throttleUp = KeyCode.W;
    [SerializeField] private KeyCode throttleDown = KeyCode.S;
    [SerializeField] private KeyCode rudderPort = KeyCode.A;
    [SerializeField] private KeyCode rudderStarboard = KeyCode.D;
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;

    public BoatControlIntent Current { get; private set; }

    private void Update()
    {
        float throttleAdjust =
            (Input.GetKey(throttleUp) ? 1f : 0f) -
            (Input.GetKey(throttleDown) ? 1f : 0f);

        float rudderAdjust =
            (Input.GetKey(rudderStarboard) ? 1f : 0f) -
            (Input.GetKey(rudderPort) ? 1f : 0f);

        Current = new BoatControlIntent
        {
            ThrottleAdjust = Mathf.Clamp(throttleAdjust, -1f, 1f),
            RudderAdjust = Mathf.Clamp(rudderAdjust, -1f, 1f),
            ExitPressed = Input.GetKeyDown(exitKey)
        };
    }

    private void OnDisable()
    {
        Current = BoatControlIntent.Neutral;
    }
}