using UnityEngine;

public struct BoatControlIntent
{
    public float Throttle;     // [-1..1]
    public bool ExitPressed;   // rising edge
}

public interface IBoatControlIntentSource
{
    BoatControlIntent Current { get; }
}

public class LocalBoatControlIntentSource : MonoBehaviour, IBoatControlIntentSource
{
    [Header("Bindings (legacy input manager)")]
    [SerializeField] private KeyCode throttleUp = KeyCode.W;
    [SerializeField] private KeyCode throttleDown = KeyCode.S;
    [SerializeField] private KeyCode exitKey = KeyCode.Escape;

    [Header("Tuning")]
    [SerializeField] private float throttleRampPerSecond = 3f; // how fast it ramps
    [SerializeField] private float throttleReturnPerSecond = 4f; // how fast it returns to 0 when no input

    public BoatControlIntent Current { get; private set; }

    private float throttle;

    void Update()
    {
        float target =
            (Input.GetKey(throttleUp) ? 1f : 0f) +
            (Input.GetKey(throttleDown) ? -1f : 0f);

        if (Mathf.Abs(target) > 0.001f)
        {
            throttle = Mathf.MoveTowards(throttle, target, throttleRampPerSecond * Time.deltaTime);
        }
        else
        {
            throttle = Mathf.MoveTowards(throttle, 0f, throttleReturnPerSecond * Time.deltaTime);
        }

        Current = new BoatControlIntent
        {
            Throttle = throttle,
            ExitPressed = Input.GetKeyDown(exitKey)
        };
    }
}
